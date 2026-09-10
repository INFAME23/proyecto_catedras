# ==========================================================================
#  routes/admin.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Panel de administración: CRUD de las 4 tablas catálogo que ya existen en
#  ApiCatedras (Sede, Encuesta, EstadoProceso, Modalidad). Cada sección usa
#  formularios HTML normales (los navegadores no mandan PUT/DELETE, así que
#  el botón "Guardar" hace POST y esta ruta traduce a PUT/DELETE contra la
#  API con 'requests').
#
#  Este blueprint NO reimplementa reglas de negocio (límite > 0, fechas
#  coherentes, es_exitoso->es_final, canal_difusion en una lista cerrada):
#  esas las aplica ApiCatedras. Aquí solo se capturan sus errores
#  (ErrorApi) y se muestran en la plantilla.
# ==========================================================================

from flask import Blueprint, flash, redirect, render_template, request, url_for

from servicios.api_catedras import ErrorApi, crear, eliminar, listar, obtener, reemplazar

admin_bp = Blueprint('admin', __name__)


@admin_bp.route('/')
def panel():
    return render_template('admin/panel.html')


# ==========================================================================
#  SEDE  (recurso API: "Sede", clave: id_sede)
# ==========================================================================

@admin_bp.route('/sedes')
def sedes_listar():
    try:
        sedes = listar('Sede')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        sedes = []
    return render_template('admin/sede_lista.html', sedes=sedes)


@admin_bp.route('/sedes/nueva', methods=['GET', 'POST'])
def sedes_nueva():
    if request.method == 'GET':
        return render_template('admin/sede_form.html', modo='nueva', sede=None, errores=[])

    datos = {
        'id_sede': request.form.get('id_sede', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'direccion': request.form.get('direccion', '').strip() or None,
        'es_virtual': request.form.get('es_virtual') == 'on',
        'activo': request.form.get('activo') == 'on',
    }
    try:
        crear('Sede', datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/sede_form.html', modo='nueva', sede=datos, errores=ex.errores)

    flash(f"Sede '{datos['id_sede']}' creada.", 'exito')
    return redirect(url_for('admin.sedes_listar'))


@admin_bp.route('/sedes/<id_sede>/editar', methods=['GET', 'POST'])
def sedes_editar(id_sede):
    if request.method == 'GET':
        try:
            sede = obtener('Sede', id_sede)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.sedes_listar'))
        return render_template('admin/sede_form.html', modo='editar', sede=sede, errores=[])

    datos = {
        'nombre': request.form.get('nombre', '').strip(),
        'direccion': request.form.get('direccion', '').strip() or None,
        'es_virtual': request.form.get('es_virtual') == 'on',
        'activo': request.form.get('activo') == 'on',
    }
    try:
        reemplazar('Sede', id_sede, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_sede'] = id_sede
        return render_template('admin/sede_form.html', modo='editar', sede=datos, errores=ex.errores)

    flash(f"Sede '{id_sede}' actualizada.", 'exito')
    return redirect(url_for('admin.sedes_listar'))


@admin_bp.route('/sedes/<id_sede>/eliminar', methods=['POST'])
def sedes_eliminar(id_sede):
    try:
        eliminar('Sede', id_sede)
        flash(f"Sede '{id_sede}' eliminada.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.sedes_listar'))


# ==========================================================================
#  MODALIDAD  (recurso API: "Modalidad", clave: id_modalidad)
# ==========================================================================

CANALES_DIFUSION = ['QR', 'ENLACE', 'AMBOS']


@admin_bp.route('/modalidades')
def modalidades_listar():
    try:
        modalidades = listar('Modalidad')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        modalidades = []
    return render_template('admin/modalidad_lista.html', modalidades=modalidades)


@admin_bp.route('/modalidades/nueva', methods=['GET', 'POST'])
def modalidades_nueva():
    if request.method == 'GET':
        return render_template('admin/modalidad_form.html', modo='nueva', modalidad=None, errores=[], canales=CANALES_DIFUSION)

    datos = {
        'id_modalidad': request.form.get('id_modalidad', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'canal_difusion': request.form.get('canal_difusion', ''),
        'activo': request.form.get('activo') == 'on',
    }
    try:
        crear('Modalidad', datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/modalidad_form.html', modo='nueva', modalidad=datos, errores=ex.errores, canales=CANALES_DIFUSION)

    flash(f"Modalidad '{datos['id_modalidad']}' creada.", 'exito')
    return redirect(url_for('admin.modalidades_listar'))


@admin_bp.route('/modalidades/<id_modalidad>/editar', methods=['GET', 'POST'])
def modalidades_editar(id_modalidad):
    if request.method == 'GET':
        try:
            modalidad = obtener('Modalidad', id_modalidad)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.modalidades_listar'))
        return render_template('admin/modalidad_form.html', modo='editar', modalidad=modalidad, errores=[], canales=CANALES_DIFUSION)

    datos = {
        'nombre': request.form.get('nombre', '').strip(),
        'canal_difusion': request.form.get('canal_difusion', ''),
        'activo': request.form.get('activo') == 'on',
    }
    try:
        reemplazar('Modalidad', id_modalidad, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_modalidad'] = id_modalidad
        return render_template('admin/modalidad_form.html', modo='editar', modalidad=datos, errores=ex.errores, canales=CANALES_DIFUSION)

    flash(f"Modalidad '{id_modalidad}' actualizada.", 'exito')
    return redirect(url_for('admin.modalidades_listar'))


@admin_bp.route('/modalidades/<id_modalidad>/eliminar', methods=['POST'])
def modalidades_eliminar(id_modalidad):
    try:
        eliminar('Modalidad', id_modalidad)
        flash(f"Modalidad '{id_modalidad}' eliminada.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.modalidades_listar'))


# ==========================================================================
#  ESTADO DE PROCESO  (recurso API: "EstadoProceso", clave: id_estado)
# ==========================================================================

@admin_bp.route('/estados-proceso')
def estados_listar():
    try:
        estados = listar('EstadoProceso')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        estados = []
    return render_template('admin/estado_proceso_lista.html', estados=estados)


@admin_bp.route('/estados-proceso/nuevo', methods=['GET', 'POST'])
def estados_nuevo():
    if request.method == 'GET':
        return render_template('admin/estado_proceso_form.html', modo='nueva', estado=None, errores=[])

    datos = {
        'id_estado': request.form.get('id_estado', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'es_final': request.form.get('es_final') == 'on',
        'es_exitoso': request.form.get('es_exitoso') == 'on',
    }
    try:
        crear('EstadoProceso', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, la regla "es_exitoso implica es_final" (400).
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/estado_proceso_form.html', modo='nueva', estado=datos, errores=ex.errores)

    flash(f"Estado de proceso '{datos['id_estado']}' creado.", 'exito')
    return redirect(url_for('admin.estados_listar'))


@admin_bp.route('/estados-proceso/<id_estado>/editar', methods=['GET', 'POST'])
def estados_editar(id_estado):
    if request.method == 'GET':
        try:
            estado = obtener('EstadoProceso', id_estado)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.estados_listar'))
        return render_template('admin/estado_proceso_form.html', modo='editar', estado=estado, errores=[])

    datos = {
        'nombre': request.form.get('nombre', '').strip(),
        'es_final': request.form.get('es_final') == 'on',
        'es_exitoso': request.form.get('es_exitoso') == 'on',
    }
    try:
        reemplazar('EstadoProceso', id_estado, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_estado'] = id_estado
        return render_template('admin/estado_proceso_form.html', modo='editar', estado=datos, errores=ex.errores)

    flash(f"Estado de proceso '{id_estado}' actualizado.", 'exito')
    return redirect(url_for('admin.estados_listar'))


@admin_bp.route('/estados-proceso/<id_estado>/eliminar', methods=['POST'])
def estados_eliminar(id_estado):
    try:
        eliminar('EstadoProceso', id_estado)
        flash(f"Estado de proceso '{id_estado}' eliminado.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.estados_listar'))


# ==========================================================================
#  ENCUESTA  (recurso API: "Encuesta", clave: id_encuesta -- la genera la BD)
# ==========================================================================

@admin_bp.route('/encuestas')
def encuestas_listar():
    try:
        encuestas = listar('Encuesta')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        encuestas = []
    return render_template('admin/encuesta_lista.html', encuestas=encuestas)


def _leer_form_encuesta():
    """Campos comunes a crear/editar. 'vigente_hasta' vacío -> None."""
    vigente_hasta = request.form.get('vigente_hasta', '').strip() or None
    version_texto = request.form.get('version', '').strip()
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'version': int(version_texto) if version_texto else None,
        'vigente_desde': request.form.get('vigente_desde', '').strip() or None,
        'vigente_hasta': vigente_hasta,
        'activa': request.form.get('activa') == 'on',
    }


@admin_bp.route('/encuestas/nueva', methods=['GET', 'POST'])
def encuestas_nueva():
    if request.method == 'GET':
        return render_template('admin/encuesta_form.html', modo='nueva', encuesta=None, errores=[])

    datos = _leer_form_encuesta()
    try:
        respuesta = crear('Encuesta', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, "vigente_hasta no puede ser anterior a vigente_desde" (400).
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/encuesta_form.html', modo='nueva', encuesta=datos, errores=ex.errores)

    flash(f"Encuesta '{datos['nombre']}' creada (id {respuesta['id_encuesta']}).", 'exito')
    return redirect(url_for('admin.encuestas_listar'))


@admin_bp.route('/encuestas/<int:id_encuesta>/editar', methods=['GET', 'POST'])
def encuestas_editar(id_encuesta):
    if request.method == 'GET':
        try:
            encuesta = obtener('Encuesta', id_encuesta)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.encuestas_listar'))
        return render_template('admin/encuesta_form.html', modo='editar', encuesta=encuesta, errores=[])

    datos = _leer_form_encuesta()
    try:
        reemplazar('Encuesta', id_encuesta, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_encuesta'] = id_encuesta
        return render_template('admin/encuesta_form.html', modo='editar', encuesta=datos, errores=ex.errores)

    flash(f"Encuesta '{id_encuesta}' actualizada.", 'exito')
    return redirect(url_for('admin.encuestas_listar'))


@admin_bp.route('/encuestas/<int:id_encuesta>/eliminar', methods=['POST'])
def encuestas_eliminar(id_encuesta):
    try:
        eliminar('Encuesta', id_encuesta)
        flash(f"Encuesta '{id_encuesta}' eliminada.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.encuestas_listar'))
