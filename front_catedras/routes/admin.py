# ==========================================================================
#  routes/admin.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Panel de administración: las tablas que expone ApiCatedras
#  (las 10 sin FK de la v1; las 5 de la v2: Facultad, Dependencia,
#  ProgramaAcademico, AliasPrograma y Catedra; las 7 de la v3: personas
#  y lotes de carga; las 2 de la v4: Sesion y Ponencia; y las 5 de la v5:
#  Usuario, RolPorUsuario, EnlaceRegistro, ClaveAcceso y RegistroAsistencia;
#  las 4 de la v6: encuestas; y las 4 de la v7: Parametro, LoteMigracion,
#  DetalleMigracion y Bitacora, al final del archivo — 37 en total). Cada sección usa
#  formularios HTML normales (los navegadores no mandan PUT/DELETE, así que
#  el botón "Guardar" hace POST y esta ruta traduce a PUT/DELETE contra la
#  API con 'requests').
#
#  Este blueprint NO reimplementa reglas de negocio (límite > 0, fechas
#  coherentes, es_exitoso->es_final, canal_difusion en una lista cerrada):
#  esas las aplica ApiCatedras. Aquí solo se capturan sus errores
#  (ErrorApi) y se muestran en la plantilla.
#
#  v5 · ACCESO: TODO este blueprint exige sesión con el rol ADMIN
#  (before_request de abajo, UN solo lugar). Sin sesión -> /login; con sesión
#  pero sin ADMIN -> 403. Es una protección del FRONT: ApiCatedras sigue sin
#  exigir nada (ADR D28).
# ==========================================================================

from datetime import datetime, timezone, timedelta

from flask import Blueprint, abort, flash, jsonify, redirect, render_template, request, url_for

from auth_helpers import ROL_ADMIN, ROL_ASISTENTE, exigir_rol, tiene_rol, usuario_actual
from servicios.api_catedras import (ErrorApi, actualizar_parcial, consultar, crear, eliminar, listar,
                                    obtener, reemplazar)
from servicios.fechas import ZONA_COLOMBIA, a_local, desde_input, fecha_hora
from servicios.qr_sesiones import (asegurar_qr_de_catedra, avisar_qr_pendientes, emitir_qr,
                                    estado_de_ventana, qr_de_sesion, svg_qr, url_de_registro)
from routes.sesiones import (ROLES_PONENCIA, asignar_ponente, buscar_candidatos, ponentes_de,
                             quitar_ponente)
from routes.admin_solicitudes import contar_pendientes, registrar_rutas   # v7.1 - D56: bandeja de ponentes
from routes import admin_restricciones   # v7.1 - D57: periodos y horarios en que se pueden programar cátedras
from routes import admin_exportar   # v7.2 - RF-S13: exportación de asistencia por cátedra

admin_bp = Blueprint('admin', __name__)


# El ASISTENTE gestiona SOLO cátedras (listar, ver, agregar, editar, borrar), las dos consultas de
# disponibilidad que usa el formulario de cátedra y la bandeja de ponentes. Todo lo demás de '/admin' es del ADMIN.
ENDPOINTS_DEL_ASISTENTE = frozenset({
    'admin.catedras_listar', 'admin.catedras_nueva', 'admin.catedras_detalle', 'admin.catedras_editar',
    'admin.catedras_eliminar', 'admin.catedras_disponibilidad', 'admin.catedras_agenda',
    # La bandeja de ponentes (aprobar o rechazar «Soy ponente»), igual que la del ADMIN.
    'admin.solicitudes_ponente_listar', 'admin.solicitudes_ponente_detalle', 'admin.solicitudes_ponente_resolver',
    # v7.2 · D73 (RF-S15): ver, generar y regenerar el QR de una sesión. La lista de quienes se registraron
    # ('admin.sesiones_asistencia_json') NO está aquí: son datos de personas y siguen siendo solo del ADMIN.
    'admin.sesiones_qr',
})


@admin_bp.before_request
def _solo_administradores():
    """v5 · Guardia de TODO '/admin'. Si devuelve algo distinto de None
    (redirección a /login o 403), Flask NO llega a ejecutar la ruta.
    El ADMIN entra a todo; el ASISTENTE, solo a las rutas de cátedras (ENDPOINTS_DEL_ASISTENTE)."""
    if request.endpoint in ENDPOINTS_DEL_ASISTENTE and tiene_rol(ROL_ASISTENTE):
        return None
    return exigir_rol(ROL_ADMIN)


@admin_bp.route('/')
def panel():
    # v7.1 · D56: cuántas solicitudes «Soy ponente» esperan decisión (None = la API no respondió).
    return render_template('admin/panel.html', solicitudes_pendientes=contar_pendientes())


# v7.1 · D56: la bandeja de solicitudes de ponente (routes/admin_solicitudes.py) cuelga de este mismo
# blueprint, así hereda la guardia de ADMIN de arriba.
registrar_rutas(admin_bp)
# v7.1 · D57: periodos y horarios de programación (routes/admin_restricciones.py), también colgados de este blueprint.
admin_restricciones.registrar_rutas(admin_bp)

# v7.2 · RF-S13/RF-S14: exportación de fin de periodo (routes/admin_exportar.py), colgada de este mismo blueprint (solo ADMIN).
admin_exportar.registrar_rutas(admin_bp)


# ==========================================================================
#  MAESTRO-DETALLE EN EL FRONT  (navegación: "entro al maestro y veo sus detalles")
# --------------------------------------------------------------------------
#  Cada maestro tiene una pantalla de DETALLE (/admin/<maestro>/<id>) que
#  muestra su ficha y, debajo, las tablas de sus detalles, con los botones
#  para agregar uno nuevo (la FK del maestro la fija la URL, Regla 2):
#
#     #  Maestro              Detalle(s)                         Pantalla
#     1  catedra              sesion                             catedras_detalle
#     2  sesion               enlace_registro                    sesiones_detalle
#     3  sesion               registro_asistencia                sesiones_detalle
#     4  asistente            documento_asistente                asistentes_detalle (v3)
#     5  asistente            consentimiento_datos               asistentes_detalle (v3)
#     6  encuesta             pregunta                           encuestas_detalle
#     7  pregunta             opcion_pregunta                    preguntas_detalle
#     8  registro_asistencia  respuesta_encuesta                 registros_detalle
#     9  respuesta_encuesta   respuesta_item                     respuestas_detalle (v6)
#    10  programa_academico   alias_programa                     programas_detalle
#
#  Reglas de la navegación:
#    · Al CREAR o EDITAR un maestro se aterriza en su pantalla de detalle
#      (así se ven enseguida sus detalles y se pueden agregar más).
#    · En las listas, el id / nombre del maestro y el botón del ojo llevan
#      a esa pantalla.
#    · Cuando un detalle se crea, edita o borra DESDE la pantalla del
#      maestro, el formulario manda 'volver=<ruta del maestro>' y la acción
#      regresa ahí. Sin 'volver', cada acción conserva su destino de siempre
#      (las listas filtradas siguen funcionando igual).
# ==========================================================================

def _destino_volver():
    """La ruta a la que hay que regresar ('volver' del formulario o de la
    URL), solo si es una ruta INTERNA de /admin. Cualquier otra cosa
    (http://..., //otro-sitio, javascript:...) se ignora: no es una
    redirección abierta."""
    destino = (request.values.get('volver') or '').strip()
    if destino.startswith('/admin/') and '//' not in destino and '\\' not in destino:
        return destino
    return None


@admin_bp.context_processor
def _contexto_volver():
    """'volver' queda disponible en TODAS las plantillas de /admin: los
    formularios de detalle lo reenvían (campo oculto) y su botón Cancelar
    regresa ahí. Si la vista pasa su propio 'volver', gana el de la vista."""
    return {'volver': _destino_volver()}


def _redirigir(endpoint, **valores):
    """Redirige a 'volver' si llegó uno válido; si no, al destino de siempre."""
    return redirect(_destino_volver() or url_for(endpoint, **valores))


def _lista_de(recurso, **filtros):
    """listar() para una tabla de detalle: si la API falla, se avisa y la
    pantalla del maestro se muestra igual, con la tabla vacía."""
    try:
        return listar(recurso, **filtros)
    except ErrorApi as ex:
        _avisar(ex)
        return []


# --------------------------------------------------------------------------
#  Filas de detalle DENTRO del formulario del maestro
# --------------------------------------------------------------------------
#  El formulario de cada maestro trae un bloque de filas (ver
#  templates/partials/filas_detalle.html): cada campo se llama
#  '<prefijo>_<campo>' y llega UNA vez por fila, en orden. Dos momentos:
#
#    · AL CREAR el maestro: las filas viajan en el MISMO POST y la API guarda
#      maestro + detalles en UNA transacción (Guía IA, Regla 1: un SP). Si
#      algo falla, no se guarda nada y el formulario vuelve con todo lo
#      escrito (maestro y filas).
#    · AL EDITAR: el maestro ya existe, así que cada fila nueva es su propia
#      petición al recurso del detalle (el caso "No aplica" de la Regla 1:
#      el detalle se agrega en otro momento). Las que fallan vuelven a
#      aparecer en el formulario con su motivo; las demás ya quedaron.
# --------------------------------------------------------------------------

def _filas(prefijo, campos, clave):
    """Lee las filas de un bloque de detalle del formulario.

    'campos': nombres de los campos de UNA fila (sin el prefijo).
    'clave':  el campo que hace que una fila "cuente": una fila con la
              clave vacía se descarta (p. ej. una sesión sin fecha de
              inicio). No se puede usar "todos vacíos" porque los <select>
              siempre traen algún valor.
    Devuelve una lista de dicts {campo: texto}, en el orden del formulario."""
    columnas = {c: request.form.getlist(f'{prefijo}_{c}') for c in campos}
    total = max((len(v) for v in columnas.values()), default=0)
    filas = []
    for i in range(total):
        fila = {c: (columnas[c][i].strip() if i < len(columnas[c]) else '') for c in campos}
        if fila.get(clave):
            filas.append(fila)
    return filas


def _crear_detalles(recurso, filas, a_cuerpo, nombre, plural):
    """AL EDITAR: crea cada fila nueva con su propia petición (y su propia
    transacción). 'a_cuerpo(fila, n)' arma el body de la API (n = posición
    1, 2, 3... para calcular 'orden' cuando hace falta). Avisa cuántas se
    guardaron y, de las que no, el motivo. Devuelve las filas que FALLARON,
    para volver a mostrarlas en el formulario."""
    fallidas, creadas = [], 0
    for n, fila in enumerate(filas, start=1):
        try:
            crear(recurso, a_cuerpo(fila, n))
            creadas += 1
        except ErrorApi as ex:
            flash(f"{nombre} {n}: {ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            fallidas.append(fila)
    if creadas:
        flash(f"Se agregó 1 {nombre.lower()}." if creadas == 1 else f"Se agregaron {creadas} {plural}.", 'exito')
    return fallidas


@admin_bp.route('/asistentes/buscar')
def asistentes_buscar():
    """Búsqueda de personas para las filas de un formulario (JSON). Regla 2:
    el asistente no se digita, se busca y se elige de la lista. Mismo
    criterio que 'buscar_candidatos' (la API filtra con ?q=, máx. 20)."""
    texto = request.args.get('q', '').strip()
    personas = [{'id_asistente': a['id_asistente'],
                 'texto': f"{a['id_asistente']} · {a['nombres']} {a['apellidos']}"
                          + (f" · {a['id_asis']}" if a.get('id_asis') else '')}
                for a in buscar_candidatos(texto)]
    return jsonify(personas)


# ==========================================================================
#  SEDE  (recurso API: "Sede", clave: id_sede)
# ==========================================================================

def _ver_inactivas():
    """True si la lista se pidió con '?inactivas=1'.

    RETROFIT criterio 3: los listados de Sede, Facultad, Dependencia, Programa y
    Cátedra salen de procedimientos que, por defecto, solo devuelven las ACTIVAS.
    Con esto la API recibe '?incluirInactivos=true' y también trae las inactivas
    (así se pueden ver y reactivar con Editar). None = no se manda el parámetro.
    """
    return 'true' if request.args.get('inactivas') == '1' else None


@admin_bp.route('/sedes')
def sedes_listar():
    try:
        sedes = listar('Sede', incluirInactivos=_ver_inactivas())
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        sedes = []
    return render_template('admin/sede_lista.html', sedes=sedes, ver_inactivas=_ver_inactivas() is not None)


def _ubicacion_de_sede(es_virtual):
    """RF-S11: latitud, longitud y radio del formulario de sede -> (datos, errores).
    Solo viajan los que se escribieron (la API exige latitud y longitud JUNTAS y revisa los rangos).
    Una sede virtual no tiene ubicación: no se manda nada (la API borra la que tuviera).
    Se acepta coma decimal. El radio es un entero de metros."""
    datos, errores = {}, []
    if es_virtual:
        return datos, errores
    for campo, convertir in (('latitud', float), ('longitud', float), ('radio_metros', int)):
        texto = request.form.get(campo, '').strip().replace(',', '.')
        if not texto:
            continue
        try:
            datos[campo] = convertir(texto)
        except ValueError:
            errores.append({'campo': campo, 'mensaje': 'No es un número válido.'})
    return datos, errores


def _sede_para_el_formulario(datos):
    """Los datos de una sede para volver a pintar el formulario con lo que la persona escribió
    (los campos de ubicación se devuelven tal cual, aunque no fueran un número)."""
    return {**datos, **{c: request.form.get(c, '') for c in ('latitud', 'longitud', 'radio_metros')}}


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
    ubicacion, errores_ubicacion = _ubicacion_de_sede(datos['es_virtual'])
    if errores_ubicacion:
        return render_template('admin/sede_form.html', modo='nueva', sede=_sede_para_el_formulario(datos),
                               errores=errores_ubicacion)
    datos.update(ubicacion)
    try:
        crear('Sede', datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return render_template('admin/sede_form.html', modo='nueva', sede=_sede_para_el_formulario(datos), errores=ex.errores)

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
    ubicacion, errores_ubicacion = _ubicacion_de_sede(datos['es_virtual'])
    if errores_ubicacion:
        datos['id_sede'] = id_sede
        return render_template('admin/sede_form.html', modo='editar', sede=_sede_para_el_formulario(datos),
                               errores=errores_ubicacion)
    datos.update(ubicacion)
    try:
        reemplazar('Sede', id_sede, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_sede'] = id_sede
        return render_template('admin/sede_form.html', modo='editar', sede=_sede_para_el_formulario(datos), errores=ex.errores)

    flash(f"Sede '{id_sede}' actualizada.", 'exito')
    return redirect(url_for('admin.sedes_listar'))


@admin_bp.route('/sedes/<id_sede>/eliminar', methods=['POST'])
def sedes_eliminar(id_sede):
    try:
        eliminar('Sede', id_sede)
        flash(f"Sede '{id_sede}' desactivada (borrado lógico: sigue en la base de datos; la lista con inactivas la muestra y se reactiva con Editar).", 'exito')
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
    vigente_hasta = request.form.get('vigente_hasta', '').strip() or None
    version_texto = request.form.get('version', '').strip()
    return {
        'nombre': request.form.get('nombre', '').strip(),
        'version': int(version_texto) if version_texto else None,
        'vigente_desde': request.form.get('vigente_desde', '').strip() or None,
        'vigente_hasta': vigente_hasta,
        'activa': request.form.get('activa') == 'on',
    }


CAMPOS_PREGUNTA_FILA = ('enunciado', 'fk_tipo_pregunta', 'obligatoria', 'valor_min', 'valor_max', 'opciones')


def _pregunta_de_fila(fila, orden):
    pregunta = {
        'enunciado': fila['enunciado'],
        'orden': orden,
        'fk_tipo_pregunta': fila['fk_tipo_pregunta'],
        'obligatoria': fila['obligatoria'] == 'si',
        'valor_min': _entero_o_none(fila['valor_min']),
        'valor_max': _entero_o_none(fila['valor_max']),
    }
    opciones = _opciones_de_texto(fila['opciones'])
    if opciones:
        pregunta['opciones'] = opciones
    return pregunta


def _render_encuesta_form(modo, encuesta, errores, filas_preguntas=(), id_encuesta=None):
    existentes = []
    if modo == 'editar' and id_encuesta:
        existentes = sorted(_lista_de('Pregunta', limite=500, fkEncuesta=id_encuesta), key=lambda p: p['orden'])
    return render_template('admin/encuesta_form.html', modo=modo, encuesta=encuesta, errores=errores,
                           filas_preguntas=list(filas_preguntas), preguntas_existentes=existentes,
                           tipos=_opciones('TipoPregunta'))


@admin_bp.route('/encuestas/nueva', methods=['GET', 'POST'])
def encuestas_nueva():
    if request.method == 'GET':
        return _render_encuesta_form('nueva', None, [])

    datos = _leer_form_encuesta()
    filas = _filas('pre', CAMPOS_PREGUNTA_FILA, clave='enunciado')
    if filas:
        datos['preguntas'] = [_pregunta_de_fila(f, n) for n, f in enumerate(filas, start=1)]
    try:
        respuesta = crear('Encuesta', datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return _render_encuesta_form('nueva', datos, ex.errores, filas)

    sufijo = f" con {len(filas)} pregunta{'' if len(filas) == 1 else 's'}" if filas else ''
    flash(f"Encuesta '{datos['nombre']}' creada{sufijo} (id {respuesta['id_encuesta']}).", 'exito')
    return redirect(url_for('admin.encuestas_detalle', id_encuesta=respuesta['id_encuesta']))


@admin_bp.route('/encuestas/<int:id_encuesta>')
def encuestas_detalle(id_encuesta):
    try:
        encuesta = obtener('Encuesta', id_encuesta)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.encuestas_listar'))

    preguntas = sorted(_lista_de('Pregunta', limite=500, fkEncuesta=id_encuesta),
                       key=lambda p: p['orden'])
    ids = {p['id_pregunta'] for p in preguntas}
    conteo = {}
    if ids:
        for o in _lista_de('OpcionPregunta', limite=5000):
            if o['fk_pregunta'] in ids:
                conteo[o['fk_pregunta']] = conteo.get(o['fk_pregunta'], 0) + 1
    for p in preguntas:
        p['cantidad_opciones'] = conteo.get(p['id_pregunta'], 0)

    return render_template('admin/encuesta_detalle.html', encuesta=encuesta, preguntas=preguntas)


@admin_bp.route('/encuestas/<int:id_encuesta>/editar', methods=['GET', 'POST'])
def encuestas_editar(id_encuesta):
    if request.method == 'GET':
        try:
            encuesta = obtener('Encuesta', id_encuesta)
        except ErrorApi as ex:
            flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
            return redirect(url_for('admin.encuestas_listar'))
        return _render_encuesta_form('editar', encuesta, [], id_encuesta=id_encuesta)

    datos = _leer_form_encuesta()
    filas = _filas('pre', CAMPOS_PREGUNTA_FILA, clave='enunciado')
    try:
        reemplazar('Encuesta', id_encuesta, datos)
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        datos['id_encuesta'] = id_encuesta
        return _render_encuesta_form('editar', datos, ex.errores, filas, id_encuesta)

    flash(f"Encuesta '{id_encuesta}' actualizada.", 'exito')
    datos['id_encuesta'] = id_encuesta
    ultimo = max((p['orden'] for p in _lista_de('Pregunta', limite=500, fkEncuesta=id_encuesta)), default=0)
    fallidas = _crear_detalles('Pregunta', filas,
                               lambda f, n: {**_pregunta_de_fila(f, ultimo + n), 'fk_encuesta': id_encuesta},
                               'Pregunta', 'preguntas')
    if fallidas:
        return _render_encuesta_form('editar', datos, [], fallidas, id_encuesta)
    return redirect(url_for('admin.encuestas_detalle', id_encuesta=id_encuesta))


@admin_bp.route('/encuestas/<int:id_encuesta>/eliminar', methods=['POST'])
def encuestas_eliminar(id_encuesta):
    try:
        eliminar('Encuesta', id_encuesta)
        flash(f"Encuesta '{id_encuesta}' eliminada.", 'exito')
    except ErrorApi as ex:
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
    return redirect(url_for('admin.encuestas_listar'))