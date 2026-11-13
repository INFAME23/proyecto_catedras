# ==========================================================================
#  routes/registro.py  ·  Front Cátedras · Versión 5 (rehecho en la v7.2 · RF-S9/RF-S10)
# --------------------------------------------------------------------------
#  Autorregistro de asistencia: pantalla PÚBLICA (sin login, fuera de /admin).
#
#  v7.2 · D60: ya NO hay clave de 6 dígitos por correo. Quien escanea el QR ve
#  el título de la cátedra y TRES opciones:
#
#    · Estudiante -> escribe su código de estudiante (carnet).
#    · Docente    -> cédula, nombre, contacto (correo o celular) y facultad.
#    · Externo    -> cédula, nombre, contacto y de dónde viene.
#
#  Rutas:
#    (ya NO hay pantalla en /registro para pegar el enlace: solo se entra con el QR o el enlace de la sesión)
#    GET  /registro/<token>               las 3 opciones (con ?tipo=... el formulario)
#    POST /registro/<token>/<tipo>        registrar (tipo = estudiante | docente | externo)
#    GET  /registro/<token>/ubicacion     JSON del AVISO TEMPRANO: ¿estás dentro del radio de la sede?
#
#  UBICACIÓN POR CANAL (D61): solo si el enlace es de canal QR (sesión presencial)
#  se pide y se valida; en sesiones virtuales (canal ENLACE) no se pide ni se
#  envía a la API. El canal NO se toma del formulario: se vuelve a leer de la API
#  en cada envío (el navegador no es de fiar). Se verifica en DOS momentos:
#    1. Aviso temprano: apenas el navegador entrega la ubicación, static/geolocalizacion.js
#       consulta /registro/<token>/ubicacion y avisa ANTES de que la persona escriba nada.
#    2. Verificación definitiva: dentro del procedimiento almacenado que registra
#       (la BD no confía en la API ni la API en el front).
#
#  Los rechazos de negocio (fuera del radio, ya registrado, enlace cerrado...) llegan
#  de la API como un 409 con un 'estado' (código) y un 'mensaje' listo para mostrar.
#
#  La IP que se guarda es la de request.remote_addr. Detrás de un proxy (Render) es la
#  del PROXY salvo que se configure PROXY_CONFIABLES (app.py, ProxyFix).
# ==========================================================================

import ipaddress
import math
import re

from flask import Blueprint, abort, flash, jsonify, redirect, render_template, request, session, url_for

from servicios.api_catedras import ErrorApi, consultar, crear, listar, obtener

registro_bp = Blueprint('registro', __name__)

TIPOS = ('estudiante', 'docente', 'externo')

# Un token es un UUID: 8-4-4-4-12 caracteres hexadecimales.
_UUID = re.compile(r'[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}')


def _ip_cliente():
    """IP de quien hace la petición, o None si no es una IP válida.
    La API responde 400 si recibe una IP mal formada, así que se valida antes."""
    try:
        return str(ipaddress.ip_address(request.remote_addr))
    except (TypeError, ValueError):
        return None


def _a_numero(texto):
    """Texto -> número finito, o None. Acepta coma decimal."""
    try:
        valor = float(str(texto).replace(',', '.'))
    except ValueError:
        return None
    return valor if math.isfinite(valor) else None


def _ubicacion(latitud, longitud):
    """(latitud, longitud) como números, o None si faltan, no son números finitos o están
    fuera de rango. La API y la BD las vuelven a validar; aquí solo se evita viajar con basura."""
    lat, lon = _a_numero(latitud), _a_numero(longitud)
    if lat is None or lon is None:
        return None
    if not (-90 <= lat <= 90 and -180 <= lon <= 180):
        return None
    return lat, lon


def _motivo(ex):
    """Texto para mostrar de un error de la API: el mensaje listo para la persona; si la API no respondió,
    un aviso genérico."""
    if ex.codigo is None:          # no hubo respuesta: la API está caída o inalcanzable
        return 'No se pudo contactar el servicio de registro. Intenta de nuevo en un momento.'
    return (ex.mensaje or ex.detalle or 'No se pudo completar la operación.').strip()


def _token_o_404(token):
    if not _UUID.fullmatch(token):
        abort(404)
    return token.lower()


def _enlace(token):
    """Datos del enlace (título, canal, ventana...) o None si el token no existe.
    Cualquier otro fallo de la API sube como ErrorApi."""
    try:
        return consultar(f'AsistenciaPublica/enlace/{token}')
    except ErrorApi as ex:
        if ex.codigo == 404:
            return None
        raise


def _facultades():
    """Facultades para el <select> del docente (Regla 2). Si la API no responde, [] y el formulario avisa."""
    try:
        return listar('Facultad', limite=1000)
    except ErrorApi:
        return []


def _enlace_invalido(mensaje):
    """Enlace o QR que no sirve: la página 404 con el motivo (ya no hay pantalla para pegar enlaces)."""
    flash(mensaje, 'error')
    return render_template('errors/404.html'), 404


def _pantalla(token, enlace, tipo=None, valores=None, errores=None, estado=None):
    """La página del enlace: las 3 opciones (tipo=None) o el formulario de UN tipo."""
    return render_template('registro_opciones.html', token=token, enlace=enlace, tipo=tipo,
                           valores=valores or {}, errores=errores or [], estado=estado,
                           facultades=_facultades() if tipo == 'docente' else [],
                           abierto=enlace.get('estado_ventana') == 'abierto')


# El nombre del endpoint ('registro.solicitar') se conserva: lo usan el QR (servicios/qr_sesiones.py) y
# las pantallas del administrador para armar el enlace público.
@registro_bp.route('/registro/<token>', methods=['GET'])
def solicitar(token):
    """Las 3 opciones tras escanear el QR; con ?tipo=estudiante|docente|externo, el formulario."""
    token = _token_o_404(token)
    try:
        enlace = _enlace(token)
    except ErrorApi as ex:
        return _enlace_invalido(_motivo(ex))
    if enlace is None:
        return _enlace_invalido('Este enlace de registro no existe. Escanea de nuevo el código QR de la sesión.')

    tipo = request.args.get('tipo')
    return _pantalla(token, enlace, tipo if tipo in TIPOS else None)


def _campos_del_formulario(tipo):
    """Lo que escribió la persona, ya sin espacios sobrantes (para volver a pintarlo si algo falla)."""
    nombres = {'estudiante': ('carnet',),
               'docente': ('cedula', 'nombre', 'contacto', 'fk_facultad'),
               'externo': ('cedula', 'nombre', 'contacto', 'procedencia')}[tipo]
    return {c: request.form.get(c, '').strip() for c in nombres}


@registro_bp.route('/registro/<token>/<tipo>', methods=['POST'])
def registrar(token, tipo):
    """Registra la asistencia de un estudiante, un docente o un externo."""
    token = _token_o_404(token)
    if tipo not in TIPOS:
        abort(404)

    try:
        enlace = _enlace(token)
    except ErrorApi as ex:
        return _enlace_invalido(_motivo(ex))
    if enlace is None:
        return _enlace_invalido('Este enlace de registro no existe. Escanea de nuevo el código QR de la sesión.')

    valores = _campos_del_formulario(tipo)
    datos = {'token': token, 'ip': _ip_cliente(),
             'user_agent': (request.headers.get('User-Agent') or '')[:300] or None, **valores}

    # D61: la ubicación solo se pide (y solo se manda) si el enlace es de canal QR. El canal sale de la API,
    # no del formulario. Sin ubicación válida NO se llama a la API (segunda barrera después del JS).
    if enlace.get('requiere_ubicacion'):
        ubicacion = _ubicacion(request.form.get('latitud', ''), request.form.get('longitud', ''))
        if ubicacion is None:
            flash('Falta tu ubicación: es obligatoria en una sesión presencial. '
                  'Permite el acceso a la ubicación en el navegador y vuelve a intentarlo.', 'error')
            return _pantalla(token, enlace, tipo, valores)
        datos['latitud'], datos['longitud'] = ubicacion

    try:
        respuesta = crear(f'AsistenciaPublica/{tipo}', datos)
    except ErrorApi as ex:
        if ex.estado == 'YA_REGISTRADO':
            # No es un error de la persona: ya estaba registrada. Mensaje amistoso, sin 500 ni alarma.
            return render_template('registro_listo.html', token=token, enlace=enlace, ya_registrado=True,
                                   id_sesion_encuesta=None)
        flash(_motivo(ex), 'error')
        return _pantalla(token, enlace, tipo, valores, errores=ex.errores, estado=ex.estado)

    # v7.1 · D55: sin cuentas, la encuesta de la sesión se responde justo ahora, con este registro
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

    return render_template('registro_listo.html', token=token, enlace=enlace, ya_registrado=False,
                           id_sesion_encuesta=id_sesion_encuesta)


@registro_bp.route('/registro/<token>/ubicacion', methods=['GET'])
def ubicacion(token):
    """AVISO TEMPRANO (JSON): apenas el navegador entrega la ubicación, static/geolocalizacion.js
    pregunta aquí si está dentro del radio de la sede. No guarda nada. Responde siempre JSON
    {estado, mensaje, dentro, distancia_metros, radio_metros}; 'estado' es el código de la API."""
    token = _token_o_404(token)
    ubic = _ubicacion(request.args.get('latitud', ''), request.args.get('longitud', ''))
    if ubic is None:
        return jsonify(estado='DATOS_INVALIDOS', mensaje='La ubicación recibida no es válida.', dentro=None), 400

    try:
        r = consultar('AsistenciaPublica/ubicacion', token=token, latitud=ubic[0], longitud=ubic[1])
    except ErrorApi as ex:
        # 404 enlace inexistente; 409 revocado / fuera de ventana / sede sin coordenadas; sin código = API caída.
        estado = ex.estado or 'ERROR'
        return jsonify(estado=estado, mensaje=_motivo(ex), dentro=None), (ex.codigo or 502)

    return jsonify(estado=r.get('estado'), mensaje=r.get('mensaje'), dentro=r.get('dentro'),
                   distancia_metros=r.get('distancia_metros'), radio_metros=r.get('radio_metros'),
                   requiere_ubicacion=r.get('requiere_ubicacion'))
