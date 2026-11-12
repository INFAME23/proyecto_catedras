# ==========================================================================
#  routes/admin_solicitudes.py  ·  Front Cátedras · Subversión v7.1 · D56 · D58
# --------------------------------------------------------------------------
#  BANDEJA DE ENTRADA del administrador: las solicitudes «Soy ponente» que
#  llegan del calendario público (routes/calendario.py).
#
#    GET  /admin/solicitudes-ponente?estado=   lista (pendientes por defecto)
#    GET  /admin/solicitudes-ponente/<id>      la solicitud completa + la decisión
#    POST /admin/solicitudes-ponente/<id>/resolver
#                                              aprobar o rechazar (una sola vez)
#
#  v7.1 · D58: el ponente propone una CÁTEDRA NUEVA con fecha y hora. Al APROBAR,
#  la cátedra se crea sola (con su sesión y su QR). Lo que el ponente no elige y la
#  cátedra necesita —tipo de evento, modalidad, sede, periodo y salón— lo escoge el
#  administrador en la misma pantalla de decisión, con valores sugeridos (el nombre
#  es el título de la ponencia, el código ASIS se asigna solo). La modalidad, la sede
#  y el periodo los elige el PONENTE en el formulario: aquí vienen ya elegidos (el
#  administrador los puede cambiar; solo en solicitudes anteriores se sugiere el periodo
#  que cubre la fecha). Antes de aprobar se revisa que el horario no se cruce con otra
#  sesión en la misma sede y salón (D52; si no se puede consultar, no se aprueba);
#  los periodos y horarios de programación (D57) los aplica la base de datos.
#
#  Las rutas se cuelgan de 'admin_bp' (routes/admin.py), así heredan su guardia
#  de rol ADMIN (before_request) y la convención 'admin.<recurso>_<acción>'.
#  Aprobar es UNA llamada a la API: la base de datos crea/reutiliza a la persona
#  y su documento, crea la cátedra y asigna al ponente, todo en una transacción.
#  Rechazar exige el motivo.
# ==========================================================================

from flask import abort, flash, redirect, render_template, request, url_for

from auth_helpers import usuario_actual
from servicios.api_catedras import ErrorApi, actualizar_parcial, listar, obtener
from servicios.fechas import a_local, desde_input
from servicios.qr_sesiones import asegurar_qr_de_catedra

# Pestañas de la bandeja: (valor de '?estado=', texto). 'todas' no filtra.
PESTANAS = [('PENDIENTE', 'Pendientes'), ('APROBADA', 'Aprobadas'), ('RECHAZADA', 'Rechazadas'), ('todas', 'Todas')]

# Cuántas solicitudes se traen para la bandeja (la API las entrega con las pendientes primero).
LIMITE_BANDEJA = 500

# Campos de la cátedra nueva que llegan del formulario de decisión.
CAMPOS_CATEDRA = ['nombre_catedra', 'fk_tipo_evento', 'id_evento_asis', 'fk_modalidad', 'fk_sede', 'fk_periodo', 'lugar',
                  'enlace_virtual']

TIPO_EVENTO_POR_DEFECTO = 'CAAB'      # «Cátedra abierta»
MODALIDAD_POR_DEFECTO = 'PRESENCIAL'


def contar_pendientes():
    """Cuántas solicitudes esperan decisión (para el aviso del panel). None si la API no responde."""
    try:
        return len(listar('SolicitudPonente', limite=LIMITE_BANDEJA, estado='PENDIENTE'))
    except ErrorApi:
        return None


def _avisar(ex):
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def _lista(recurso):
    """Un catálogo para un <select>; si la API falla, se avisa y queda vacío."""
    try:
        return listar(recurso)
    except ErrorApi as ex:
        _avisar(ex)
        return []


def _periodo_de(fecha_iso, periodos):
    """El periodo académico activo que cubre esa fecha ('AAAA-MM-DD...'), o None."""
    dia = (fecha_iso or '')[:10]
    for p in periodos:
        if p.get('activo', True) and p['fecha_ini'] <= dia <= p['fecha_fin']:
            return p['id_periodo'].strip()
    return None


def _valores_por_defecto(s, opciones):
    """Los datos sugeridos de la cátedra que se va a crear (el administrador los puede cambiar)."""
    inicio_local = a_local(s.get('inicio_propuesto'))
    sedes = opciones['sedes']
    return {
        'nombre_catedra': s['titulo_ponencia'],
        'fk_tipo_evento': TIPO_EVENTO_POR_DEFECTO,
        'id_evento_asis': '',
        # Lo que eligió el ponente; si es una solicitud anterior (sin esos datos), valores sugeridos.
        'fk_modalidad': s.get('fk_modalidad') or MODALIDAD_POR_DEFECTO,
        'fk_sede': s.get('fk_sede') or (sedes[0]['id_sede'] if len(sedes) == 1 else ''),     # con una sola sede, ya está elegida
        'fk_periodo': ((s.get('fk_periodo') or '').strip()
                       or _periodo_de(inicio_local.date().isoformat() if inicio_local else '', opciones['periodos']) or ''),
        'lugar': '',
        'enlace_virtual': '',
        'inicio': inicio_local.strftime('%Y-%m-%dT%H:%M') if inicio_local else '',
        'fin': (a_local(s.get('fin_propuesto')).strftime('%Y-%m-%dT%H:%M') if s.get('fin_propuesto') else ''),
    }


def _render_detalle(s, valores=None, avisos=(), estado=200):
    opciones = {}
    crea_catedra = s['estado'] == 'PENDIENTE' and bool(s.get('inicio_propuesto'))
    if crea_catedra:
        opciones = {'tipos_evento': _lista('TipoEvento'), 'modalidades': _lista('Modalidad'),
                    'sedes': [x for x in _lista('Sede') if x.get('activo', True)], 'periodos': _lista('PeriodoAcademico')}
        valores = valores or _valores_por_defecto(s, opciones)
    return render_template('admin/solicitud_ponente_detalle.html', s=s, crea_catedra=crea_catedra, opciones=opciones,
                           valores=valores or {}, avisos=list(avisos)), estado


def registrar_rutas(admin_bp):
    """Agrega las rutas de la bandeja a 'admin_bp'."""

    @admin_bp.route('/solicitudes-ponente')
    def solicitudes_ponente_listar():
        estado = request.args.get('estado', 'PENDIENTE').strip()
        if estado not in dict(PESTANAS):
            estado = 'PENDIENTE'

        error = None
        todas = []
        try:
            todas = listar('SolicitudPonente', limite=LIMITE_BANDEJA)
        except ErrorApi as ex:
            error = ex

        conteo = {valor: sum(1 for s in todas if s['estado'] == valor) for valor, _ in PESTANAS if valor != 'todas'}
        conteo['todas'] = len(todas)
        solicitudes = todas if estado == 'todas' else [s for s in todas if s['estado'] == estado]

        return render_template('admin/solicitud_ponente_lista.html', solicitudes=solicitudes, estado=estado,
                               pestanas=PESTANAS, conteo=conteo, error=error), (502 if error else 200)

    def _solicitud_o_redirigir(id_solicitud):
        try:
            return obtener('SolicitudPonente', id_solicitud), None
        except ErrorApi as ex:
            if ex.codigo == 404:
                abort(404)
            _avisar(ex)
            return None, redirect(url_for('admin.solicitudes_ponente_listar'))

    @admin_bp.route('/solicitudes-ponente/<int:id_solicitud>')
    def solicitudes_ponente_detalle(id_solicitud):
        solicitud, salida = _solicitud_o_redirigir(id_solicitud)
        return salida or _render_detalle(solicitud)

    @admin_bp.route('/solicitudes-ponente/<int:id_solicitud>/resolver', methods=['POST'])
    def solicitudes_ponente_resolver(id_solicitud):
        decision = request.form.get('decision', '')
        if decision not in ('aprobar', 'rechazar'):
            flash("Elige «Aprobar» o «Rechazar».", 'error')
            return redirect(url_for('admin.solicitudes_ponente_detalle', id_solicitud=id_solicitud))

        observacion = request.form.get('observacion', '').strip()
        if decision == 'rechazar' and not observacion:
            flash('Para rechazar la solicitud escribe el motivo: es lo que se le dirá a la persona.', 'error')
            return redirect(url_for('admin.solicitudes_ponente_detalle', id_solicitud=id_solicitud))

        usuario = usuario_actual() or {}
        datos = {'aprobar': decision == 'aprobar', 'observacion': observacion or None,
                 'resuelto_por': usuario.get('id_usuario')}

        if decision == 'aprobar':
            solicitud, salida = _solicitud_o_redirigir(id_solicitud)
            if salida:
                return salida
            if solicitud.get('inicio_propuesto'):
                # D58: aprobar = crear la cátedra. Datos del formulario de decisión (vacío = que decida la base de datos).
                valores = {c: request.form.get(c, '').strip() for c in CAMPOS_CATEDRA}
                valores['inicio'] = request.form.get('inicio', '').strip()
                valores['fin'] = request.form.get('fin', '').strip()
                datos.update({c: valores[c] or None for c in CAMPOS_CATEDRA})
                datos['inicio'] = desde_input(valores['inicio'])
                datos['fin'] = desde_input(valores['fin'])

                # Que el horario no se cruce con otra sesión en la misma sede y salón (D52). Falla cerrado.
                from routes import admin as admin_rutas     # (import tardío: admin.py importa este módulo)
                inicio = datos['inicio'] or solicitud['inicio_propuesto']
                fin = datos['fin'] or solicitud['fin_propuesto']
                if inicio and fin:
                    try:
                        choques = admin_rutas._conflictos_de_horario(inicio, fin, valores['fk_sede'], valores['lugar'])
                    except ErrorApi as ex:
                        return _render_detalle(solicitud, valores, [
                            'No se pudo verificar la disponibilidad del horario, así que no se aprobó nada. '
                            f"Intenta de nuevo en un momento. {ex.detalle or ''}".strip()], 502)
                    if choques:
                        return _render_detalle(solicitud, valores, [
                            f'No se aprobó: el horario choca con {admin_rutas._describe_choque(c)}.' for c in choques] + [
                            'Cambia la fecha o la hora (debe haber al menos una hora entre una cátedra y otra) y vuelve a aprobar.'], 409)

        try:
            respuesta = actualizar_parcial('SolicitudPonente', f'{id_solicitud}/resolver', datos)
        except ErrorApi as ex:
            # 409: ya la resolvió otra persona, la fecha ya pasó o no cabe en los periodos; 400/422: dato inválido; 500: FK. Se muestra tal cual.
            _avisar(ex)
            return redirect(url_for('admin.solicitudes_ponente_detalle', id_solicitud=id_solicitud))

        flash(respuesta.get('mensaje', 'Solicitud resuelta.'), 'exito')
        resuelta = respuesta.get('solicitud') or {}
        if decision == 'aprobar' and resuelta.get('fk_catedra'):
            # v7.1 · D53: cada sesión nace con su QR de cierre (se ve en «QR de asistencia» de la sesión).
            hechos, total = asegurar_qr_de_catedra(resuelta['fk_catedra'], usuario.get('id_usuario'))
            if hechos != total:
                flash(f'Se generó el QR de {hechos} de {total} sesiones.', 'error')
        return redirect(url_for('admin.solicitudes_ponente_detalle', id_solicitud=id_solicitud))
