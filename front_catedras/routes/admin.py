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


# ==========================================================================
#  PERIODO ACADÉMICO  (recurso API: "PeriodoAcademico", clave: id_periodo)
# ==========================================================================

@admin_bp.route('/periodos')
def periodos_listar():
    try:
        periodos = listar('PeriodoAcademico')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        periodos = []
    return render_template('admin/periodo_lista.html', periodos=periodos)


@admin_bp.route('/periodos/nuevo', methods=['GET', 'POST'])
def periodos_nuevo():
    if request.method == 'GET':
        return render_template('admin/periodo_form.html', modo='nueva', periodo=None, errores=[])

    datos = {
        'id_periodo': request.form.get('id_periodo', '').strip(),
        'fecha_ini': request.form.get('fecha_ini', '').strip() or None,
        'fecha_fin': request.form.get('fecha_fin', '').strip() or None,
        'activo': request.form.get('activo') == 'on',
    }
    try:
        crear('PeriodoAcademico', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, "fecha_fin debe ser posterior a fecha_ini" (400).
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/periodo_form.html', modo='nueva', periodo=datos, errores=ex.errores)

    flash(f"Periodo académico '{datos['id_periodo']}' creado.", 'exito')
    return redirect(url_for('admin.periodos_listar'))


@admin_bp.route('/periodos/<id_periodo>/editar', methods=['GET', 'POST'])
def periodos_editar(id_periodo):
    if request.method == 'GET':
        try:
            periodo = obtener('PeriodoAcademico', id_periodo)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.periodos_listar'))
        return render_template('admin/periodo_form.html', modo='editar', periodo=periodo, errores=[])

    datos = {
        'fecha_ini': request.form.get('fecha_ini', '').strip() or None,
        'fecha_fin': request.form.get('fecha_fin', '').strip() or None,
        'activo': request.form.get('activo') == 'on',
    }
    try:
        reemplazar('PeriodoAcademico', id_periodo, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_periodo'] = id_periodo
        return render_template('admin/periodo_form.html', modo='editar', periodo=datos, errores=ex.errores)

    flash(f"Periodo académico '{id_periodo}' actualizado.", 'exito')
    return redirect(url_for('admin.periodos_listar'))


@admin_bp.route('/periodos/<id_periodo>/eliminar', methods=['POST'])
def periodos_eliminar(id_periodo):
    try:
        eliminar('PeriodoAcademico', id_periodo)
        flash(f"Periodo académico '{id_periodo}' eliminado.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.periodos_listar'))


# ==========================================================================
#  ROL  (recurso API: "Rol", clave: id_rol)
# ==========================================================================

@admin_bp.route('/roles')
def roles_listar():
    try:
        roles = listar('Rol')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        roles = []
    return render_template('admin/rol_lista.html', roles=roles)


@admin_bp.route('/roles/nuevo', methods=['GET', 'POST'])
def roles_nuevo():
    if request.method == 'GET':
        return render_template('admin/rol_form.html', modo='nueva', rol=None, errores=[])

    datos = {
        'id_rol': request.form.get('id_rol', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'descripcion': request.form.get('descripcion', '').strip() or None,
    }
    try:
        crear('Rol', datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/rol_form.html', modo='nueva', rol=datos, errores=ex.errores)

    flash(f"Rol '{datos['id_rol']}' creado.", 'exito')
    return redirect(url_for('admin.roles_listar'))


@admin_bp.route('/roles/<id_rol>/editar', methods=['GET', 'POST'])
def roles_editar(id_rol):
    if request.method == 'GET':
        try:
            rol = obtener('Rol', id_rol)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.roles_listar'))
        return render_template('admin/rol_form.html', modo='editar', rol=rol, errores=[])

    datos = {
        'nombre': request.form.get('nombre', '').strip(),
        'descripcion': request.form.get('descripcion', '').strip() or None,
    }
    try:
        reemplazar('Rol', id_rol, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_rol'] = id_rol
        return render_template('admin/rol_form.html', modo='editar', rol=datos, errores=ex.errores)

    flash(f"Rol '{id_rol}' actualizado.", 'exito')
    return redirect(url_for('admin.roles_listar'))


@admin_bp.route('/roles/<id_rol>/eliminar', methods=['POST'])
def roles_eliminar(id_rol):
    try:
        eliminar('Rol', id_rol)
        flash(f"Rol '{id_rol}' eliminado.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.roles_listar'))


# ==========================================================================
#  TIPO DE DOCUMENTO  (recurso API: "TipoDocumento", clave: id_tipo_documento)
# ==========================================================================

@admin_bp.route('/tipos-documento')
def tipos_documento_listar():
    try:
        tipos = listar('TipoDocumento')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        tipos = []
    return render_template('admin/tipo_documento_lista.html', tipos=tipos)


@admin_bp.route('/tipos-documento/nuevo', methods=['GET', 'POST'])
def tipos_documento_nuevo():
    if request.method == 'GET':
        return render_template('admin/tipo_documento_form.html', modo='nueva', tipo=None, errores=[])

    datos = {
        'id_tipo_documento': request.form.get('id_tipo_documento', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'activo': request.form.get('activo') == 'on',
    }
    try:
        crear('TipoDocumento', datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/tipo_documento_form.html', modo='nueva', tipo=datos, errores=ex.errores)

    flash(f"Tipo de documento '{datos['id_tipo_documento']}' creado.", 'exito')
    return redirect(url_for('admin.tipos_documento_listar'))


@admin_bp.route('/tipos-documento/<id_tipo_documento>/editar', methods=['GET', 'POST'])
def tipos_documento_editar(id_tipo_documento):
    if request.method == 'GET':
        try:
            tipo = obtener('TipoDocumento', id_tipo_documento)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.tipos_documento_listar'))
        return render_template('admin/tipo_documento_form.html', modo='editar', tipo=tipo, errores=[])

    datos = {
        'nombre': request.form.get('nombre', '').strip(),
        'activo': request.form.get('activo') == 'on',
    }
    try:
        reemplazar('TipoDocumento', id_tipo_documento, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_tipo_documento'] = id_tipo_documento
        return render_template('admin/tipo_documento_form.html', modo='editar', tipo=datos, errores=ex.errores)

    flash(f"Tipo de documento '{id_tipo_documento}' actualizado.", 'exito')
    return redirect(url_for('admin.tipos_documento_listar'))


@admin_bp.route('/tipos-documento/<id_tipo_documento>/eliminar', methods=['POST'])
def tipos_documento_eliminar(id_tipo_documento):
    try:
        eliminar('TipoDocumento', id_tipo_documento)
        flash(f"Tipo de documento '{id_tipo_documento}' eliminado.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.tipos_documento_listar'))


# ==========================================================================
#  TIPO DE EVENTO  (recurso API: "TipoEvento", clave: id_tipo_evento)
# ==========================================================================

@admin_bp.route('/tipos-evento')
def tipos_evento_listar():
    try:
        tipos = listar('TipoEvento')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        tipos = []
    return render_template('admin/tipo_evento_lista.html', tipos=tipos)


@admin_bp.route('/tipos-evento/nuevo', methods=['GET', 'POST'])
def tipos_evento_nuevo():
    if request.method == 'GET':
        return render_template('admin/tipo_evento_form.html', modo='nueva', tipo=None, errores=[])

    datos = {
        'id_tipo_evento': request.form.get('id_tipo_evento', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'activo': request.form.get('activo') == 'on',
    }
    try:
        crear('TipoEvento', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, "debe tener exactamente 4 caracteres" (422).
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/tipo_evento_form.html', modo='nueva', tipo=datos, errores=ex.errores)

    flash(f"Tipo de evento '{datos['id_tipo_evento']}' creado.", 'exito')
    return redirect(url_for('admin.tipos_evento_listar'))


@admin_bp.route('/tipos-evento/<id_tipo_evento>/editar', methods=['GET', 'POST'])
def tipos_evento_editar(id_tipo_evento):
    if request.method == 'GET':
        try:
            tipo = obtener('TipoEvento', id_tipo_evento)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.tipos_evento_listar'))
        return render_template('admin/tipo_evento_form.html', modo='editar', tipo=tipo, errores=[])

    datos = {
        'nombre': request.form.get('nombre', '').strip(),
        'activo': request.form.get('activo') == 'on',
    }
    try:
        reemplazar('TipoEvento', id_tipo_evento, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_tipo_evento'] = id_tipo_evento
        return render_template('admin/tipo_evento_form.html', modo='editar', tipo=datos, errores=ex.errores)

    flash(f"Tipo de evento '{id_tipo_evento}' actualizado.", 'exito')
    return redirect(url_for('admin.tipos_evento_listar'))


@admin_bp.route('/tipos-evento/<id_tipo_evento>/eliminar', methods=['POST'])
def tipos_evento_eliminar(id_tipo_evento):
    try:
        eliminar('TipoEvento', id_tipo_evento)
        flash(f"Tipo de evento '{id_tipo_evento}' eliminado.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.tipos_evento_listar'))
