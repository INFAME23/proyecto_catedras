# ==========================================================================
#  routes/admin.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Panel de administración: CRUD de las 15 tablas que ya expone ApiCatedras
#  (las 10 sin FK de la v1 y las 5 de la v2: Facultad, Dependencia,
#  ProgramaAcademico, AliasPrograma y Catedra). Cada sección usa
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


# ==========================================================================
#  Ayudas compartidas (v2)
# ==========================================================================

def _avisar(ex):
    """Muestra el error de la API (mensaje + detalle) como alerta roja."""
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def _texto_o_none(campo):
    """Lee un campo del formulario; vacío -> None (la API lo recibe como null)."""
    return request.form.get(campo, '').strip() or None


def _opciones(recurso):
    """Lista de un recurso para llenar un <select>. Si la API falla, se avisa
    y se devuelve [] (el formulario se muestra igual, sin opciones)."""
    try:
        return listar(recurso)
    except ErrorApi as ex:
        _avisar(ex)
        return []


# ==========================================================================
#  TIPO DE PREGUNTA  (recurso API: "TipoPregunta", clave: id_tipo_pregunta)
#  Cierre de la v1: las plantillas existían pero faltaban estas rutas.
#  El formulario elige UN destino; aquí se traduce a los 3 booleanos.
# ==========================================================================

DESTINOS_PREGUNTA = [('numero', 'Número'), ('texto', 'Texto'), ('opcion', 'Opción')]


def _destino_de(tipo):
    """Qué destino tiene marcado un tipo de pregunta (para preseleccionarlo)."""
    if not tipo:
        return ''
    for valor, _ in DESTINOS_PREGUNTA:
        if tipo.get(f'guarda_{valor}'):
            return valor
    return ''


def _leer_form_tipo_pregunta():
    destino = request.form.get('destino', '')
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'guarda_numero': destino == 'numero',
        'guarda_texto': destino == 'texto',
        'guarda_opcion': destino == 'opcion',
    }


@admin_bp.route('/tipos-pregunta')
def tipos_pregunta_listar():
    try:
        tipos = listar('TipoPregunta')
    except ErrorApi as ex:
        _avisar(ex)
        tipos = []
    return render_template('admin/tipo_pregunta_lista.html', tipos=tipos)


@admin_bp.route('/tipos-pregunta/nuevo', methods=['GET', 'POST'])
def tipos_pregunta_nuevo():
    if request.method == 'GET':
        return render_template('admin/tipo_pregunta_form.html', modo='nueva', tipo=None,
                               errores=[], destinos=DESTINOS_PREGUNTA, destino='')

    datos = _leer_form_tipo_pregunta()
    datos['id_tipo_pregunta'] = request.form.get('id_tipo_pregunta', '').strip()
    try:
        crear('TipoPregunta', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/tipo_pregunta_form.html', modo='nueva', tipo=datos,
                               errores=ex.errores, destinos=DESTINOS_PREGUNTA, destino=_destino_de(datos))

    flash(f"Tipo de pregunta '{datos['id_tipo_pregunta']}' creado.", 'exito')
    return redirect(url_for('admin.tipos_pregunta_listar'))


@admin_bp.route('/tipos-pregunta/<id_tipo_pregunta>/editar', methods=['GET', 'POST'])
def tipos_pregunta_editar(id_tipo_pregunta):
    if request.method == 'GET':
        try:
            tipo = obtener('TipoPregunta', id_tipo_pregunta)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.tipos_pregunta_listar'))
        return render_template('admin/tipo_pregunta_form.html', modo='editar', tipo=tipo,
                               errores=[], destinos=DESTINOS_PREGUNTA, destino=_destino_de(tipo))

    datos = _leer_form_tipo_pregunta()
    try:
        reemplazar('TipoPregunta', id_tipo_pregunta, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_tipo_pregunta'] = id_tipo_pregunta
        return render_template('admin/tipo_pregunta_form.html', modo='editar', tipo=datos,
                               errores=ex.errores, destinos=DESTINOS_PREGUNTA, destino=_destino_de(datos))

    flash(f"Tipo de pregunta '{id_tipo_pregunta}' actualizado.", 'exito')
    return redirect(url_for('admin.tipos_pregunta_listar'))


@admin_bp.route('/tipos-pregunta/<id_tipo_pregunta>/eliminar', methods=['POST'])
def tipos_pregunta_eliminar(id_tipo_pregunta):
    try:
        eliminar('TipoPregunta', id_tipo_pregunta)
        flash(f"Tipo de pregunta '{id_tipo_pregunta}' eliminado.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.tipos_pregunta_listar'))


# ==========================================================================
#  TIPO DE VINCULACIÓN  (recurso API: "TipoVinculacion") — cierre de la v1
# ==========================================================================

def _leer_form_tipo_vinculacion():
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'es_interno': request.form.get('es_interno') == 'on',
        'exige_programa': request.form.get('exige_programa') == 'on',
        'activo': request.form.get('activo') == 'on',
    }


@admin_bp.route('/tipos-vinculacion')
def tipos_vinculacion_listar():
    try:
        tipos = listar('TipoVinculacion')
    except ErrorApi as ex:
        _avisar(ex)
        tipos = []
    return render_template('admin/tipo_vinculacion_lista.html', tipos=tipos)


@admin_bp.route('/tipos-vinculacion/nuevo', methods=['GET', 'POST'])
def tipos_vinculacion_nuevo():
    if request.method == 'GET':
        return render_template('admin/tipo_vinculacion_form.html', modo='nueva', tipo=None, errores=[])

    datos = _leer_form_tipo_vinculacion()
    datos['id_tipo_vinculacion'] = request.form.get('id_tipo_vinculacion', '').strip()
    try:
        crear('TipoVinculacion', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/tipo_vinculacion_form.html', modo='nueva', tipo=datos, errores=ex.errores)

    flash(f"Tipo de vinculación '{datos['id_tipo_vinculacion']}' creado.", 'exito')
    return redirect(url_for('admin.tipos_vinculacion_listar'))


@admin_bp.route('/tipos-vinculacion/<id_tipo_vinculacion>/editar', methods=['GET', 'POST'])
def tipos_vinculacion_editar(id_tipo_vinculacion):
    if request.method == 'GET':
        try:
            tipo = obtener('TipoVinculacion', id_tipo_vinculacion)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.tipos_vinculacion_listar'))
        return render_template('admin/tipo_vinculacion_form.html', modo='editar', tipo=tipo, errores=[])

    datos = _leer_form_tipo_vinculacion()
    try:
        reemplazar('TipoVinculacion', id_tipo_vinculacion, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_tipo_vinculacion'] = id_tipo_vinculacion
        return render_template('admin/tipo_vinculacion_form.html', modo='editar', tipo=datos, errores=ex.errores)

    flash(f"Tipo de vinculación '{id_tipo_vinculacion}' actualizado.", 'exito')
    return redirect(url_for('admin.tipos_vinculacion_listar'))


@admin_bp.route('/tipos-vinculacion/<id_tipo_vinculacion>/eliminar', methods=['POST'])
def tipos_vinculacion_eliminar(id_tipo_vinculacion):
    try:
        eliminar('TipoVinculacion', id_tipo_vinculacion)
        flash(f"Tipo de vinculación '{id_tipo_vinculacion}' eliminado.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.tipos_vinculacion_listar'))


# ==========================================================================
#  v2 · FACULTAD  (recurso API: "Facultad", clave: id_facultad)
#  El formulario ofrece un <select> con las facultades existentes como
#  posible padre (RF94). La regla "no puede ser su propio padre" la aplica
#  la API (400); aquí además se quita la propia fila de las opciones.
# ==========================================================================

def _leer_form_facultad():
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'fk_facultad_padre': _texto_o_none('fk_facultad_padre'),
        'activo': request.form.get('activo') == 'on',
    }


@admin_bp.route('/facultades')
def facultades_listar():
    try:
        facultades = listar('Facultad')
    except ErrorApi as ex:
        _avisar(ex)
        facultades = []
    return render_template('admin/facultad_lista.html', facultades=facultades)


@admin_bp.route('/facultades/nueva', methods=['GET', 'POST'])
def facultades_nueva():
    if request.method == 'GET':
        return render_template('admin/facultad_form.html', modo='nueva', facultad=None,
                               errores=[], padres=_opciones('Facultad'))

    datos = _leer_form_facultad()
    datos['id_facultad'] = request.form.get('id_facultad', '').strip()
    try:
        crear('Facultad', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/facultad_form.html', modo='nueva', facultad=datos,
                               errores=ex.errores, padres=_opciones('Facultad'))

    flash(f"Facultad '{datos['id_facultad']}' creada.", 'exito')
    return redirect(url_for('admin.facultades_listar'))


@admin_bp.route('/facultades/<id_facultad>/editar', methods=['GET', 'POST'])
def facultades_editar(id_facultad):
    # Posibles padres: todas MENOS la propia facultad.
    padres = [f for f in _opciones('Facultad') if f['id_facultad'] != id_facultad]

    if request.method == 'GET':
        try:
            facultad = obtener('Facultad', id_facultad)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.facultades_listar'))
        return render_template('admin/facultad_form.html', modo='editar', facultad=facultad,
                               errores=[], padres=padres)

    datos = _leer_form_facultad()
    try:
        reemplazar('Facultad', id_facultad, datos)   # PUT: el padre vacío queda en null
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_facultad'] = id_facultad
        return render_template('admin/facultad_form.html', modo='editar', facultad=datos,
                               errores=ex.errores, padres=padres)

    flash(f"Facultad '{id_facultad}' actualizada.", 'exito')
    return redirect(url_for('admin.facultades_listar'))


@admin_bp.route('/facultades/<id_facultad>/eliminar', methods=['POST'])
def facultades_eliminar(id_facultad):
    try:
        eliminar('Facultad', id_facultad)
        flash(f"Facultad '{id_facultad}' eliminada (sus hijas y programas quedan sin facultad).", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.facultades_listar'))


# ==========================================================================
#  v2 · DEPENDENCIA  (recurso API: "Dependencia", clave: id_dependencia)
#  Mismo patrón que Facultad (autorreferencia con <select> de padres).
# ==========================================================================

def _leer_form_dependencia():
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'fk_dependencia_padre': _texto_o_none('fk_dependencia_padre'),
        'activo': request.form.get('activo') == 'on',
    }


@admin_bp.route('/dependencias')
def dependencias_listar():
    try:
        dependencias = listar('Dependencia')
    except ErrorApi as ex:
        _avisar(ex)
        dependencias = []
    return render_template('admin/dependencia_lista.html', dependencias=dependencias)


@admin_bp.route('/dependencias/nueva', methods=['GET', 'POST'])
def dependencias_nueva():
    if request.method == 'GET':
        return render_template('admin/dependencia_form.html', modo='nueva', dependencia=None,
                               errores=[], padres=_opciones('Dependencia'))

    datos = _leer_form_dependencia()
    datos['id_dependencia'] = request.form.get('id_dependencia', '').strip()
    try:
        crear('Dependencia', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/dependencia_form.html', modo='nueva', dependencia=datos,
                               errores=ex.errores, padres=_opciones('Dependencia'))

    flash(f"Dependencia '{datos['id_dependencia']}' creada.", 'exito')
    return redirect(url_for('admin.dependencias_listar'))


@admin_bp.route('/dependencias/<id_dependencia>/editar', methods=['GET', 'POST'])
def dependencias_editar(id_dependencia):
    padres = [d for d in _opciones('Dependencia') if d['id_dependencia'] != id_dependencia]

    if request.method == 'GET':
        try:
            dependencia = obtener('Dependencia', id_dependencia)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.dependencias_listar'))
        return render_template('admin/dependencia_form.html', modo='editar', dependencia=dependencia,
                               errores=[], padres=padres)

    datos = _leer_form_dependencia()
    try:
        reemplazar('Dependencia', id_dependencia, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_dependencia'] = id_dependencia
        return render_template('admin/dependencia_form.html', modo='editar', dependencia=datos,
                               errores=ex.errores, padres=padres)

    flash(f"Dependencia '{id_dependencia}' actualizada.", 'exito')
    return redirect(url_for('admin.dependencias_listar'))


@admin_bp.route('/dependencias/<id_dependencia>/eliminar', methods=['POST'])
def dependencias_eliminar(id_dependencia):
    try:
        eliminar('Dependencia', id_dependencia)
        flash(f"Dependencia '{id_dependencia}' eliminada (sus hijas y cátedras quedan sin dependencia).", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.dependencias_listar'))


# ==========================================================================
#  v2 · PROGRAMA ACADÉMICO  (recurso API: "ProgramaAcademico", clave: codigo)
# ==========================================================================

NIVELES_PROGRAMA = ['PREGRADO', 'ESPECIALIZACION', 'MAESTRIA', 'DOCTORADO', 'EDUCACION_CONTINUA', 'OTRO']


def _leer_form_programa():
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'fk_facultad': _texto_o_none('fk_facultad'),
        'nivel': request.form.get('nivel', ''),
        'activo': request.form.get('activo') == 'on',
    }


def _render_programa_form(modo, programa, errores):
    return render_template('admin/programa_form.html', modo=modo, programa=programa, errores=errores,
                           facultades=_opciones('Facultad'), niveles=NIVELES_PROGRAMA)


@admin_bp.route('/programas')
def programas_listar():
    try:
        programas = listar('ProgramaAcademico')
    except ErrorApi as ex:
        _avisar(ex)
        programas = []
    return render_template('admin/programa_lista.html', programas=programas)


@admin_bp.route('/programas/nuevo', methods=['GET', 'POST'])
def programas_nuevo():
    if request.method == 'GET':
        return _render_programa_form('nueva', None, [])

    datos = _leer_form_programa()
    datos['codigo'] = request.form.get('codigo', '').strip()
    # Retrofit V2 (Guía IA, Regla 1): alias iniciales, UNO POR LÍNEA en el
    # <textarea>. Se mandan en el MISMO POST que el programa, así la API los
    # guarda juntos en una sola transacción (SP sp_guardar_programa_academico).
    # splitlines() parte el texto por renglones; se descartan los vacíos.
    datos['alias'] = [
        {'texto_normalizado': linea.strip()}
        for linea in request.form.get('alias', '').splitlines()
        if linea.strip()
    ]
    try:
        crear('ProgramaAcademico', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, el 422 de un código en minúscula, o el
        # 500 de un alias que ya existe (y entonces NO se creó nada: ni el
        # programa ni ningún alias).
        _avisar(ex)
        return _render_programa_form('nueva', datos, ex.errores)

    cantidad = len(datos['alias'])
    sufijo = f" con {cantidad} alias" if cantidad else ''
    flash(f"Programa '{datos['codigo']}' creado{sufijo}.", 'exito')
    return redirect(url_for('admin.programas_listar'))


@admin_bp.route('/programas/<codigo>/editar', methods=['GET', 'POST'])
def programas_editar(codigo):
    if request.method == 'GET':
        try:
            programa = obtener('ProgramaAcademico', codigo)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.programas_listar'))
        return _render_programa_form('editar', programa, [])

    datos = _leer_form_programa()
    try:
        reemplazar('ProgramaAcademico', codigo, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['codigo'] = codigo
        return _render_programa_form('editar', datos, ex.errores)

    flash(f"Programa '{codigo}' actualizado.", 'exito')
    return redirect(url_for('admin.programas_listar'))


@admin_bp.route('/programas/<codigo>/eliminar', methods=['POST'])
def programas_eliminar(codigo):
    try:
        eliminar('ProgramaAcademico', codigo)
        flash(f"Programa '{codigo}' eliminado.", 'exito')
    except ErrorApi as ex:
        # Lo normal: 500 porque tiene asistentes (FK RESTRICT de programa_asistente).
        _avisar(ex)
    return redirect(url_for('admin.programas_listar'))


# ==========================================================================
#  v2 · ALIAS DE PROGRAMA  (recurso API: "AliasPrograma", clave: id_alias)
#  SOLO listar, crear y eliminar: la API no tiene PUT/PATCH (RF80-RF83).
# ==========================================================================

@admin_bp.route('/alias-programa')
def alias_listar():
    try:
        alias = listar('AliasPrograma')
    except ErrorApi as ex:
        _avisar(ex)
        alias = []
    return render_template('admin/alias_lista.html', alias=alias)


@admin_bp.route('/alias-programa/nuevo', methods=['GET', 'POST'])
def alias_nuevo():
    if request.method == 'GET':
        return render_template('admin/alias_form.html', alias=None, errores=[],
                               programas=_opciones('ProgramaAcademico'))

    datos = {
        'texto_normalizado': request.form.get('texto_normalizado', '').strip(),
        'fk_programa': request.form.get('fk_programa', ''),
    }
    origen = _texto_o_none('origen')
    if origen:                 # si no llega, la API usa 'FORMULARIO'
        datos['origen'] = origen
    try:
        respuesta = crear('AliasPrograma', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/alias_form.html', alias=datos, errores=ex.errores,
                               programas=_opciones('ProgramaAcademico'))

    flash(f"Alias creado con id {respuesta['id_alias']}.", 'exito')
    return redirect(url_for('admin.alias_listar'))


@admin_bp.route('/alias-programa/<int:id_alias>/eliminar', methods=['POST'])
def alias_eliminar(id_alias):
    try:
        eliminar('AliasPrograma', id_alias)
        flash(f"Alias {id_alias} eliminado.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.alias_listar'))


# ==========================================================================
#  v2 · CÁTEDRA  (recurso API: "Catedra", clave: id_catedra numérica)
#  Con 5502 filas, la lista tiene buscador y paginación (mismo criterio que
#  el catálogo público: una llamada a la API, filtro y corte en Python).
#  'nombre_asis' NO está en el formulario: la calcula la BD.
# ==========================================================================

CATEDRAS_POR_PAGINA = 50


def _leer_form_catedra():
    return {
        'id_evento_asis': request.form.get('id_evento_asis', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'fk_tipo_evento': request.form.get('fk_tipo_evento', ''),
        'fk_dependencia': _texto_o_none('fk_dependencia'),
        'activo': request.form.get('activo') == 'on',
    }


def _render_catedra_form(modo, catedra, errores):
    return render_template('admin/catedra_form.html', modo=modo, catedra=catedra, errores=errores,
                           tipos_evento=_opciones('TipoEvento'), dependencias=_opciones('Dependencia'))


@admin_bp.route('/catedras')
def catedras_listar():
    texto = request.args.get('q', '').strip()
    try:
        pagina = max(1, int(request.args.get('pagina', 1)))
    except ValueError:
        pagina = 1

    try:
        catedras = listar('Catedra', timeout=30)
    except ErrorApi as ex:
        _avisar(ex)
        catedras = []

    if texto:
        buscado = texto.lower()
        catedras = [c for c in catedras
                    if buscado in c['nombre'].lower() or buscado in c['id_evento_asis']]

    total = len(catedras)
    total_paginas = max(1, -(-total // CATEDRAS_POR_PAGINA))
    pagina = min(pagina, total_paginas)
    corte = (pagina - 1) * CATEDRAS_POR_PAGINA
    return render_template('admin/catedra_lista.html', catedras=catedras[corte:corte + CATEDRAS_POR_PAGINA],
                           q=texto, pagina=pagina, total_paginas=total_paginas, total=total)


@admin_bp.route('/catedras/nueva', methods=['GET', 'POST'])
def catedras_nueva():
    if request.method == 'GET':
        return _render_catedra_form('nueva', None, [])

    datos = _leer_form_catedra()
    try:
        respuesta = crear('Catedra', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return _render_catedra_form('nueva', datos, ex.errores)

    flash(f"Cátedra creada con id {respuesta['id_catedra']} "
          f"(nombre ASIS: '{respuesta['nombre_asis']}').", 'exito')
    return redirect(url_for('admin.catedras_listar'))


@admin_bp.route('/catedras/<int:id_catedra>/editar', methods=['GET', 'POST'])
def catedras_editar(id_catedra):
    if request.method == 'GET':
        try:
            catedra = obtener('Catedra', id_catedra)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.catedras_listar'))
        return _render_catedra_form('editar', catedra, [])

    datos = _leer_form_catedra()
    try:
        reemplazar('Catedra', id_catedra, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_catedra'] = id_catedra
        return _render_catedra_form('editar', datos, ex.errores)

    flash(f"Cátedra {id_catedra} actualizada.", 'exito')
    return redirect(url_for('admin.catedras_listar'))


@admin_bp.route('/catedras/<int:id_catedra>/eliminar', methods=['POST'])
def catedras_eliminar(id_catedra):
    try:
        eliminar('Catedra', id_catedra)
        flash(f"Cátedra {id_catedra} eliminada (y sus sesiones, por ON DELETE CASCADE).", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.catedras_listar'))
