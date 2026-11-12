# ==========================================================================
#  routes/calendario.py  ·  Front Cátedras · Subversión v7.1 · D56
# --------------------------------------------------------------------------
#  Calendario PÚBLICO de las cátedras (sus sesiones) y el formulario «Soy
#  ponente».
#
#    GET  /calendario            la página (cuadrícula mensual, semana y lista).
#    GET  /calendario/eventos    JSON de sesiones para pintar el calendario.
#    POST /calendario/ponente    la persona envía su postulación -> la solicitud
#                                llega a la bandeja del ADMIN (/admin/solicitudes-ponente).
#
#  Las sesiones salen de GET /api/Disponibilidad/agenda (v7.1, D52), que ya trae
#  el nombre de la cátedra y de la sede en UNA llamada por rango. Esa consulta
#  acepta como máximo 31 días: un mes con sus semanas de borde son hasta 42,
#  así que el rango se parte en tramos de 30 días y se juntan.
#
#  Horas: FullCalendar se configura en 'UTC' y aquí se le entregan las horas
#  como "reloj de pared" de Colombia sin zona ('2026-11-05T14:00:00'), así cada
#  persona ve la hora de la sesión sin importar la zona de su navegador. El
#  rango que pide FullCalendar se lee igual: como hora de Colombia.
#
#  v7.1 · D58: el ponente propone una CÁTEDRA NUEVA y elige la fecha y la hora ('inicio' y 'fin', hora de
#  Colombia). Si el administrador aprueba la solicitud, la cátedra se crea sola con esa fecha y hora. Ya no se
#  elige una sesión que existe. El aviso de los periodos en que se pueden programar cátedras (D57) sale de
#  routes/admin_restricciones.py; que la fecha sea futura y quepa lo decide la base de datos al enviar (409).
#  También elige la modalidad, la sede y el periodo académico (<select> del catálogo, Regla 2); la fecha debe caer
#  dentro del periodo (la base de datos lo exige y calendario.js elige solo el periodo que cubre la fecha).
#  El administrador puede cambiar todo eso al aprobar.
#
#  El formulario no exige sesión (D55: solo el ADMIN inicia sesión). Antispam:
#  un campo trampa ('sitio_web') que una persona no ve y un robot sí llena.
#  Las reglas de los datos las aplica la API (422 con la lista de campos); aquí
#  solo se muestran junto a cada campo.
# ==========================================================================

from datetime import datetime, timedelta, timezone

from flask import Blueprint, flash, jsonify, redirect, render_template, request, url_for

from servicios.api_catedras import ErrorApi, consultar, crear, listar
from servicios.fechas import ZONA_COLOMBIA, a_local, desde_input
from routes.admin_restricciones import ventanas_activas   # v7.1 · D57: periodos y horarios de programación

calendario_bp = Blueprint('calendario', __name__)

# Tipos de documento que ofrece el formulario (cédula, pasaporte, licencia de
# conducción y código institucional), en este orden. Los nombres salen del
# catálogo 'tipo_documento' (Regla 2: la FK se elige de un <select>).
TIPOS_DOCUMENTO_PONENTE = ['CC', 'PA', 'LC', 'CI']

# Campos del formulario (los mismos nombres que recibe la API).
CAMPOS_PONENTE = ['nombres', 'apellidos', 'fk_tipo_documento', 'numero_documento', 'perfil_profesional',
                  'direccion', 'telefono', 'correo_institucional', 'titulo_ponencia', 'resumen_ponencia',
                  'inicio', 'fin', 'fk_modalidad', 'fk_sede', 'fk_periodo']

# Color de cada modalidad en el calendario (los mismos de la leyenda en static/estilo.css).
COLOR_MODALIDAD = {'presencial': '#EF7D00', 'virtual': '#1D1D1B', 'telepresencial': '#2F6F8F', 'hibrida': '#8A4B00'}
COLOR_SIN_MODALIDAD = '#6B6B6B'

# La agenda de la API acepta rangos de hasta 31 días; se pide en tramos de 30.
TRAMO_DIAS = 30
# Tope del rango que se sirve en una sola petición del calendario (la vista de lista de FullCalendar es de un mes).
MAXIMO_DIAS_EVENTOS = 95
LIMITE_POR_TRAMO = 1000


def _hora_pared(texto):
    """'2026-11-01T00:00:00Z' | '...-05:00' | '2026-11-01' -> datetime en hora de Colombia, tomando los
    números del texto como hora de pared de Colombia (ver la nota de horas arriba). None si no se lee."""
    if not texto:
        return None
    try:
        momento = datetime.fromisoformat(texto.replace('Z', '+00:00'))
    except ValueError:
        return None
    return momento.replace(tzinfo=ZONA_COLOMBIA)


def sesiones_en_rango(desde, hasta):
    """Sesiones que tocan [desde, hasta) (datetimes con zona), de la agenda de la API, sin repetir.
    Un error de la API sube como ErrorApi."""
    por_id = {}
    tramo_ini = desde
    while tramo_ini < hasta:
        tramo_fin = min(tramo_ini + timedelta(days=TRAMO_DIAS), hasta)
        respuesta = consultar('Disponibilidad/agenda', desde=tramo_ini.isoformat(), hasta=tramo_fin.isoformat(),
                              limite=LIMITE_POR_TRAMO)
        for sesion in (respuesta or {}).get('datos', []):
            por_id[sesion['id_sesion']] = sesion
        tramo_ini = tramo_fin
    return sorted(por_id.values(), key=lambda s: (s['inicio'], s['id_sesion']))


def _como_evento(sesion, ahora):
    """Una sesión de la agenda -> evento de FullCalendar (horas de pared de Colombia, sin zona)."""
    inicio = a_local(sesion.get('inicio'))
    fin = a_local(sesion.get('fin'))
    if inicio is None or fin is None:
        return None
    modalidad = (sesion.get('fk_modalidad') or '').strip().lower()
    nombre = (sesion.get('nombre_catedra') or 'Cátedra').strip()
    return {
        'id': sesion['id_sesion'],
        'title': nombre,
        'start': inicio.strftime('%Y-%m-%dT%H:%M:%S'),
        'end': fin.strftime('%Y-%m-%dT%H:%M:%S'),
        'color': COLOR_MODALIDAD.get(modalidad, COLOR_SIN_MODALIDAD),
        'classNames': [f'mod-{modalidad}'] if modalidad else [],
        'extendedProps': {
            'id_sesion': sesion['id_sesion'],
            'fk_catedra': sesion.get('fk_catedra'),
            'numero_reunion': sesion.get('numero_reunion'),
            'sede': sesion.get('nombre_sede'),
            'lugar': sesion.get('lugar'),
            'modalidad': (sesion.get('fk_modalidad') or '').strip(),
            'estado': sesion.get('estado'),
            'futura': inicio > ahora,
            'url': url_for('sesiones.detalle', id_sesion=sesion['id_sesion']),
        },
    }


@calendario_bp.route('/calendario/eventos')
def eventos():
    desde = _hora_pared(request.args.get('desde', ''))
    hasta = _hora_pared(request.args.get('hasta', ''))
    if desde is None or hasta is None or hasta <= desde:
        return jsonify({'mensaje': "Los parámetros 'desde' y 'hasta' son obligatorios (fechas ISO, desde < hasta)."}), 400
    if hasta - desde > timedelta(days=MAXIMO_DIAS_EVENTOS):
        return jsonify({'mensaje': f'El rango no puede pasar de {MAXIMO_DIAS_EVENTOS} días.'}), 400

    try:
        sesiones = sesiones_en_rango(desde, hasta)
    except ErrorApi as ex:
        # RF93: el error se muestra, nunca se inventan sesiones.
        return jsonify({'mensaje': ex.mensaje, 'detalle': ex.detalle}), 502

    ahora = datetime.now(timezone.utc)
    lista = [e for e in (_como_evento(s, ahora) for s in sesiones) if e is not None]
    return jsonify(lista)


def _datos_del_formulario():
    """Lo que necesita la página para dibujar el formulario: los tipos de documento, las modalidades, las sedes y los
    periodos académicos que se pueden elegir y los periodos de programación (D57). Si una consulta falla, el formulario
    se muestra igual (con ese <select> vacío) y se avisa."""
    avisos = []
    opciones = {}

    tipos = []
    try:
        catalogo = {t['id_tipo_documento'].strip(): t['nombre'] for t in listar('TipoDocumento')}
        tipos = [{'id': i, 'nombre': catalogo[i]} for i in TIPOS_DOCUMENTO_PONENTE if i in catalogo]
    except ErrorApi as ex:
        avisos.append(f'No se pudo cargar la lista de tipos de documento: {ex.mensaje}')

    hoy = datetime.now(ZONA_COLOMBIA).date().isoformat()
    for clave, recurso, nombre in (('modalidades', 'Modalidad', 'modalidades'), ('sedes', 'Sede', 'sedes'),
                                   ('periodos', 'PeriodoAcademico', 'periodos académicos')):
        try:
            filas = listar(recurso)
        except ErrorApi as ex:
            filas = []
            avisos.append(f'No se pudo cargar la lista de {nombre}: {ex.mensaje}')
        if clave == 'periodos':      # solo los activos que no han terminado
            filas = [p for p in filas if p.get('activo', True) and p['fecha_fin'] >= hoy]
        elif clave == 'sedes':       # solo las activas
            filas = [s for s in filas if s.get('activo', True)]
        opciones[clave] = filas

    return tipos, opciones, ventanas_activas(), avisos


def _pagina(valores=None, errores=None, error_general=None, abrir_formulario=False, estado=200):
    tipos, opciones, ventanas, avisos = _datos_del_formulario()
    return render_template('calendario.html', tipos_documento=tipos, opciones=opciones, ventanas=ventanas, avisos=avisos,
                           valores=valores or {}, errores=errores or {}, error_general=error_general,
                           abrir_formulario=abrir_formulario), estado


@calendario_bp.route('/calendario')
def calendario():
    return _pagina()


def _texto(campo):
    return request.form.get(campo, '').strip()


@calendario_bp.route('/calendario/ponente', methods=['POST'])
def enviar_ponente():
    valores = {campo: _texto(campo) for campo in CAMPOS_PONENTE}

    # Campo trampa: una persona no lo ve; un robot que llena todo, sí. Se responde como si todo hubiera salido bien.
    if _texto('sitio_web'):
        flash('Solicitud recibida.', 'exito')
        return redirect(url_for('calendario.calendario'))

    if not request.form.get('acepta_datos'):
        return _pagina(valores, {'acepta_datos': 'Hace falta autorizar el tratamiento de tus datos personales.'},
                       abrir_formulario=True, estado=422)

    datos = {campo: (valores[campo] or None) for campo in CAMPOS_PONENTE}
    # El <input type="datetime-local"> no trae zona ('2026-11-20T14:00'): se le pone la de Colombia.
    datos['inicio'] = desde_input(datos['inicio'])
    datos['fin'] = desde_input(datos['fin'])

    try:
        respuesta = crear('SolicitudPonente', datos)
    except ErrorApi as ex:
        # 422: la API dice qué campo falló -> se muestra junto a cada campo. Otro error: un aviso general.
        por_campo = {}
        for e in ex.errores:
            por_campo.setdefault(e.get('campo'), e.get('mensaje'))
        # 400 (fin antes del inicio...) y 409 (fecha pasada, fuera de periodo, ya hay una pendiente igual): el motivo va en un aviso.
        general = None if por_campo else f'{ex.mensaje} {ex.detalle or ""}'.strip()
        estado = 422 if por_campo else (ex.codigo if ex.codigo in (400, 409) else 502)
        return _pagina(valores, por_campo, error_general=general, abrir_formulario=True, estado=estado)

    flash(f"Recibimos tu solicitud (n.º {respuesta.get('id_solicitud')}). El equipo administrador revisará tu "
          "ponencia; si la aprueba, tu cátedra se crea en la fecha y la hora que elegiste y te contactará al correo "
          "institucional que registraste.", 'exito')
    return redirect(url_for('calendario.calendario'))
