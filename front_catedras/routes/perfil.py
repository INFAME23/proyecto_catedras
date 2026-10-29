# ==========================================================================
#  routes/perfil.py  ·  Front Cátedras · Subversión v7.1 (RF-S5, RF-S6)
# --------------------------------------------------------------------------
#  DOS FLUJOS DISTINTOS para la clave (a propósito, y los dos existen):
#
#   1) CAMBIAR la clave sabiendo la actual  ->  GET/POST /perfil/clave
#      Para cualquier usuario autenticado (admin, ponente, estudiante).
#      Pide clave actual + nueva + confirmación; la API verifica la actual con
#      BCrypt. NO exige código por correo: quien conoce su clave ya demostró
#      quién es, y obligarlo a pasar por el correo solo agrega fricción.
#
#   2) RECUPERAR una clave olvidada  ->  /recuperar y /recuperar/codigo
#      Público (no hay sesión: justo por eso la recupera). La persona escribe
#      su usuario o correo; la API genera un código de un solo uso (patrón de
#      ClaveAcceso, v5) y ESTE front lo ENVÍA POR CORREO (servicios/correo.py);
#      nunca se muestra en pantalla. Con el código elige la clave nueva.
#
#  NO REVELAR QUÉ CUENTAS EXISTEN (ADR D27): "te enviamos un código si existe
#  esa cuenta" se dice igual haya o no cuenta, y cualquier fallo al canjear el
#  código muestra el mismo mensaje genérico (no se distingue "no existe",
#  "ya se usó", "venció" ni "se agotaron los intentos").
#
#  El id del código viaja en la sesión de Flask (cookie firmada); el código en
#  sí solo existe en el correo. La clave nueva la hashea la API (BCrypt, Guía
#  IA Regla 3); este front jamás ve un hash.
# ==========================================================================

import ipaddress
import logging

from flask import Blueprint, flash, redirect, render_template, request, session, url_for

from auth_helpers import endpoint_de_inicio, sesion_activa, usuario_actual
from servicios import correo
from servicios.api_catedras import ErrorApi, crear

perfil_bp = Blueprint('perfil', __name__)
registro = logging.getLogger(__name__)

CLAVE_MIN = 8   # el mismo mínimo que exige la API (UsuarioCrear / UsuarioCambiarClave)

# Un id que no existe, para la persona que pidió recuperar una cuenta que no
# existe: así sigue el mismo camino de pantallas y solo falla al canjear, con
# el mismo mensaje genérico que cualquier otro fallo.
_ID_CODIGO_INEXISTENTE = 9223372036854775807

MINUTOS_VIGENCIA = 10    # = parametro CLAVE_VIGENCIA_MINUTOS por defecto; solo para el texto del correo

MENSAJE_CODIGO = ('El código no es válido, ya se usó o venció. Revisa tu correo o '
                  'solicita uno nuevo.')


def _ip_cliente():
    try:
        return str(ipaddress.ip_address(request.remote_addr))
    except (TypeError, ValueError):
        return None


def _validar_claves_nuevas(nueva, confirmacion):
    """Mensaje de error de la clave nueva, o None si está bien. (La API la
    vuelve a validar; esto es para decirlo antes y en claro.)"""
    if len(nueva) < CLAVE_MIN:
        return f'La clave nueva debe tener al menos {CLAVE_MIN} caracteres.'
    if nueva != confirmacion:
        return 'La confirmación no coincide con la clave nueva.'
    return None


# --------------------------------------------------------------------------
#  1) Cambiar la clave (sesión iniciada)
# --------------------------------------------------------------------------
@perfil_bp.route('/perfil/clave', methods=['GET', 'POST'])
def cambiar_clave():
    if not sesion_activa():
        return redirect(url_for('auth.login', siguiente=url_for('perfil.cambiar_clave')))

    if request.method == 'GET':
        return render_template('perfil_clave.html')

    actual = request.form.get('clave_actual', '')      # las claves NO se recortan
    nueva = request.form.get('clave_nueva', '')
    confirmacion = request.form.get('clave_confirmacion', '')

    error = _validar_claves_nuevas(nueva, confirmacion)
    if error:
        flash(error, 'error')
        return render_template('perfil_clave.html'), 200
    if nueva == actual:
        flash('La clave nueva debe ser distinta de la actual.', 'error')
        return render_template('perfil_clave.html'), 200

    try:
        crear(f"Usuario/{usuario_actual()['id_usuario']}/cambiar-clave",
              {'clave_actual': actual, 'clave_nueva': nueva})
    except ErrorApi as ex:
        if ex.codigo == 401:
            flash('La clave actual no es correcta.', 'error')
        elif ex.codigo is None:
            flash('No se pudo contactar el servicio. Intenta de nuevo en un momento.', 'error')
        else:
            flash((ex.detalle or ex.mensaje or 'No se pudo cambiar la clave.').strip(), 'error')
        return render_template('perfil_clave.html', errores=ex.errores), 200

    flash('Tu clave se actualizó.', 'exito')
    return redirect(url_for(endpoint_de_inicio()))


# --------------------------------------------------------------------------
#  2) Recuperar una clave olvidada (público)
# --------------------------------------------------------------------------
@perfil_bp.route('/recuperar', methods=['GET', 'POST'])
def recuperar():
    if request.method == 'GET':
        return render_template('recuperar_solicitar.html', identificador='')

    identificador = request.form.get('identificador', '').strip()
    if not identificador:
        flash('Escribe tu usuario o tu correo.', 'error')
        return render_template('recuperar_solicitar.html', identificador='')

    try:
        respuesta = crear('Usuario/recuperacion/solicitar', {
            'identificador': identificador,
            'ip': _ip_cliente(),
        })
    except ErrorApi as ex:
        flash('No se pudo procesar la solicitud. Intenta de nuevo en un momento.', 'error')
        registro.error('Recuperación: la API respondió %s', ex.codigo)
        return render_template('recuperar_solicitar.html', identificador=identificador)

    id_codigo = _ID_CODIGO_INEXISTENTE
    if respuesta.get('estado') == 'solicitada':
        try:
            correo.enviar_codigo_recuperacion(respuesta['enviado_a'], respuesta['usuario'],
                                              respuesta['codigo'], MINUTOS_VIGENCIA)
        except (correo.CorreoNoConfigurado, correo.ErrorEnvio) as ex:
            # Esta rama SÍ distingue "hay cuenta" de "no hay": no se puede evitar,
            # el envío falló de verdad y la persona debe saberlo (y avisar al
            # administrador). Por eso se muestra sin detalle técnico.
            registro.error('Recuperación: no salió el correo: %s', ex)
            flash('No pudimos enviar el correo con el código. Intenta más tarde o avisa al administrador.', 'error')
            return render_template('recuperar_solicitar.html', identificador=identificador)
        id_codigo = respuesta['id_codigo']

    session['recuperacion'] = {'id_codigo': id_codigo}
    flash('Si existe una cuenta con esos datos, te enviamos un código al correo registrado.', 'exito')
    return redirect(url_for('perfil.recuperar_codigo'))


@perfil_bp.route('/recuperar/codigo', methods=['GET', 'POST'])
def recuperar_codigo():
    pendiente = session.get('recuperacion')
    if not pendiente:
        return redirect(url_for('perfil.recuperar'))

    if request.method == 'GET':
        return render_template('recuperar_restablecer.html')

    codigo = request.form.get('codigo', '').strip()
    nueva = request.form.get('clave_nueva', '')
    confirmacion = request.form.get('clave_confirmacion', '')

    if not codigo:
        flash('Escribe el código que recibiste por correo.', 'error')
        return render_template('recuperar_restablecer.html')
    error = _validar_claves_nuevas(nueva, confirmacion)
    if error:
        flash(error, 'error')
        return render_template('recuperar_restablecer.html')

    try:
        crear('Usuario/recuperacion/restablecer', {
            'id_codigo': pendiente['id_codigo'],
            'codigo': codigo,
            'clave_nueva': nueva,
        })
    except ErrorApi as ex:
        if ex.codigo is None:
            flash('No se pudo contactar el servicio. Intenta de nuevo en un momento.', 'error')
        else:
            flash(MENSAJE_CODIGO, 'error')       # genérico: ver la cabecera del archivo
        return render_template('recuperar_restablecer.html')

    session.pop('recuperacion', None)
    flash('Listo: tu clave se actualizó. Ya puedes iniciar sesión.', 'exito')
    return redirect(url_for('auth.login'))
