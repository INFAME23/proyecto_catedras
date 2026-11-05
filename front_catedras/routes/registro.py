# ==========================================================================
#  routes/registro.py  ·  Front Cátedras · Versión 5
# --------------------------------------------------------------------------
#  Autorregistro de asistencia: pantalla PÚBLICA (sin login, fuera de /admin).
#
#  Flujo de 2 pasos (RF149/RF150 de version5/2_spec.md):
#    1) GET/POST /registro/<token>          -> la persona escribe su
#       identificador; el front llama POST /api/ClaveAcceso/solicitar y
#       recibe una clave de 6 dígitos de un solo uso.
#    2) POST /registro/<token>/validar      -> la persona escribe esa clave;
#       el front llama POST /api/ClaveAcceso/validar y queda registrada.
#
#  RNF12: la clave viaja en texto plano UNA sola vez, en la respuesta de
#  'solicitar'. En producción llegaría por correo; aquí se MUESTRA en pantalla
#  para simular el envío. El front nunca la guarda: ni en sesión ni en disco.
#
#  v7.1 · RF-S1: 'validar' exige además la ubicación del navegador
#  (campos ocultos 'latitud' y 'longitud', que llena static/geolocalizacion.js).
#  Sin ella no se registra la asistencia; ver el comentario de ese archivo.
#
#  Las 5 razones por las que 'validar' puede fallar (clave incorrecta, usada,
#  expirada, sin vinculación vigente) llegan como un 500 de la
#  API (ADR D25); el texto de la razón viene en 'detalle' y se muestra tal cual.
# ==========================================================================

import ipaddress
import math
import re

from flask import Blueprint, abort, flash, redirect, render_template, request, session, url_for

from servicios import correo
from servicios.api_catedras import ErrorApi, crear, obtener

registro_bp = Blueprint('registro', __name__)

# Vigencia de la clave (el parámetro CLAVE_VIGENCIA_MINUTOS de la BD vale 10); solo se usa para el texto del correo.
CLAVE_VIGENCIA_MINUTOS = 10

# Un token es un UUID: 8-4-4-4-12 caracteres hexadecimales.
_UUID = re.compile(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')


def _ip_cliente():
    """IP de quien hace la petición, o None si no es una IP válida.
    La API responde 400 si recibe una IP mal formada, así que se valida antes."""
    try:
        return str(ipaddress.ip_address(request.remote_addr))
    except (TypeError, ValueError):
        return None


def _ubicacion_del_formulario():
    """(latitud, longitud) del formulario como números, o None si faltan,
    no son números finitos o están fuera de rango. La API y la BD las
    vuelven a validar; aquí solo se evita viajar con basura."""
    try:
        latitud = float(request.form.get('latitud', ''))
        longitud = float(request.form.get('longitud', ''))
    except ValueError:
        return None
    if not (math.isfinite(latitud) and math.isfinite(longitud)):
        return None
    if not (-90 <= latitud <= 90 and -180 <= longitud <= 180):
        return None
    return latitud, longitud


def _motivo(ex):
    """Texto para mostrar de un error de la API: la razón de negocio (detalle)
    si existe; si no, el mensaje general."""
    if ex.codigo is None:          # no hubo respuesta: la API está caída o inalcanzable
        return 'No se pudo contactar el servicio de registro. Intenta de nuevo en un momento.'
    return (ex.detalle or ex.mensaje or 'No se pudo completar la operación.').strip()


def _token_o_404(token):
    if not _UUID.fullmatch(token):
        abort(404)
    return token.lower()


@registro_bp.route('/registro', methods=['GET', 'POST'])
def inicio():
    """Pide pegar el enlace (o solo el token) que el organizador compartió."""
    if request.method == 'GET':
        return render_template('registro_inicio.html', texto='')

    texto = request.form.get('enlace', '').strip()
    encontrado = _UUID.search(texto)
    if not encontrado:
        flash('No encontramos un código válido en lo que pegaste. Copia el enlace completo del QR o del correo.', 'error')
        return render_template('registro_inicio.html', texto=texto)
    return redirect(url_for('registro.solicitar', token=encontrado.group(0).lower()))


@registro_bp.route('/registro/<token>', methods=['GET', 'POST'])
def solicitar(token):
    """Paso 1: la persona dice quién es y la API le genera una clave."""
    token = _token_o_404(token)

    if request.method == 'GET':
        return render_template('registro_solicitar.html', token=token,
                               identificador='')

    identificador = request.form.get('identificador', '').strip()
    try:
        respuesta = crear('ClaveAcceso/solicitar', {
            'identificador': identificador,
            'token': token,
            'ip': _ip_cliente(),
        })
    except ErrorApi as ex:
        # Identificador desconocido, enlace revocado o fuera de ventana (500),
        # o campo vacío (422).
        flash(_motivo(ex), 'error')
        return render_template('registro_solicitar.html', token=token,
                               identificador=identificador, errores=ex.errores)

    # v7.1 · D53: la clave existe para que NADIE registre a otro: tiene que llegar al correo de la persona.
    # Con el correo configurado se ENVÍA y no se muestra en pantalla. Sin correo configurado (desarrollo)
    # se muestra, como simulación del envío (RNF12: se ve UNA vez).
    clave_visible = respuesta['clave']
    if correo.configurado():
        try:
            correo.enviar_clave_asistencia(respuesta['enviado_a'], respuesta['clave'], CLAVE_VIGENCIA_MINUTOS)
            clave_visible = None
        except (correo.ErrorEnvio, correo.CorreoNoConfigurado) as ex:
            flash(f'{ex} Intenta de nuevo en un momento.', 'error')
            return render_template('registro_solicitar.html', token=token, identificador=identificador)

    return render_template('registro_validar.html', token=token,
                           id_clave=respuesta['id_clave'],
                           clave=clave_visible,
                           enviado_a=respuesta['enviado_a'],
                           enviada_por_correo=clave_visible is None,
                           registrado=False)


@registro_bp.route('/registro/<token>/validar', methods=['POST'])
def validar(token):
    """Paso 2: la persona escribe la clave recibida y queda registrada."""
    token = _token_o_404(token)

    try:
        id_clave = int(request.form.get('id_clave', ''))
    except ValueError:
        abort(400)
    clave = request.form.get('clave', '').strip()

    # v7.1 · RF-S1: la ubicación es obligatoria. static/geolocalizacion.js ya
    # bloquea el botón sin ella; esto es la segunda barrera (el navegador no es
    # de fiar). Se corta ANTES de llamar a la API para no gastar un intento
    # de la clave.
    ubicacion = _ubicacion_del_formulario()
    if ubicacion is None:
        flash('Falta tu ubicación: es obligatoria para registrar la asistencia. '
              'Permite el acceso a la ubicación en el navegador y vuelve a intentarlo.', 'error')
        return render_template('registro_validar.html', token=token, id_clave=id_clave,
                               clave=None, enviado_a=None, registrado=False)

    try:
        respuesta = crear('ClaveAcceso/validar', {
            'id_clave': id_clave,
            'clave': clave,
            'ip': _ip_cliente(),
            'user_agent': (request.headers.get('User-Agent') or '')[:300] or None,
            'latitud': ubicacion[0],
            'longitud': ubicacion[1],
        })
    except ErrorApi as ex:
        # Cualquiera de las 5 razones de rechazo. La clave original NO se
        # vuelve a mostrar: la persona ya la tiene.
        flash(_motivo(ex), 'error')
        return render_template('registro_validar.html', token=token, id_clave=id_clave,
                               clave=None, enviado_a=None, registrado=False,
                               errores=ex.errores)

    # v7.1 · D55: sin cuentas de estudiante, la encuesta de la sesión se responde justo ahora, con este registro
    # (queda en la cookie firmada; ver routes/encuestas.py). Si algo de esto falla, la asistencia ya quedó registrada.
    id_sesion_encuesta = None
    try:
        registro_creado = obtener('RegistroAsistencia', respuesta['id_registro'])
        sesion = obtener('Sesion', registro_creado['fk_sesion'])
        session['registro_reciente'] = {'registro': respuesta['id_registro'], 'sesion': sesion['id_sesion']}
        if sesion.get('fk_encuesta'):
            id_sesion_encuesta = sesion['id_sesion']
    except (ErrorApi, KeyError, TypeError):
        pass

    return render_template('registro_validar.html', token=token, id_clave=id_clave,
                           clave=None, enviado_a=None, registrado=True, id_sesion_encuesta=id_sesion_encuesta)
