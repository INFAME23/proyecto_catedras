# ==========================================================================
#  routes/admin_restricciones.py  ·  Front Cátedras · Subversión v7.1 · D57
# --------------------------------------------------------------------------
#  «Periodos y horarios de programación» (grupo «Estructura académica y
#  cátedras» del panel): el administrador define las VENTANAS en que se pueden
#  programar cátedras —fechas, horas y días de la semana— porque las cátedras se
#  dictan en periodos específicos.
#
#    GET  /admin/restricciones-programacion                 lista
#    GET/POST /admin/restricciones-programacion/nueva       crear
#    GET/POST /admin/restricciones-programacion/<id>/editar editar
#    POST /admin/restricciones-programacion/<id>/estado     activar / pausar
#    POST /admin/restricciones-programacion/<id>/eliminar   borrar
#
#  Sin ventanas ACTIVAS no hay restricción. Con alguna, la base de datos rechaza
#  toda sesión nueva (o con inicio/fin cambiado) que no quepa en una de ellas;
#  la API lo devuelve como 409 con el mensaje y los formularios de cátedra y de
#  sesión lo muestran. Aquí además se ofrece (a esos formularios):
#    · 'ventanas_activas()'      las ventanas vigentes, para el aviso de arriba;
#    · 'fuera_de_periodo(i, f)'  el texto de por qué un horario no cabe (o None),
#                                para el aviso en vivo de disponibilidad.js.
#  Las rutas cuelgan de 'admin_bp' (routes/admin.py): heredan su guardia de ADMIN.
# ==========================================================================

from datetime import datetime

from flask import abort, flash, redirect, render_template, request, url_for

from servicios.api_catedras import ErrorApi, consultar, crear, eliminar, listar, obtener, reemplazar
from servicios.fechas import ZONA_COLOMBIA

# (número ISO, nombre, abreviatura): 1 = lunes … 7 = domingo.
DIAS_SEMANA = [(1, 'Lunes', 'lun'), (2, 'Martes', 'mar'), (3, 'Miércoles', 'mié'), (4, 'Jueves', 'jue'),
               (5, 'Viernes', 'vie'), (6, 'Sábado', 'sáb'), (7, 'Domingo', 'dom')]

_ABREVIATURA = {n: abrev for n, _, abrev in DIAS_SEMANA}


def ventanas_activas():
    """Las ventanas ACTIVAS (para el aviso de los formularios de cátedra y de sesión). Si la API no responde, [] (el aviso es
    una ayuda; quien decide es la base de datos al guardar)."""
    try:
        return listar('RestriccionProgramacion', soloActivas='true')
    except ErrorApi:
        return []


def fuera_de_periodo(inicio, fin):
    """None si el horario cabe (o no hay restricción o no se pudo consultar); si no, el texto de por qué no.
    'inicio'/'fin': texto ISO con zona."""
    try:
        r = consultar('RestriccionProgramacion/validar', inicio=inicio, fin=fin)
    except ErrorApi:
        return None
    return None if r.get('permitido', True) else r.get('mensaje')


def _avisar(ex):
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def _hhmm(texto):
    """'06:00:00' -> '06:00' (la API pide HH:mm); vacío -> None."""
    return (texto or '').strip()[:5] or None


def _leer_formulario():
    dias = sorted({int(d) for d in request.form.getlist('dias_semana') if d.isdigit()})
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'fecha_desde': request.form.get('fecha_desde', '').strip() or None,
        'fecha_hasta': request.form.get('fecha_hasta', '').strip() or None,
        'hora_desde': _hhmm(request.form.get('hora_desde')),
        'hora_hasta': _hhmm(request.form.get('hora_hasta')),
        'dias_semana': dias or None,
        'activo': request.form.get('activo') == 'on',
    }


def _estado_por_fechas(v, hoy):
    """'vigente' | 'futura' | 'vencida' según la fecha de hoy en Colombia (solo informativo)."""
    if v['fecha_hasta'] < hoy:
        return 'vencida'
    if v['fecha_desde'] > hoy:
        return 'futura'
    return 'vigente'


def _render_formulario(modo, ventana, errores=None, id_restriccion=None):
    return render_template('admin/restriccion_form.html', modo=modo, v=ventana, errores=errores or {},
                           id_restriccion=id_restriccion, dias_semana=DIAS_SEMANA)


def _errores_por_campo(ex):
    por_campo = {}
    for e in ex.errores:
        por_campo.setdefault(e.get('campo'), e.get('mensaje'))
    return por_campo


def registrar_rutas(admin_bp):
    """Agrega las rutas de las ventanas de programación a 'admin_bp'."""

    @admin_bp.context_processor
    def _ayudas_de_programacion():
        # Las plantillas la llaman solo donde hace falta (formularios de cátedra y de sesión).
        return {'ventanas_programacion': ventanas_activas}

    @admin_bp.route('/restricciones-programacion')
    def restricciones_listar():
        error = None
        ventanas = []
        try:
            ventanas = listar('RestriccionProgramacion')
        except ErrorApi as ex:
            error = ex
        hoy = datetime.now(ZONA_COLOMBIA).date().isoformat()
        for v in ventanas:
            v['periodo'] = _estado_por_fechas(v, hoy)
        return render_template('admin/restriccion_lista.html', ventanas=ventanas, error=error,
                               abreviatura=_ABREVIATURA, hay_activas=any(v['activo'] for v in ventanas)), (502 if error else 200)

    @admin_bp.route('/restricciones-programacion/nueva', methods=['GET', 'POST'])
    def restricciones_nueva():
        if request.method == 'GET':
            return _render_formulario('nueva', {'activo': True})
        datos = _leer_formulario()
        try:
            respuesta = crear('RestriccionProgramacion', datos)
        except ErrorApi as ex:
            por_campo = _errores_por_campo(ex)
            if not por_campo:
                _avisar(ex)
            return _render_formulario('nueva', datos, por_campo), (422 if por_campo else 400 if ex.codigo == 400 else 502)
        flash(f"Ventana «{datos['nombre']}» creada (id {respuesta['id_restriccion']}).", 'exito')
        return redirect(url_for('admin.restricciones_listar'))

    @admin_bp.route('/restricciones-programacion/<int:id_restriccion>/editar', methods=['GET', 'POST'])
    def restricciones_editar(id_restriccion):
        if request.method == 'GET':
            try:
                ventana = obtener('RestriccionProgramacion', id_restriccion)
            except ErrorApi as ex:
                if ex.codigo == 404:
                    abort(404)
                _avisar(ex)
                return redirect(url_for('admin.restricciones_listar'))
            return _render_formulario('editar', ventana, id_restriccion=id_restriccion)

        datos = _leer_formulario()
        try:
            reemplazar('RestriccionProgramacion', id_restriccion, datos)
        except ErrorApi as ex:
            if ex.codigo == 404:
                abort(404)
            por_campo = _errores_por_campo(ex)
            if not por_campo:
                _avisar(ex)
            return _render_formulario('editar', datos, por_campo, id_restriccion), (422 if por_campo else 400 if ex.codigo == 400 else 502)
        flash(f"Ventana «{datos['nombre']}» actualizada.", 'exito')
        return redirect(url_for('admin.restricciones_listar'))

    @admin_bp.route('/restricciones-programacion/<int:id_restriccion>/estado', methods=['POST'])
    def restricciones_estado(id_restriccion):
        """Activar / pausar. La API reemplaza la fila completa (PUT): se lee, se cambia 'activo' y se guarda."""
        try:
            v = obtener('RestriccionProgramacion', id_restriccion)
            nuevo = not v['activo']
            reemplazar('RestriccionProgramacion', id_restriccion, {
                'nombre': v['nombre'], 'fecha_desde': v['fecha_desde'], 'fecha_hasta': v['fecha_hasta'],
                'hora_desde': _hhmm(v.get('hora_desde')), 'hora_hasta': _hhmm(v.get('hora_hasta')),
                'dias_semana': v.get('dias_semana'), 'activo': nuevo})
            flash(f"Ventana «{v['nombre']}» {'activada: ahora limita la programación' if nuevo else 'pausada: ya no limita'}.", 'exito')
        except ErrorApi as ex:
            _avisar(ex)
        return redirect(url_for('admin.restricciones_listar'))

    @admin_bp.route('/restricciones-programacion/<int:id_restriccion>/eliminar', methods=['POST'])
    def restricciones_eliminar(id_restriccion):
        try:
            eliminar('RestriccionProgramacion', id_restriccion)
            flash(f'Ventana {id_restriccion} eliminada. Las sesiones ya creadas no cambian.', 'exito')
        except ErrorApi as ex:
            _avisar(ex)
        return redirect(url_for('admin.restricciones_listar'))
