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
#   · AL CREAR el maestro: las filas viajan en el MISMO POST y la API guarda
#     maestro + detalles en UNA transacción (Guía IA, Regla 1: un SP). Si
#     algo falla, no se guarda nada y el formulario vuelve con todo lo
#     escrito (maestro y filas).
#   · AL EDITAR: el maestro ya existe, así que cada fila nueva es su propia
#     petición al recurso del detalle (el caso "No aplica" de la Regla 1:
#     el detalle se agrega en otro momento). Las que fallan vuelven a
#     aparecer en el formulario con su motivo; las demás ya quedaron.
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
    # RF-S11: latitud, longitud y radio del formulario de sede -> (datos, errores).
    ubicacion, errores_ubicacion = _ubicacion_de_sede(datos['es_virtual'])
    if errores_ubicacion:
        datos['id_sede'] = id_sede
        return render_template('admin/sede_form.html', modo='editar', sede=_sede_para_el_formulario(datos),
                               errores=errores_ubicacion)
    datos.update(ubicacion)
    # v7.2: la ubicación solo se manda si se escribió (en blanco = no cambia); una sede virtual la pierde.
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


# Maestro-detalle #6 (y #7) en el formulario de la encuesta: las preguntas
# en filas, y en cada fila sus opciones (una por línea, "etiqueta;valor").
# El 'orden' de cada pregunta lo pone su posición (nada que numerar a mano).
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
    # Regla 1: preguntas (y sus opciones) en el MISMO POST: la API guarda
    # todo en un solo CALL (sp_guardar_encuesta). Si una falla, no queda nada.
    filas = _filas('pre', CAMPOS_PREGUNTA_FILA, clave='enunciado')
    if filas:
        datos['preguntas'] = [_pregunta_de_fila(f, n) for n, f in enumerate(filas, start=1)]
    try:
        respuesta = crear('Encuesta', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, "vigente_hasta no puede ser anterior a vigente_desde" (400).
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return _render_encuesta_form('nueva', datos, ex.errores, filas)

    sufijo = f" con {len(filas)} pregunta{'' if len(filas) == 1 else 's'}" if filas else ''
    flash(f"Encuesta '{datos['nombre']}' creada{sufijo} (id {respuesta['id_encuesta']}).", 'exito')
    return redirect(url_for('admin.encuestas_detalle', id_encuesta=respuesta['id_encuesta']))


@admin_bp.route('/encuestas/<int:id_encuesta>')
def encuestas_detalle(id_encuesta):
    """Maestro-detalle #6: la encuesta con sus preguntas (en su orden)."""
    try:
        encuesta = obtener('Encuesta', id_encuesta)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.encuestas_listar'))

    preguntas = sorted(_lista_de('Pregunta', limite=500, fkEncuesta=id_encuesta),
                       key=lambda p: p['orden'])
    # Cuántas opciones tiene cada pregunta: UNA llamada a OpcionPregunta
    # (sin filtro) y se cuentan aquí, en vez de una llamada por pregunta.
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
    # Preguntas nuevas: cada una con su propio POST /api/Pregunta, que a su vez
    # guarda la pregunta y SUS opciones juntas (sp_guardar_pregunta, v6). El
    # orden sigue después de la última pregunta que ya tiene la encuesta.
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


RECURSOS_CON_ACTIVO = {'Sede', 'Facultad', 'Dependencia', 'ProgramaAcademico', 'Catedra'}


def _opciones(recurso):
    """Lista de un recurso para llenar un <select>. Si la API falla, se avisa
    y se devuelve [] (el formulario se muestra igual, sin opciones)."""
    # Estos 5 recursos solo devuelven las ACTIVAS por defecto (retrofit criterio 3).
    # Para un <select> se piden todas, como antes: si una fila apunta a un padre /
    # sede / facultad inactiva y la opción no existe, al guardar el formulario el
    # valor se perdería (null en el PUT).
    filtros = {'incluirInactivos': 'true'} if recurso in RECURSOS_CON_ACTIVO else {}
    try:
        return listar(recurso, **filtros)
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
        facultades = listar('Facultad', incluirInactivos=_ver_inactivas())
    except ErrorApi as ex:
        _avisar(ex)
        facultades = []
    return render_template('admin/facultad_lista.html', facultades=facultades, ver_inactivas=_ver_inactivas() is not None)


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
        flash(f"Facultad '{id_facultad}' desactivada (borrado lógico: sus hijas y programas siguen enlazados a ella; se reactiva con Editar).", 'exito')
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
        dependencias = listar('Dependencia', incluirInactivos=_ver_inactivas())
    except ErrorApi as ex:
        _avisar(ex)
        dependencias = []
    return render_template('admin/dependencia_lista.html', dependencias=dependencias, ver_inactivas=_ver_inactivas() is not None)


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
        flash(f"Dependencia '{id_dependencia}' desactivada (borrado lógico: sus hijas y cátedras siguen enlazadas a ella; se reactiva con Editar).", 'exito')
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


# Maestro-detalle #10 en el formulario del programa: los alias en filas.
CAMPOS_ALIAS_FILA = ('texto_normalizado',)


def _render_programa_form(modo, programa, errores, filas_alias=(), codigo=None):
    existentes = _alias_de(codigo) if modo == 'editar' and codigo else []
    return render_template('admin/programa_form.html', modo=modo, programa=programa, errores=errores,
                           facultades=_opciones('Facultad'), niveles=NIVELES_PROGRAMA,
                           filas_alias=list(filas_alias), alias_existentes=existentes)


@admin_bp.route('/programas')
def programas_listar():
    try:
        programas = listar('ProgramaAcademico', incluirInactivos=_ver_inactivas())
    except ErrorApi as ex:
        _avisar(ex)
        programas = []
    return render_template('admin/programa_lista.html', programas=programas, ver_inactivas=_ver_inactivas() is not None)


@admin_bp.route('/programas/nuevo', methods=['GET', 'POST'])
def programas_nuevo():
    if request.method == 'GET':
        return _render_programa_form('nueva', None, [])

    datos = _leer_form_programa()
    datos['codigo'] = request.form.get('codigo', '').strip()
    # Retrofit V2 (Guía IA, Regla 1): los alias de las FILAS del formulario
    # se mandan en el MISMO POST que el programa, así la API los guarda
    # juntos en una sola transacción (SP sp_guardar_programa_academico).
    filas = _filas('ali', CAMPOS_ALIAS_FILA, clave='texto_normalizado')
    datos['alias'] = [{'texto_normalizado': f['texto_normalizado']} for f in filas]
    try:
        crear('ProgramaAcademico', datos)
    except ErrorApi as ex:
        # Aquí llega, por ejemplo, el 422 de un código en minúscula, o el
        # 500 de un alias que ya existe (y entonces NO se creó nada: ni el
        # programa ni ningún alias).
        _avisar(ex)
        return _render_programa_form('nueva', datos, ex.errores, filas)

    cantidad = len(datos['alias'])
    sufijo = f" con {cantidad} alias" if cantidad else ''
    flash(f"Programa '{datos['codigo']}' creado{sufijo}.", 'exito')
    return redirect(url_for('admin.programas_detalle', codigo=datos['codigo']))


def _alias_de(codigo):
    """Los alias de UN programa. La API no filtra AliasPrograma por
    programa (RF80: solo '?limite='), así que se filtra aquí. La tabla es
    pequeña (un puñado de alias por programa), mismo criterio que
    '_ya_respondida' en routes/encuestas.py."""
    return [a for a in _lista_de('AliasPrograma', limite=5000) if a['fk_programa'] == codigo]


@admin_bp.route('/programas/<codigo>')
def programas_detalle(codigo):
    """Maestro-detalle #10: el programa con sus alias (nombres alternos)."""
    try:
        programa = obtener('ProgramaAcademico', codigo)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.programas_listar'))
    return render_template('admin/programa_detalle.html', programa=programa,
                           alias=sorted(_alias_de(codigo), key=lambda a: a['id_alias']))


@admin_bp.route('/programas/<codigo>/editar', methods=['GET', 'POST'])
def programas_editar(codigo):
    if request.method == 'GET':
        try:
            programa = obtener('ProgramaAcademico', codigo)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.programas_listar'))
        return _render_programa_form('editar', programa, [], codigo=codigo)

    datos = _leer_form_programa()
    filas = _filas('ali', CAMPOS_ALIAS_FILA, clave='texto_normalizado')
    try:
        reemplazar('ProgramaAcademico', codigo, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['codigo'] = codigo
        return _render_programa_form('editar', datos, ex.errores, filas, codigo)

    flash(f"Programa '{codigo}' actualizado.", 'exito')
    datos['codigo'] = codigo
    # Alias nuevos: cada uno con su POST /api/AliasPrograma (el programa ya existe).
    fallidas = _crear_detalles('AliasPrograma', filas,
                               lambda f, n: {'texto_normalizado': f['texto_normalizado'], 'fk_programa': codigo},
                               'Alias', 'alias')
    if fallidas:
        return _render_programa_form('editar', datos, [], fallidas, codigo)
    return redirect(url_for('admin.programas_detalle', codigo=codigo))


@admin_bp.route('/programas/<codigo>/eliminar', methods=['POST'])
def programas_eliminar(codigo):
    try:
        eliminar('ProgramaAcademico', codigo)
        flash(f"Programa '{codigo}' desactivado (borrado lógico: sus asistentes y alias se conservan; se reactiva con Editar).", 'exito')
    except ErrorApi as ex:
        # Ya no falla por tener asistentes: el borrado es lógico. Queda para errores de red/API.
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
        # Desde la pantalla del programa llega '?fk_programa=': queda preseleccionado.
        fk_programa = request.args.get('fk_programa')
        return render_template('admin/alias_form.html', errores=[],
                               alias={'fk_programa': fk_programa} if fk_programa else None,
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
    return _redirigir('admin.alias_listar')


@admin_bp.route('/alias-programa/<int:id_alias>/eliminar', methods=['POST'])
def alias_eliminar(id_alias):
    try:
        eliminar('AliasPrograma', id_alias)
        flash(f"Alias {id_alias} eliminado.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return _redirigir('admin.alias_listar')


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


# Maestro-detalle #1 en el formulario: las sesiones de la cátedra.
CAMPOS_SESION_FILA = ('titulo', 'inicio', 'fin', 'fk_modalidad', 'fk_sede', 'fk_periodo',
                      'fk_encuesta', 'lugar', 'enlace_virtual')


def _sesion_de_fila(fila):
    """Una fila del formulario -> una sesión para la API (sin 'fk_catedra':
    al crear la fija el SP; al editar se agrega aparte). Las horas del
    datetime-local se envían con la zona de Colombia (ADR D21)."""
    return {
        'titulo': fila['titulo'] or None,
        'inicio': desde_input(fila['inicio']),
        'fin': desde_input(fila['fin']),
        'fk_modalidad': fila['fk_modalidad'],
        'fk_sede': fila['fk_sede'],
        'fk_periodo': fila['fk_periodo'],
        'fk_encuesta': _entero_o_none(fila['fk_encuesta']),
        'lugar': fila['lugar'] or None,
        'enlace_virtual': fila['enlace_virtual'] or None,     # v7.1 · D59: enlace de ingreso
    }


def _render_catedra_form(modo, catedra, errores, filas_sesiones=(), sesiones_existentes=None, choques=()):
    return render_template('admin/catedra_form.html', modo=modo, catedra=catedra, errores=errores, choques=list(choques),
                           tipos_evento=_opciones('TipoEvento'), dependencias=_opciones('Dependencia'),
                           filas_sesiones=list(filas_sesiones), sesiones_existentes=sesiones_existentes or [],
                           **_catalogos_sesion())


@admin_bp.route('/catedras')
def catedras_listar():
    texto = request.args.get('q', '').strip()
    try:
        pagina = max(1, int(request.args.get('pagina', 1)))
    except ValueError:
        pagina = 1

    try:
        catedras = listar('Catedra', timeout=30, incluirInactivos=_ver_inactivas())
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
    # El ADMIN ve la bandeja de ponentes en su panel; el ASISTENTE (que no tiene panel) la ve aquí arriba.
    bandeja = None if tiene_rol(ROL_ADMIN) else {'pendientes': contar_pendientes()}
    # El ADMIN ve la bandeja de ponentes en su panel; el ASISTENTE (que no tiene panel) la ve aquí arriba.
    bandeja = None if tiene_rol(ROL_ADMIN) else {'pendientes': contar_pendientes()}
    return render_template('admin/catedra_lista.html', catedras=catedras[corte:corte + CATEDRAS_POR_PAGINA],
                           q=texto, pagina=pagina, total_paginas=total_paginas, total=total,
                           ver_inactivas=_ver_inactivas() is not None, bandeja=bandeja)
                           ver_inactivas=_ver_inactivas() is not None, bandeja=bandeja)


@admin_bp.route('/catedras/nueva', methods=['GET', 'POST'])
def catedras_nueva():
    if request.method == 'GET':
        return _render_catedra_form('nueva', None, [])

    datos = _leer_form_catedra()
    # Regla 1: las sesiones escritas en el formulario viajan en el MISMO POST
    # y la API guarda cátedra + sesiones en un solo CALL (sp_guardar_catedra).
    filas = _filas('ses', CAMPOS_SESION_FILA, clave='inicio')
    if filas:
        # v7.1 · D52/D55: que ninguna sesión se cruce con otra en el mismo salón (si no se puede consultar, no se crea nada).
        try:
            choques = _choques_del_formulario(filas)
        except ErrorApi as ex:
            flash('No se pudo verificar la disponibilidad del horario, así que no se creó nada. '
                  f"Intenta de nuevo en un momento. {ex.detalle or ''}".strip(), 'error')
            return _render_catedra_form('nueva', datos, [], filas)
        if choques:
            # Fijos en la página (no flash: un flash de error ya no se borra, pero esto es una lista larga).
            return _render_catedra_form('nueva', datos, [], filas, choques=choques)
        datos['sesiones'] = [_sesion_de_fila(f) for f in filas]
    try:
        respuesta = crear('Catedra', datos)
    except ErrorApi as ex:
        # Si UNA sesión falla, no se creó nada: vuelve todo lo escrito.
        _avisar(ex)
        return _render_catedra_form('nueva', datos, ex.errores, filas)

    sufijo = f" con {len(filas)} sesion{'' if len(filas) == 1 else 'es'}" if filas else ''
    flash(f"Cátedra creada con id {respuesta['id_catedra']}{sufijo} "
          f"(nombre ASIS: '{respuesta['nombre_asis']}').", 'exito')
    if filas:
        # v7.1 · D53: cada sesión nace con su QR de cierre (se ve en «QR de asistencia» de la sesión).
        motivos_qr = []
        qr_hechos, qr_total = asegurar_qr_de_catedra(respuesta['id_catedra'], usuario_actual().get('id_usuario'), motivos_qr)
        # v7.2 · D72: si una sesión presencial está en una sede sin coordenadas, el aviso dice qué sede completar.
        avisar_qr_pendientes(flash, qr_hechos, qr_total, motivos_qr)
        motivos_qr = []
        qr_hechos, qr_total = asegurar_qr_de_catedra(respuesta['id_catedra'], usuario_actual().get('id_usuario'), motivos_qr)
        # v7.2 · D72: si una sesión presencial está en una sede sin coordenadas, el aviso dice qué sede completar.
        avisar_qr_pendientes(flash, qr_hechos, qr_total, motivos_qr)
    return redirect(url_for('admin.catedras_detalle', id_catedra=respuesta['id_catedra']))


@admin_bp.route('/catedras/<int:id_catedra>')
def catedras_detalle(id_catedra):
    """Maestro-detalle #1: la cátedra con sus sesiones (la relación
    principal del sistema; 'numero_reunion' no se repite dentro de ella,
    uq_sesion_reunion)."""
    try:
        catedra = obtener('Catedra', id_catedra)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.catedras_listar'))
    # La API filtra por cátedra en SQL (?fkCatedra=): llegan solo las suyas.
    sesiones = sorted(_lista_de('Sesion', limite=1000, fkCatedra=id_catedra),
                      key=lambda s: s['numero_reunion'])
    return render_template('admin/catedra_detalle.html', catedra=catedra, sesiones=sesiones)


@admin_bp.route('/catedras/<int:id_catedra>/editar', methods=['GET', 'POST'])
def catedras_editar(id_catedra):
    existentes = sorted(_lista_de('Sesion', limite=1000, fkCatedra=id_catedra),
                        key=lambda s: s['numero_reunion'])
    if request.method == 'GET':
        try:
            catedra = obtener('Catedra', id_catedra)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.catedras_listar'))
        return _render_catedra_form('editar', catedra, [], sesiones_existentes=existentes)

    datos = _leer_form_catedra()
    filas = _filas('ses', CAMPOS_SESION_FILA, clave='inicio')
    try:
        reemplazar('Catedra', id_catedra, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_catedra'] = id_catedra
        return _render_catedra_form('editar', datos, ex.errores, filas, existentes)

    flash(f"Cátedra {id_catedra} actualizada.", 'exito')
    # Sesiones nuevas: cada una con su propio POST /api/Sesion (la cátedra ya existe).
    fallidas = _crear_detalles('Sesion', filas,
                               lambda f, n: {**_sesion_de_fila(f), 'fk_catedra': id_catedra},
                               'Sesión', 'sesiones')
    if fallidas:
        datos['id_catedra'] = id_catedra
        return _render_catedra_form('editar', datos, [], fallidas,
                                    _lista_de('Sesion', limite=1000, fkCatedra=id_catedra))
    return redirect(url_for('admin.catedras_detalle', id_catedra=id_catedra))


@admin_bp.route('/catedras/<int:id_catedra>/eliminar', methods=['POST'])
def catedras_eliminar(id_catedra):
    try:
        eliminar('Catedra', id_catedra)
        flash(f"Cátedra {id_catedra} desactivada (borrado lógico: sus sesiones se conservan; se reactiva con Editar).", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.catedras_listar'))


# ==========================================================================
#  v3 · ASISTENTE  (recurso API: "Asistente", clave: id_asistente numérica)
# --------------------------------------------------------------------------
#  ADR D18: con 14.811 filas, el buscador y la paginación los hace la API
#  ('?q=' y '?pagina='), NO este front. Cada búsqueda pide solo la página
#  visible; nunca se descarga la tabla completa (a diferencia de Cátedra,
#  D13 de la v2, que sí filtra en Python).
#
#  Los detalles del asistente (documentos, vinculaciones, programas y
#  consentimientos) se crean DESDE la ficha del asistente: así el
#  'fk_asistente' nunca se digita (Guía IA, Regla 2) — lo fija la URL.
# ==========================================================================

ASISTENTES_POR_PAGINA = 50
ESTADOS_PROGRAMA = ['ACTIVO', 'RETIRADO', 'GRADUADO', 'APLAZADO']
CANALES_CONSENTIMIENTO = ['WEB', 'PRESENCIAL', 'CORREO', 'IMPORTADO']


def _entero_o_none(texto):
    """'12' -> 12; vacío o no numérico -> None."""
    try:
        return int(texto)
    except (TypeError, ValueError):
        return None


def _catalogos_asistente():
    """Los catálogos (v1/v2) que llenan los <select> de los detalles."""
    return {
        'tipos_documento': _opciones('TipoDocumento'),
        'tipos_vinculacion': _opciones('TipoVinculacion'),
        'programas': _opciones('ProgramaAcademico'),
        'periodos': _opciones('PeriodoAcademico'),
        'estados_programa': ESTADOS_PROGRAMA,
        'canales': CANALES_CONSENTIMIENTO,
    }


def _leer_form_asistente():
    """Columnas propias del asistente (las mismas en el alta y en el PUT)."""
    return {
        'id_asis': _texto_o_none('id_asis'),
        'nombres': request.form.get('nombres', '').strip(),
        'apellidos': request.form.get('apellidos', '').strip(),
        'nombre_completo_asis': _texto_o_none('nombre_completo_asis'),
        'correo_institucional': _texto_o_none('correo_institucional'),
        'correo_personal': _texto_o_none('correo_personal'),
        'celular': _texto_o_none('celular'),
        'es_externo': request.form.get('es_externo') == 'on',
        'activo': request.form.get('activo') == 'on',
    }


# Maestro-detalle #4 y #5 en el formulario del asistente: documentos en
# filas (varios a la vez) y el consentimiento (casilla). Al crear viajan en el
# MISMO POST (sp_guardar_asistente, v3); al editar, cada uno aparte.
CAMPOS_DOCUMENTO_FILA = ('fk_tipo_documento', 'numero', 'vigente')


def _documento_de_fila(fila):
    return {'fk_tipo_documento': fila['fk_tipo_documento'], 'numero': fila['numero'],
            'vigente': fila['vigente'] != 'no'}


def _consentimiento_del_formulario():
    """Lo que pide la casilla "aceptó la política" (o None si no está marcada)."""
    if request.form.get('consentimiento') != 'on':
        return None
    return {'version_politica': request.form.get('version_politica', '').strip(),
            'canal': request.form.get('canal') or 'WEB'}


def _render_asistente_form(modo, asistente, errores, filas_documentos=(), id_asistente=None):
    documentos, consentimientos = [], []
    if modo == 'editar' and id_asistente:
        documentos = _lista_de('DocumentoAsistente', fkAsistente=id_asistente)
        consentimientos = _lista_de('ConsentimientoDatos', fkAsistente=id_asistente)
    return render_template('admin/asistente_form.html', modo=modo, asistente=asistente, errores=errores,
                           filas_documentos=list(filas_documentos), documentos_existentes=documentos,
                           consentimientos_existentes=consentimientos, **_catalogos_asistente())


def _volver_a_asistente(fk_asistente, respaldo):
    """Tras una acción sobre un detalle: a la ficha del asistente si se
    conoce, o al listado 'respaldo' si no."""
    if fk_asistente:
        return redirect(url_for('admin.asistentes_detalle', id_asistente=fk_asistente))
    return redirect(url_for(respaldo))


@admin_bp.route('/asistentes')
def asistentes_listar():
    texto = request.args.get('q', '').strip()
    pagina = max(1, _entero_o_none(request.args.get('pagina')) or 1)

    try:
        # La API filtra (ILIKE) y pagina (LIMIT/OFFSET) en SQL.
        asistentes = listar('Asistente', limite=ASISTENTES_POR_PAGINA,
                            q=texto or None, pagina=pagina)
    except ErrorApi as ex:
        _avisar(ex)
        asistentes = []

    # Sin pedir un conteo total: si la página llegó llena, puede haber otra.
    hay_siguiente = len(asistentes) == ASISTENTES_POR_PAGINA
    return render_template('admin/asistente_lista.html', asistentes=asistentes, q=texto,
                           pagina=pagina, hay_siguiente=hay_siguiente)


@admin_bp.route('/asistentes/nuevo', methods=['GET', 'POST'])
def asistentes_nuevo():
    if request.method == 'GET':
        return _render_asistente_form('nueva', None, [])

    datos = _leer_form_asistente()

    # Guía IA, Regla 1: los detalles iniciales viajan en el MISMO POST y la
    # API los guarda junto con el asistente (sp_guardar_asistente). Cada
    # bloque se agrega solo si el formulario lo trae lleno. Los documentos
    # llegan en FILAS (pueden ser varios; solo uno vigente: ux_documento_vigente).
    filas_documentos = _filas('doc', CAMPOS_DOCUMENTO_FILA, clave='numero')
    if filas_documentos:
        datos['documentos'] = [_documento_de_fila(f) for f in filas_documentos]
    if request.form.get('vinc_tipo'):
        vinculacion = {'fk_tipo_vinculacion': request.form['vinc_tipo']}
        if request.form.get('vinc_fecha_ini'):
            vinculacion['fecha_ini'] = request.form['vinc_fecha_ini']
        datos['vinculaciones'] = [vinculacion]
    if request.form.get('prog_codigo') and request.form.get('prog_periodo'):
        datos['programas'] = [{'fk_programa': request.form['prog_codigo'],
                               'fk_periodo': request.form['prog_periodo']}]
    consentimiento = _consentimiento_del_formulario()
    if consentimiento is not None:
        datos['consentimiento'] = consentimiento

    try:
        respuesta = crear('Asistente', datos)
    except ErrorApi as ex:
        # 400 (id_asis/es_externo, correos), 422 (forma) o 500: si fue un
        # detalle el que falló, NO se creó nada (ni el asistente).
        _avisar(ex)
        return _render_asistente_form('nueva', datos, ex.errores, filas_documentos)

    flash(f"Persona creada con id {respuesta['id_asistente']}.", 'exito')
    flash(f"Persona creada con id {respuesta['id_asistente']}.", 'exito')
    return redirect(url_for('admin.asistentes_detalle', id_asistente=respuesta['id_asistente']))


@admin_bp.route('/asistentes/<int:id_asistente>')
def asistentes_detalle(id_asistente):
    """Ficha del asistente con sus 4 tablas de detalle."""
    try:
        asistente = obtener('Asistente', id_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.asistentes_listar'))

    detalles = {}
    for clave, recurso in (('documentos', 'DocumentoAsistente'), ('vinculaciones', 'VinculacionAsistente'),
                           ('programas', 'ProgramaAsistente'), ('consentimientos', 'ConsentimientoDatos')):
        try:
            detalles[clave] = listar(recurso, fkAsistente=id_asistente)
        except ErrorApi as ex:
            _avisar(ex)
            detalles[clave] = []

    return render_template('admin/asistente_detalle.html', asistente=asistente,
                           estados_programa=ESTADOS_PROGRAMA, **detalles)


@admin_bp.route('/asistentes/<int:id_asistente>/editar', methods=['GET', 'POST'])
def asistentes_editar(id_asistente):
    if request.method == 'GET':
        try:
            asistente = obtener('Asistente', id_asistente)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.asistentes_listar'))
        return _render_asistente_form('editar', asistente, [], id_asistente=id_asistente)

    datos = _leer_form_asistente()
    filas = _filas('doc', CAMPOS_DOCUMENTO_FILA, clave='numero')
    try:
        reemplazar('Asistente', id_asistente, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_asistente'] = id_asistente
        return _render_asistente_form('editar', datos, ex.errores, filas, id_asistente)

    flash(f"Persona {id_asistente} actualizada.", 'exito')
    flash(f"Persona {id_asistente} actualizada.", 'exito')
    datos['id_asistente'] = id_asistente

    # Detalles nuevos: el asistente ya existe -> cada uno aparte.
    fallidas = _crear_detalles('DocumentoAsistente', filas,
                               lambda f, n: {**_documento_de_fila(f), 'fk_asistente': id_asistente},
                               'Documento', 'documentos')
    hubo_error = False
    consentimiento = _consentimiento_del_formulario()
    if consentimiento is not None:
        try:
            crear('ConsentimientoDatos', {**consentimiento, 'fk_asistente': id_asistente,
                                          'ip': request.remote_addr})
            flash('Consentimiento registrado.', 'exito')
        except ErrorApi as ex:
            _avisar(ex)
            hubo_error = True
    if fallidas or hubo_error:
        return _render_asistente_form('editar', datos, [], fallidas, id_asistente)
    return redirect(url_for('admin.asistentes_detalle', id_asistente=id_asistente))


@admin_bp.route('/asistentes/<int:id_asistente>/eliminar', methods=['POST'])
def asistentes_eliminar(id_asistente):
    try:
        eliminar('Asistente', id_asistente)
        # Retrofit criterio 3 (Fase 4): el borrado es LÓGICO (activo = false).
        # Ya no hay CASCADE de sus detalles ni 500 por RESTRICT: el asistente
        # queda inactivo, con toda su historia, y sale de la lista.
        flash(f"Persona {id_asistente} desactivada (borrado lógico: sus documentos, "
        flash(f"Persona {id_asistente} desactivada (borrado lógico: sus documentos, "
              f"vinculaciones, programas, consentimientos y registros se conservan).", 'exito')
    except ErrorApi as ex:
        # 404 si no existe o ya estaba inactivo.
        _avisar(ex)
        return redirect(url_for('admin.asistentes_detalle', id_asistente=id_asistente))
    return redirect(url_for('admin.asistentes_listar'))


# ==========================================================================
#  v3 · DOCUMENTO DEL ASISTENTE  (recurso API: "DocumentoAsistente")
#  CRUD completo. "Un solo vigente por persona" lo vigila la BD: para
#  cambiar el vigente, primero se retira el anterior (vigente = false).
# ==========================================================================

@admin_bp.route('/documentos')
def documentos_listar():
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    try:
        documentos = listar('DocumentoAsistente', limite=200, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        documentos = []
    return render_template('admin/documento_lista.html', documentos=documentos, fk_asistente=fk_asistente)


@admin_bp.route('/asistentes/<int:id_asistente>/documentos/nuevo', methods=['GET', 'POST'])
def documentos_nuevo(id_asistente):
    if request.method == 'GET':
        return render_template('admin/detalle_asistente_form.html', tipo='documento', id_asistente=id_asistente,
                               dato=None, errores=[], **_catalogos_asistente())

    datos = {
        'fk_asistente': id_asistente,   # lo fija la URL, no se digita
        'fk_tipo_documento': request.form.get('fk_tipo_documento', ''),
        'numero': request.form.get('numero', '').strip(),
        'vigente': request.form.get('vigente') == 'on',
    }
    try:
        crear('DocumentoAsistente', datos)
    except ErrorApi as ex:
        # 500 típico: ya tiene un documento vigente (ux_documento_vigente).
        _avisar(ex)
        return render_template('admin/detalle_asistente_form.html', tipo='documento', id_asistente=id_asistente,
                               dato=datos, errores=ex.errores, **_catalogos_asistente())

    flash('Documento registrado.', 'exito')
    return redirect(url_for('admin.asistentes_detalle', id_asistente=id_asistente))


@admin_bp.route('/documentos/<int:id_documento>/editar', methods=['GET', 'POST'])
def documentos_editar(id_documento):
    try:
        documento = obtener('DocumentoAsistente', id_documento)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.documentos_listar'))

    if request.method == 'GET':
        return render_template('admin/detalle_asistente_form.html', tipo='documento_editar',
                               id_asistente=documento['fk_asistente'], dato=documento, errores=[],
                               **_catalogos_asistente())

    datos = {
        'fk_tipo_documento': request.form.get('fk_tipo_documento', ''),
        'numero': request.form.get('numero', '').strip(),
        'vigente': request.form.get('vigente') == 'on',
    }
    try:
        reemplazar('DocumentoAsistente', id_documento, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_documento'] = id_documento
        return render_template('admin/detalle_asistente_form.html', tipo='documento_editar',
                               id_asistente=documento['fk_asistente'], dato=datos, errores=ex.errores,
                               **_catalogos_asistente())

    flash(f"Documento {id_documento} actualizado.", 'exito')
    return redirect(url_for('admin.asistentes_detalle', id_asistente=documento['fk_asistente']))


@admin_bp.route('/documentos/<int:id_documento>/vigencia', methods=['POST'])
def documentos_vigencia(id_documento):
    """Cambia SOLO 'vigente' (PATCH): el paso previo a registrar un documento nuevo."""
    fk_asistente = _entero_o_none(request.form.get('fk_asistente'))
    vigente = request.form.get('vigente') == 'true'
    try:
        actualizar_parcial('DocumentoAsistente', id_documento, {'vigente': vigente})
        flash(f"Documento {id_documento} marcado como {'vigente' if vigente else 'no vigente'}.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return _volver_a_asistente(fk_asistente, 'admin.documentos_listar')


@admin_bp.route('/documentos/<int:id_documento>/eliminar', methods=['POST'])
def documentos_eliminar(id_documento):
    fk_asistente = _entero_o_none(request.form.get('fk_asistente'))
    try:
        eliminar('DocumentoAsistente', id_documento)
        flash(f"Documento {id_documento} eliminado.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return _volver_a_asistente(fk_asistente, 'admin.documentos_listar')


# ==========================================================================
#  v3 · VINCULACIÓN DEL ASISTENTE  (recurso API: "VinculacionAsistente")
#  LLAVE COMPUESTA (ADR D16): no hay "editar" ni "eliminar"; solo abrir y
#  CERRAR. El cierre manda la llave completa (asistente, tipo, fecha_ini).
# ==========================================================================

@admin_bp.route('/vinculaciones')
def vinculaciones_listar():
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    try:
        vinculaciones = listar('VinculacionAsistente', limite=200, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        vinculaciones = []
    return render_template('admin/vinculacion_lista.html', vinculaciones=vinculaciones, fk_asistente=fk_asistente)


@admin_bp.route('/asistentes/<int:id_asistente>/vinculaciones/nueva', methods=['GET', 'POST'])
def vinculaciones_nueva(id_asistente):
    if request.method == 'GET':
        return render_template('admin/detalle_asistente_form.html', tipo='vinculacion', id_asistente=id_asistente,
                               dato=None, errores=[], **_catalogos_asistente())

    datos = {'fk_asistente': id_asistente,
             'fk_tipo_vinculacion': request.form.get('fk_tipo_vinculacion', '')}
    if request.form.get('fecha_ini'):          # sin fecha -> la API usa hoy
        datos['fecha_ini'] = request.form['fecha_ini']
    try:
        crear('VinculacionAsistente', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/detalle_asistente_form.html', tipo='vinculacion', id_asistente=id_asistente,
                               dato=datos, errores=ex.errores, **_catalogos_asistente())

    flash('Vinculación abierta.', 'exito')
    return redirect(url_for('admin.asistentes_detalle', id_asistente=id_asistente))


@admin_bp.route('/vinculaciones/cerrar', methods=['POST'])
def vinculaciones_cerrar():
    fk_asistente = _entero_o_none(request.form.get('fk_asistente'))
    llave = {
        'fk_asistente': fk_asistente,
        'fk_tipo_vinculacion': request.form.get('fk_tipo_vinculacion', ''),
        'fecha_ini': request.form.get('fecha_ini', ''),
    }
    if request.form.get('fecha_fin'):          # sin fecha -> la API usa hoy
        llave['fecha_fin'] = request.form['fecha_fin']
    try:
        actualizar_parcial('VinculacionAsistente', 'cerrar', llave)
        flash(f"Vinculación '{llave['fk_tipo_vinculacion']}' cerrada.", 'exito')
    except ErrorApi as ex:
        # 400 si ya estaba cerrada o la fecha de cierre es anterior al inicio.
        _avisar(ex)
    return _volver_a_asistente(fk_asistente, 'admin.vinculaciones_listar')


# ==========================================================================
#  v3 · PROGRAMA DEL ASISTENTE  (recurso API: "ProgramaAsistente")
#  LLAVE COMPUESTA (ADR D16): matricular y CAMBIAR ESTADO; sin editar ni
#  eliminar.
# ==========================================================================

@admin_bp.route('/programas-asistente')
def programas_asistente_listar():
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    try:
        programas = listar('ProgramaAsistente', limite=200, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        programas = []
    return render_template('admin/programa_asistente_lista.html', programas=programas,
                           fk_asistente=fk_asistente, estados_programa=ESTADOS_PROGRAMA)


@admin_bp.route('/asistentes/<int:id_asistente>/programas/nuevo', methods=['GET', 'POST'])
def programas_asistente_nuevo(id_asistente):
    if request.method == 'GET':
        return render_template('admin/detalle_asistente_form.html', tipo='programa', id_asistente=id_asistente,
                               dato=None, errores=[], **_catalogos_asistente())

    datos = {
        'fk_asistente': id_asistente,
        'fk_programa': request.form.get('fk_programa', ''),
        'fk_periodo': request.form.get('fk_periodo', ''),
        'estado': request.form.get('estado') or 'ACTIVO',
    }
    try:
        crear('ProgramaAsistente', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/detalle_asistente_form.html', tipo='programa', id_asistente=id_asistente,
                               dato=datos, errores=ex.errores, **_catalogos_asistente())

    flash('Matrícula registrada.', 'exito')
    return redirect(url_for('admin.asistentes_detalle', id_asistente=id_asistente))


@admin_bp.route('/programas-asistente/cambiar-estado', methods=['POST'])
def programas_asistente_cambiar_estado():
    fk_asistente = _entero_o_none(request.form.get('fk_asistente'))
    datos = {
        'fk_asistente': fk_asistente,
        'fk_programa': request.form.get('fk_programa', ''),
        'fk_periodo': request.form.get('fk_periodo', ''),
        'estado': request.form.get('estado', ''),
    }
    try:
        actualizar_parcial('ProgramaAsistente', 'cambiar-estado', datos)
        flash(f"Matrícula en '{datos['fk_programa']}' ({datos['fk_periodo']}) ahora en {datos['estado']}.", 'exito')
    except ErrorApi as ex:
        # 400 si ya tenía ese estado.
        _avisar(ex)
    return _volver_a_asistente(fk_asistente, 'admin.programas_asistente_listar')


# ==========================================================================
#  v3 · CONSENTIMIENTO DE DATOS  (recurso API: "ConsentimientoDatos")
#  Ley 1581: solo se REGISTRA. Sin editar ni eliminar (la API no lo permite).
# ==========================================================================

@admin_bp.route('/consentimientos')
def consentimientos_listar():
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    try:
        consentimientos = listar('ConsentimientoDatos', limite=200, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        consentimientos = []
    return render_template('admin/consentimiento_lista.html', consentimientos=consentimientos,
                           fk_asistente=fk_asistente)


@admin_bp.route('/asistentes/<int:id_asistente>/consentimientos/nuevo', methods=['GET', 'POST'])
def consentimientos_nuevo(id_asistente):
    if request.method == 'GET':
        return render_template('admin/detalle_asistente_form.html', tipo='consentimiento', id_asistente=id_asistente,
                               dato=None, errores=[], **_catalogos_asistente())

    datos = {
        'fk_asistente': id_asistente,
        'version_politica': request.form.get('version_politica', '').strip(),
        'canal': request.form.get('canal') or 'WEB',
    }
    # La IP: la que escriba el administrador, o la de quien usa el formulario.
    datos['ip'] = _texto_o_none('ip') or request.remote_addr
    try:
        crear('ConsentimientoDatos', datos)
    except ErrorApi as ex:
        # 500 típico: esa versión de la política ya estaba aceptada.
        _avisar(ex)
        return render_template('admin/detalle_asistente_form.html', tipo='consentimiento', id_asistente=id_asistente,
                               dato=datos, errores=ex.errores, **_catalogos_asistente())

    flash('Consentimiento registrado.', 'exito')
    return redirect(url_for('admin.asistentes_detalle', id_asistente=id_asistente))


# ==========================================================================
#  v3 · LOTES DE CARGA Y SUS NOVEDADES  (solo lectura en el panel)
#  Un lote se ABRE y se CIERRA desde la pantalla de carga de datos
#  (routes/carga.py); aquí solo se consulta la evidencia que dejó.
# ==========================================================================

@admin_bp.route('/lotes')
def lotes_listar():
    try:
        lotes = listar('LoteCargaAsistente', limite=200)
    except ErrorApi as ex:
        _avisar(ex)
        lotes = []
    return render_template('admin/lote_lista.html', lotes=lotes)


@admin_bp.route('/lotes/<int:id_lote_carga>')
def lotes_detalle(id_lote_carga):
    try:
        lote = obtener('LoteCargaAsistente', id_lote_carga)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.lotes_listar'))
    try:
        novedades = listar('NovedadCarga', fkLoteCarga=id_lote_carga)
    except ErrorApi as ex:
        _avisar(ex)
        novedades = []
    return render_template('admin/lote_detalle.html', lote=lote, novedades=novedades)


@admin_bp.route('/novedades')
def novedades_listar():
    try:
        novedades = listar('NovedadCarga', limite=500)
    except ErrorApi as ex:
        _avisar(ex)
        novedades = []
    return render_template('admin/novedad_lista.html', novedades=novedades)


# ==========================================================================
#  v4 · SESIÓN  (recurso API: "Sesion", clave: id_sesion numérica)
# --------------------------------------------------------------------------
#  CRUD completo. Tres decisiones de esta pantalla:
#   · 'fk_catedra' NO se digita ni sale de un <select> de 5502 cátedras: lo
#     fija la URL (?fk_catedra=), igual que 'fk_asistente' en la v3 (Regla 2
#     de la guía IA). Se llega desde el botón "Sesiones" de cada cátedra.
#   · 'numero_reunion' en el alta se deja VACÍO por defecto: la BD pone el
#     consecutivo (ADR D20). En la edición es obligatorio (en un UPDATE el
#     trigger no corre).
#   · Las horas del formulario (datetime-local, sin zona) se envían con la
#     zona de Colombia (-05:00); la API las guarda en UTC (ADR D21).
#  "fin > inicio" NO se revisa aquí: lo valida la API (400) y se muestra.
# ==========================================================================

ESTADOS_SESION = ['PROGRAMADA', 'ABIERTA', 'CERRADA', 'MIGRADA']
SESIONES_LIMITE_LISTA = 200


def _catalogos_sesion():
    """Catálogos de la v1 que llenan los <select> del formulario de sesión."""
    return {
        'modalidades': _opciones('Modalidad'),
        'sedes': _opciones('Sede'),
        'periodos': _opciones('PeriodoAcademico'),
        'encuestas': _opciones('Encuesta'),
        'estados': ESTADOS_SESION,
    }


def _leer_form_sesion():
    """Campos del formulario -> body de la API (los vacíos van como null)."""
    return {
        'numero_reunion': _entero_o_none(request.form.get('numero_reunion')),
        'numero_reunion_asis': _entero_o_none(request.form.get('numero_reunion_asis')),
        'titulo': _texto_o_none('titulo'),
        'inicio': desde_input(request.form.get('inicio')),
        'fin': desde_input(request.form.get('fin')),
        'fk_modalidad': request.form.get('fk_modalidad', ''),
        'fk_sede': request.form.get('fk_sede', ''),
        'fk_periodo': request.form.get('fk_periodo', ''),
        'fk_encuesta': _entero_o_none(request.form.get('fk_encuesta')),
        'lugar': _texto_o_none('lugar'),
        'enlace_virtual': _texto_o_none('enlace_virtual'),
        'estado': request.form.get('estado') or 'PROGRAMADA',
    }


# Maestro-detalle #2 y #3 en el formulario de la sesión:
#  · enlace de registro: casilla "emitir" (al crear, en el MISMO CALL:
#    sp_guardar_sesion; al editar, POST /api/EnlaceRegistro aparte).
#  · registros de asistencia: filas, SOLO al editar (la asistencia ocurre
#    cuando la sesión ya existe). La persona se busca y se elige (Regla 2).
CAMPOS_REGISTRO_FILA = ('fk_asistente', 'texto_asistente', 'origen')


def _enlace_del_formulario():
    """Lo que pide la casilla "emitir enlace" (o None si no está marcada).
    'fk_usuario' = el administrador con la sesión abierta (Regla 2)."""
    if request.form.get('emitir_enlace') != 'on':
        return None
    enlace = {'fk_usuario': _id_admin_actual()}
    for campo in ('minutos_antes', 'minutos_despues'):     # vacío -> el del parámetro
        valor = _entero_o_none(request.form.get(campo))
        if valor is not None:
            enlace[campo] = valor
    return {k: v for k, v in enlace.items() if v is not None}


def _render_sesion_form(modo, sesion, catedra, errores, filas_registros=(), id_sesion=None,
                        emitir_marcado=False):
    # Al editar se muestra lo que la sesión YA tiene (solo lectura aquí).
    enlaces, registros = [], []
    if modo == 'editar' and id_sesion:
        enlaces = _preparar_enlaces(_lista_de('EnlaceRegistro', limite=500, fkSesion=id_sesion))
        registros = _lista_de('RegistroAsistencia', limite=5000, fkSesion=id_sesion)
    return render_template('admin/sesion_form.html', modo=modo, sesion=sesion, catedra=catedra,
                           errores=errores, filas_registros=list(filas_registros),
                           enlaces_existentes=enlaces, registros_existentes=registros,
                           # La casilla "emitir enlace" vuelve marcada SOLO si ese enlace
                           # todavía no se emitió (si no, reenviar emitiría otro).
                           emitir_marcado=emitir_marcado,
                           minutos_antes=request.form.get('minutos_antes', ''),
                           minutos_despues=request.form.get('minutos_despues', ''),
                           **_catalogos_sesion())


def _catedra_o_none(id_catedra):
    """La cátedra (para mostrar su nombre), o None si no se pudo leer."""
    if not id_catedra:
        return None
    try:
        return obtener('Catedra', id_catedra)
    except ErrorApi as ex:
        _avisar(ex)
        return None


@admin_bp.route('/sesiones')
def sesiones_listar():
    fk_catedra = _entero_o_none(request.args.get('fk_catedra'))
    try:
        # Con 5714 sesiones, la API filtra por cátedra en SQL (?fkCatedra=).
        sesiones = listar('Sesion', limite=SESIONES_LIMITE_LISTA, fkCatedra=fk_catedra)
    except ErrorApi as ex:
        _avisar(ex)
        sesiones = []
    return render_template('admin/sesion_lista.html', sesiones=sesiones, fk_catedra=fk_catedra,
                           catedra=_catedra_o_none(fk_catedra), limite=SESIONES_LIMITE_LISTA)


@admin_bp.route('/sesiones/nueva', methods=['GET', 'POST'])
def sesiones_nueva():
    fk_catedra = _entero_o_none(request.values.get('fk_catedra'))
    if not fk_catedra:
        flash('Elige primero la cátedra: entra a la cátedra (lista de cátedras) y usa "Nueva sesión".', 'error')
        return redirect(url_for('admin.catedras_listar'))
    catedra = _catedra_o_none(fk_catedra)

    if request.method == 'GET':
        return _render_sesion_form('nueva', {'fk_catedra': fk_catedra}, catedra, [])

    datos = _leer_form_sesion()
    datos['fk_catedra'] = fk_catedra          # lo fija la URL, no se digita
    if datos['numero_reunion'] is None:
        del datos['numero_reunion']            # ausente -> la BD pone el consecutivo
    # Regla 1: si se marcó "emitir enlace", viaja en el MISMO POST y la API
    # crea sesión + enlace en un solo CALL (sp_guardar_sesion).
    enlace = _enlace_del_formulario()
    if enlace is not None:
        datos['enlace'] = enlace
    try:
        respuesta = crear('Sesion', datos)
    except ErrorApi as ex:
        # 400 (fin <= inicio), 422 (forma) o 500 (FK, número repetido).
        # Si el enlace falló, tampoco quedó la sesión.
        _avisar(ex)
        return _render_sesion_form('nueva', datos, catedra, ex.errores, emitir_marcado=enlace is not None)

    flash(f"Sesión creada con id {respuesta['id_sesion']}: la base de datos le asignó "
          f"el número de reunión {respuesta['numero_reunion']}"
          + (" y emitió su enlace de registro." if enlace is not None else "."), 'exito')
    return redirect(url_for('admin.sesiones_detalle', id_sesion=respuesta['id_sesion']))


@admin_bp.route('/sesiones/<int:id_sesion>')
def sesiones_detalle(id_sesion):
    """Maestro-detalle #2 y #3: la sesión con sus enlaces de registro y sus
    registros de asistencia (y, de cada registro, si ya respondió la
    encuesta: #8). Desde aquí se emite / revoca el enlace y se registra
    asistencia a mano; todo regresa a esta misma pantalla ('volver')."""
    try:
        sesion = obtener('Sesion', id_sesion)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.sesiones_listar'))

    enlaces = _preparar_enlaces(_lista_de('EnlaceRegistro', limite=500, fkSesion=id_sesion))
    registros = _lista_de('RegistroAsistencia', limite=5000, fkSesion=id_sesion)

    # ¿Qué registros ya respondieron la encuesta de la sesión? La API no
    # filtra RespuestaEncuesta por registro (solo por encuesta), así que se
    # piden las de la encuesta UNA vez y se cruzan aquí (igual que
    # '_ya_respondida' en routes/encuestas.py).
    respondidas = {}
    if sesion.get('fk_encuesta') and registros:
        for r in _lista_de('RespuestaEncuesta', limite=5000, fkEncuesta=sesion['fk_encuesta']):
            respondidas[r['fk_registro']] = r['id_respuesta']
    for r in registros:
        r['id_respuesta'] = respondidas.get(r['id_registro'])

    texto = request.args.get('q', '').strip()
    return render_template('admin/sesion_detalle.html', sesion=sesion,
                           catedra=_catedra_o_none(sesion['fk_catedra']),
                           enlaces=enlaces, registros=registros, q=texto,
                           candidatos=buscar_candidatos(texto))


@admin_bp.route('/sesiones/<int:id_sesion>/editar', methods=['GET', 'POST'])
def sesiones_editar(id_sesion):
    try:
        actual = obtener('Sesion', id_sesion)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.sesiones_listar'))
    catedra = _catedra_o_none(actual['fk_catedra'])

    if request.method == 'GET':
        return _render_sesion_form('editar', actual, catedra, [], id_sesion=id_sesion)

    datos = _leer_form_sesion()
    datos['fk_catedra'] = actual['fk_catedra']   # PUT = reemplazo completo; la cátedra no cambia aquí
    filas = _filas('reg', CAMPOS_REGISTRO_FILA, clave='fk_asistente')
    try:
        reemplazar('Sesion', id_sesion, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_sesion'] = id_sesion
        return _render_sesion_form('editar', datos, catedra, ex.errores, filas, id_sesion,
                                   emitir_marcado=request.form.get('emitir_enlace') == 'on')

    flash(f"Sesión {id_sesion} actualizada.", 'exito')
    datos['id_sesion'] = id_sesion

    # Detalles nuevos: la sesión ya existe, así que cada uno es su propia
    # petición (caso "No aplica" de la Regla 1).
    hubo_error = False
    enlace = _enlace_del_formulario()
    if enlace is not None:
        try:
            crear('EnlaceRegistro', {**enlace, 'fk_sesion': id_sesion})
            flash('Enlace de registro emitido (el anterior, si había uno vigente, quedó revocado).', 'exito')
        except ErrorApi as ex:
            _avisar(ex)
            hubo_error = True
    fallidas = _crear_detalles('RegistroAsistencia', filas,
                               lambda f, n: {'fk_sesion': id_sesion,
                                             'fk_asistente': _entero_o_none(f['fk_asistente']),
                                             'origen': f['origen'] or 'MANUAL'},
                               'Registro', 'registros')
    if fallidas or hubo_error:
        return _render_sesion_form('editar', datos, catedra, [], fallidas, id_sesion,
                                   emitir_marcado=hubo_error)
    return redirect(url_for('admin.sesiones_detalle', id_sesion=id_sesion))


@admin_bp.route('/sesiones/<int:id_sesion>/eliminar', methods=['POST'])
def sesiones_eliminar(id_sesion):
    fk_catedra = _entero_o_none(request.form.get('fk_catedra'))
    try:
        eliminar('Sesion', id_sesion)
        flash(f"Sesión {id_sesion} eliminada (y sus ponencias, por ON DELETE CASCADE).", 'exito')
    except ErrorApi as ex:
        # 500 típico: la sesión ya tiene registros de asistencia o un lote de
        # migración (RESTRICT). No se borró NADA, ni siquiera sus ponencias.
        _avisar(ex)
    return _redirigir('admin.sesiones_listar', fk_catedra=fk_catedra)


# ==========================================================================
#  v4 · PONENCIA  (recurso API: "Ponencia", llave compuesta de 3 columnas)
# --------------------------------------------------------------------------
#  ADR D19: se ASIGNA y se QUITA; no hay "editar" (las 3 columnas son la
#  llave). Para cambiar el rol de alguien: quitarlo y asignarlo de nuevo.
#  Con '?fk_sesion=' la pantalla muestra además el formulario de asignar
#  (la sesión queda fija por la URL, Regla 2).
# ==========================================================================

@admin_bp.route('/ponencias')
def ponencias_listar():
    fk_sesion = _entero_o_none(request.args.get('fk_sesion'))
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    texto = request.args.get('q', '').strip()

    sesion = None
    if fk_sesion:
        try:
            sesion = obtener('Sesion', fk_sesion)
        except ErrorApi as ex:
            _avisar(ex)

    try:
        if sesion:
            ponencias = ponentes_de(fk_sesion)          # con el nombre de cada persona
        else:
            ponencias = listar('Ponencia', limite=200, fkSesion=fk_sesion, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        ponencias = []

    return render_template('admin/ponencia_lista.html', ponencias=ponencias, sesion=sesion,
                           fk_sesion=fk_sesion, fk_asistente=fk_asistente, q=texto,
                           candidatos=buscar_candidatos(texto) if sesion else [],
                           roles=ROLES_PONENCIA)


@admin_bp.route('/ponencias/<int:fk_sesion>/asignar', methods=['POST'])
def ponencias_asignar(fk_sesion):
    asignar_ponente(fk_sesion, request.form)
    return redirect(url_for('admin.ponencias_listar', fk_sesion=fk_sesion))


@admin_bp.route('/ponencias/<int:fk_sesion>/quitar', methods=['POST'])
def ponencias_quitar(fk_sesion):
    quitar_ponente(fk_sesion, request.form)
    volver = _entero_o_none(request.form.get('volver_fk_asistente'))
    if volver:   # se quitó desde el listado filtrado por persona
        return redirect(url_for('admin.ponencias_listar', fk_asistente=volver))
    return redirect(url_for('admin.ponencias_listar', fk_sesion=fk_sesion))


# ==========================================================================
#  v5 · USUARIO  (recurso API: "Usuario", clave: id_usuario)
#  La API NUNCA devuelve la clave ni su hash (ADR D23). La clave se escribe
#  en texto plano SOLO al crear o cambiar; la API la convierte en hash.
#  Al editar, dejar la clave vacía = no cambiarla.
# ==========================================================================

@admin_bp.route('/usuarios')
def usuarios_listar():
    try:
        usuarios = listar('Usuario', limite=200)
    except ErrorApi as ex:
        _avisar(ex)
        usuarios = []
    return render_template('admin/usuario_lista.html', usuarios=usuarios)


CAMPOS_ROL_FILA = ('fk_rol', 'fecha_ini')


def _render_usuario_nuevo(usr, errores, filas_roles):
    """El formulario de usuario nuevo (maestro-detalle): persona + cuenta + roles."""
    return render_template('admin/usuario_form.html', modo='nueva', usr=usr, errores=errores,
                           roles_catalogo=_opciones('Rol'),
                           filas_roles=filas_roles if filas_roles else [{'fk_rol': ''}])
CAMPOS_ROL_FILA = ('fk_rol', 'fecha_ini')


def _render_usuario_nuevo(usr, errores, filas_roles):
    """El formulario de usuario nuevo (maestro-detalle): persona + cuenta + roles."""
    return render_template('admin/usuario_form.html', modo='nueva', usr=usr, errores=errores,
                           roles_catalogo=_opciones('Rol'),
                           filas_roles=filas_roles if filas_roles else [{'fk_rol': ''}])


@admin_bp.route('/usuarios/nuevo', methods=['GET', 'POST'])
def usuarios_nuevo():
    """Guía IA, Regla 1 (maestro-detalle): el usuario SE CREA, no se busca. Un solo POST lleva la
    persona, la cuenta y las filas de roles; la API los guarda en UNA transacción (sp_guardar_usuario):
    si algo falla (login o correo repetido, rol repetido...), no se crea nada."""
    """Guía IA, Regla 1 (maestro-detalle): el usuario SE CREA, no se busca. Un solo POST lleva la
    persona, la cuenta y las filas de roles; la API los guarda en UNA transacción (sp_guardar_usuario):
    si algo falla (login o correo repetido, rol repetido...), no se crea nada."""
    if request.method == 'GET':
        return _render_usuario_nuevo(None, [], [])

    filas_roles = _filas('rol', CAMPOS_ROL_FILA, clave='fk_rol')
        return _render_usuario_nuevo(None, [], [])

    filas_roles = _filas('rol', CAMPOS_ROL_FILA, clave='fk_rol')
    datos = {
        'nombres': request.form.get('nombres', '').strip(),
        'apellidos': request.form.get('apellidos', '').strip(),
        'correo_institucional': _texto_o_none('correo_institucional'),
        'correo_personal': _texto_o_none('correo_personal'),
        'celular': _texto_o_none('celular'),
        'nombres': request.form.get('nombres', '').strip(),
        'apellidos': request.form.get('apellidos', '').strip(),
        'correo_institucional': _texto_o_none('correo_institucional'),
        'correo_personal': _texto_o_none('correo_personal'),
        'celular': _texto_o_none('celular'),
        'usuario': request.form.get('usuario', '').strip(),
        'clave': request.form.get('clave', ''),
        'activo': request.form.get('activo') == 'on',
        'debe_cambiar_clave': request.form.get('debe_cambiar_clave') == 'on',
        'roles': [{k: v for k, v in (('fk_rol', f['fk_rol']), ('fecha_ini', f['fecha_ini'])) if v}
                  for f in filas_roles],
        'debe_cambiar_clave': request.form.get('debe_cambiar_clave') == 'on',
        'roles': [{k: v for k, v in (('fk_rol', f['fk_rol']), ('fecha_ini', f['fecha_ini'])) if v}
                  for f in filas_roles],
    }
    try:
        crear('Usuario/con-roles', datos)
        crear('Usuario/con-roles', datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos.pop('clave')                 # la clave nunca se devuelve al formulario
        return _render_usuario_nuevo(datos, ex.errores, filas_roles)
        return _render_usuario_nuevo(datos, ex.errores, filas_roles)

    flash(f"Usuario '{datos['usuario']}' creado con {len(datos['roles'])} "
          f"rol{'' if len(datos['roles']) == 1 else 'es'}.", 'exito')
    flash(f"Usuario '{datos['usuario']}' creado con {len(datos['roles'])} "
          f"rol{'' if len(datos['roles']) == 1 else 'es'}.", 'exito')
    return redirect(url_for('admin.usuarios_listar'))


@admin_bp.route('/usuarios/<int:id_usuario>/exigir-cambio-clave', methods=['POST'])
def usuarios_exigir_cambio(id_usuario):
    """v7.1 · D54 · Obliga a esa persona a cambiar su clave en el próximo ingreso."""
    try:
        crear(f'Usuario/{id_usuario}/exigir-cambio-clave', {})
        flash('Esa persona deberá cambiar su clave la próxima vez que entre.', 'exito')
    except ErrorApi as ex:
        _avisar(ex)
    return redirect(url_for('admin.usuarios_listar'))


@admin_bp.route('/usuarios/<int:id_usuario>/editar', methods=['GET', 'POST'])
def usuarios_editar(id_usuario):
    if request.method == 'GET':
        try:
            usr = obtener('Usuario', id_usuario)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.usuarios_listar'))
        return render_template('admin/usuario_form.html', modo='editar', usr=usr, errores=[])
        return render_template('admin/usuario_form.html', modo='editar', usr=usr, errores=[])

    # La persona dueña de la cuenta NO se cambia desde aquí (se muestra fija).
    datos = {
        'usuario': request.form.get('usuario', '').strip(),
        'activo': request.form.get('activo') == 'on',
    }
    if request.form.get('clave'):          # vacío = no cambiar la clave
        datos['clave'] = request.form['clave']
    try:
        actualizar_parcial('Usuario', id_usuario, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos.pop('clave', None)
        datos['id_usuario'] = id_usuario
        datos['fk_asistente'] = _entero_o_none(request.form.get('fk_asistente_actual'))
        return render_template('admin/usuario_form.html', modo='editar', usr=datos, errores=ex.errores)
        return render_template('admin/usuario_form.html', modo='editar', usr=datos, errores=ex.errores)

    flash(f"Usuario '{datos['usuario']}' actualizado.", 'exito')
    return redirect(url_for('admin.usuarios_listar'))


@admin_bp.route('/usuarios/<int:id_usuario>/eliminar', methods=['POST'])
def usuarios_eliminar(id_usuario):
    actual = usuario_actual()
    if actual and actual['id_usuario'] == id_usuario:
        flash('No puedes eliminar el usuario con el que iniciaste sesión.', 'error')
        return redirect(url_for('admin.usuarios_listar'))
    try:
        eliminar('Usuario', id_usuario)
        flash(f'Usuario {id_usuario} eliminado.', 'exito')
    except ErrorApi as ex:
        _avisar(ex)        # p. ej. 500 si tiene roles o enlaces que dependen de él (RESTRICT)
    return redirect(url_for('admin.usuarios_listar'))


# ==========================================================================
#  v5 · ROL POR USUARIO  (recurso API: "RolPorUsuario")
#  LLAVE COMPUESTA (ADR D16): usuario + rol + fecha_ini. Se ASIGNA y se
#  CIERRA; no se edita ni se elimina (queda el historial).
# ==========================================================================

@admin_bp.route('/roles-usuario')
def roles_usuario_listar():
    fk_usuario = _entero_o_none(request.args.get('fk_usuario'))
    try:
        asignaciones = listar('RolPorUsuario', limite=200, fkUsuario=fk_usuario)
    except ErrorApi as ex:
        _avisar(ex)
        asignaciones = []
    usuarios = _opciones('Usuario')
    return render_template('admin/rol_usuario_lista.html', asignaciones=asignaciones,
                           fk_usuario=fk_usuario, usuarios=usuarios,
                           nombres={u['id_usuario']: u['usuario'] for u in usuarios},
                           roles=_opciones('Rol'))


@admin_bp.route('/roles-usuario/asignar', methods=['POST'])
def roles_usuario_asignar():
    fk_usuario = _entero_o_none(request.form.get('fk_usuario'))
    datos = {'fk_usuario': fk_usuario, 'fk_rol': request.form.get('fk_rol', '')}
    if request.form.get('fecha_ini'):          # sin fecha -> la API usa hoy
        datos['fecha_ini'] = request.form['fecha_ini']
    try:
        crear('RolPorUsuario', datos)
        flash(f"Rol '{datos['fk_rol']}' asignado al usuario {fk_usuario}.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)        # 422 sin usuario/rol, 500 si el usuario o el rol no existen o ya lo tiene
    return redirect(url_for('admin.roles_usuario_listar', fk_usuario=fk_usuario))


@admin_bp.route('/roles-usuario/cerrar', methods=['POST'])
def roles_usuario_cerrar():
    fk_usuario = _entero_o_none(request.form.get('fk_usuario'))
    llave = {
        'fk_usuario': fk_usuario,
        'fk_rol': request.form.get('fk_rol', ''),
        'fecha_ini': request.form.get('fecha_ini', ''),
    }
    if request.form.get('fecha_fin'):          # sin fecha -> la API usa hoy
        llave['fecha_fin'] = request.form['fecha_fin']
    try:
        actualizar_parcial('RolPorUsuario', 'cerrar', llave)
        flash(f"Rol '{llave['fk_rol']}' cerrado.", 'exito')
    except ErrorApi as ex:
        _avisar(ex)        # 400 si ya estaba cerrado o fecha_fin < fecha_ini
    return redirect(url_for('admin.roles_usuario_listar', fk_usuario=request.form.get('volver_fk_usuario') or None))


# ==========================================================================
#  v5 · ENLACE DE REGISTRO  (recurso API: "EnlaceRegistro")
#  No es un CRUD: se EMITE (POST) y se REVOCA (PATCH). Emitir un enlace
#  nuevo para una sesión revoca el anterior de esa sesión (RN-14).
# ==========================================================================

def _sesion_o_none(fk_sesion):
    """La sesión de una pantalla v5 (viene de la URL: ?fk_sesion=). None si no hay o no existe."""
    if not fk_sesion:
        return None
    try:
        return obtener('Sesion', fk_sesion)
    except ErrorApi as ex:
        _avisar(ex)
        return None


def _estado_enlace(enlace):
    """'Revocado' | 'Programado' | 'Vencido' | 'Vigente', según revocado_en y la ventana."""
    if enlace.get('revocado_en'):
        return 'Revocado'
    ahora = datetime.now(timezone.utc)
    desde, hasta = a_local(enlace.get('ventana_desde')), a_local(enlace.get('ventana_hasta'))
    if desde and ahora < desde:
        return 'Programado'
    if hasta and ahora > hasta:
        return 'Vencido'
    return 'Vigente'


@admin_bp.route('/enlaces')
def enlaces_listar():
    fk_sesion = _entero_o_none(request.args.get('fk_sesion'))
    sesion = _sesion_o_none(fk_sesion)       # Regla 2: la sesión se fija por URL, no se digita
    try:
        enlaces = listar('EnlaceRegistro', limite=200, fkSesion=fk_sesion)
    except ErrorApi as ex:
        _avisar(ex)
        enlaces = []
    return render_template('admin/enlace_lista.html', enlaces=_preparar_enlaces(enlaces),
                           fk_sesion=fk_sesion, sesion=sesion)


def _preparar_enlaces(enlaces):
    """Agrega a cada enlace su estado y el enlace público para compartir
    (lo usan la lista de enlaces y la pantalla de la sesión)."""
    for e in enlaces:
        e['estado'] = _estado_enlace(e)
        # El enlace que se COMPARTE con el público es el de ESTE front
        # (url_publica de la BD es solo el dato guardado).
        e['enlace_publico'] = url_for('registro.solicitar', token=e['token'], _external=True)
    return enlaces


@admin_bp.route('/enlaces/emitir', methods=['POST'])
def enlaces_emitir():
    fk_sesion = _entero_o_none(request.form.get('fk_sesion'))
    datos = {'fk_sesion': fk_sesion}
    for campo in ('minutos_antes', 'minutos_despues'):      # vacío -> la API usa su valor por defecto
        valor = _entero_o_none(request.form.get(campo))
        if valor is not None:
            datos[campo] = valor
    actual = usuario_actual()
    if actual:
        datos['fk_usuario'] = actual['id_usuario']          # queda quién lo emitió
    try:
        respuesta = crear('EnlaceRegistro', datos)
        flash(f"Enlace {respuesta['id_enlace']} emitido para la sesión {fk_sesion}. "
              "Si la sesión tenía otro enlace vigente, quedó revocado (RN-14).", 'exito')
    except ErrorApi as ex:
        _avisar(ex)        # 422 sin sesión, 500 si la sesión no existe
    return _redirigir('admin.enlaces_listar', fk_sesion=fk_sesion)


@admin_bp.route('/enlaces/<int:id_enlace>/revocar', methods=['POST'])
def enlaces_revocar(id_enlace):
    try:
        actualizar_parcial('EnlaceRegistro', f'{id_enlace}/revocar', {})
        flash(f'Enlace {id_enlace} revocado.', 'exito')
    except ErrorApi as ex:
        _avisar(ex)        # 400 si ya estaba revocado, 404 si no existe
    return _redirigir('admin.enlaces_listar', fk_sesion=_entero_o_none(request.form.get('volver_fk_sesion')))


# ==========================================================================
#  v5 · CLAVE DE ACCESO  (recurso API: "ClaveAcceso")  ·  SOLO LECTURA
#  Las claves las genera la API cuando alguien las solicita en /registro;
#  aquí no se crean ni se editan. Nunca se ve la clave (solo su hash en BD).
# ==========================================================================

@admin_bp.route('/claves')
def claves_listar():
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    try:
        claves = listar('ClaveAcceso', limite=200, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        claves = []
    return render_template('admin/clave_lista.html', claves=claves, fk_asistente=fk_asistente)


# ==========================================================================
#  v5 · REGISTRO DE ASISTENCIA  (recurso API: "RegistroAsistencia")
#  Se lee, y se puede registrar a mano (origen MANUAL o IMPORTADO, D26).
#  Los registros por QR los crea la API al validar una clave. Sin editar
#  ni eliminar: es un registro de auditoría.
# ==========================================================================

@admin_bp.route('/registros')
def registros_listar():
    fk_sesion = _entero_o_none(request.args.get('fk_sesion'))
    fk_asistente = _entero_o_none(request.args.get('fk_asistente'))
    texto = request.args.get('q', '').strip()
    sesion = _sesion_o_none(fk_sesion)
    try:
        registros = listar('RegistroAsistencia', limite=200, fkSesion=fk_sesion, fkAsistente=fk_asistente)
    except ErrorApi as ex:
        _avisar(ex)
        registros = []
    # El alta manual solo se ofrece con una sesión fijada; la persona se busca y se elige.
    return render_template('admin/registro_asistencia_lista.html', registros=registros,
                           fk_sesion=fk_sesion, fk_asistente=fk_asistente, sesion=sesion,
                           q=texto, candidatos=buscar_candidatos(texto) if sesion else [])


@admin_bp.route('/registros/nuevo', methods=['POST'])
def registros_nuevo():
    fk_sesion = _entero_o_none(request.form.get('fk_sesion'))
    datos = {
        'fk_sesion': fk_sesion,
        'fk_asistente': _entero_o_none(request.form.get('fk_asistente')),
        'origen': request.form.get('origen', ''),
    }
    if datos['fk_asistente'] is None:
        flash('Selecciona una persona de la lista (primero búscala).', 'error')
        return _redirigir('admin.registros_listar', fk_sesion=fk_sesion)
    try:
        respuesta = crear('RegistroAsistencia', datos)
        flash(f"Asistencia registrada (registro {respuesta['id_registro']}).", 'exito')
    except ErrorApi as ex:
        _avisar(ex)        # 400 origen inválido, 500 si el asistente no tiene vinculación vigente (RN-07)
    return _redirigir('admin.registros_listar', fk_sesion=fk_sesion)


def _respuesta_de_registro(registro, sesion):
    """La RespuestaEncuesta de un registro (a lo sumo una:
    uq_respuesta_registro), o None. La API solo filtra RespuestaEncuesta
    por encuesta, así que se piden las de la encuesta de la sesión (o todas,
    si la sesión no tiene encuesta) y se busca el registro aquí. Son pocas
    (62 en los datos de ejemplo): mismo criterio que '_ya_respondida'."""
    filtros = {'fkEncuesta': sesion['fk_encuesta']} if sesion and sesion.get('fk_encuesta') else {}
    for r in _lista_de('RespuestaEncuesta', limite=20000, **filtros):
        if r['fk_registro'] == registro['id_registro']:
            return r
    return None


@admin_bp.route('/registros/<int:id_registro>')
def registros_detalle(id_registro):
    """Maestro-detalle #8: el registro de asistencia con su respuesta de
    encuesta (la respuesta cuelga del REGISTRO, no de la encuesta) y, de
    una vez, los ítems de esa respuesta (#9)."""
    try:
        registro = obtener('RegistroAsistencia', id_registro)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.registros_listar'))

    sesion = _sesion_o_none(registro['fk_sesion'])
    asistente = None
    try:
        asistente = obtener('Asistente', registro['fk_asistente'])
    except ErrorApi:
        pass   # se muestra solo el id

    respuesta = _respuesta_de_registro(registro, sesion)
    items = _items_de_respuesta(respuesta['id_respuesta']) if respuesta else []
    return render_template('admin/registro_asistencia_detalle.html', registro=registro, sesion=sesion,
                           asistente=asistente, respuesta=respuesta, items=items)


# ==========================================================================
#  v6 · PREGUNTA  (recurso API: "Pregunta", clave: id_pregunta)
#  CRUD con 'opciones' iniciales OPCIONALES al crear (Guía IA, Regla 1):
#  una pregunta de tipo OPCION_UNICA se crea junto con sus opciones, en
#  una sola transacción (sp_guardar_pregunta) — igual mecánica que el
#  alias de ProgramaAcademico (v2), pero de nacimiento, no retrofit.
#  El PUT de editar NO toca 'fk_encuesta' ni las opciones (D33): esas se
#  administran aparte, en OpcionPregunta.
# ==========================================================================

def _opciones_de_texto(texto):
    """Una opción por línea, formato libre 'etiqueta' o 'etiqueta;valor'.
    El 'orden' lo pone la posición de la línea (Regla 2: nada que la
    persona tenga que numerar a mano). Lo usan las filas de pregunta del
    formulario de la ENCUESTA (dos niveles de detalle en un solo campo)."""
    opciones = []
    for i, linea in enumerate((texto or '').splitlines(), start=1):
        linea = linea.strip()
        if not linea:
            continue
        etiqueta, _, valor = linea.partition(';')
        opcion = {'etiqueta': etiqueta.strip(), 'orden': i}
        valor = valor.strip()
        if valor:
            try:
                opcion['valor'] = int(valor)
            except ValueError:
                pass   # un valor no numérico se ignora; la etiqueta y el orden sí quedan
        opciones.append(opcion)
    return opciones


def _leer_form_pregunta():
    datos = {
        'enunciado': request.form.get('enunciado', '').strip(),
        'orden': _entero_o_none(request.form.get('orden')),
        'fk_tipo_pregunta': request.form.get('fk_tipo_pregunta', ''),
        'obligatoria': request.form.get('obligatoria') == 'on',
    }
    valor_min = _entero_o_none(request.form.get('valor_min'))
    valor_max = _entero_o_none(request.form.get('valor_max'))
    if valor_min is not None:
        datos['valor_min'] = valor_min
    if valor_max is not None:
        datos['valor_max'] = valor_max
    return datos


# Maestro-detalle #7 en el formulario de la pregunta: las opciones en filas.
CAMPOS_OPCION_FILA = ('etiqueta', 'valor')


def _opcion_de_fila(fila, orden):
    opcion = {'etiqueta': fila['etiqueta'], 'orden': orden}
    valor = _entero_o_none(fila['valor'])
    if valor is not None:
        opcion['valor'] = valor
    return opcion


def _render_pregunta_form(modo, pregunta, errores, filas_opciones=(), id_pregunta=None):
    existentes = []
    if modo == 'editar' and id_pregunta:
        existentes = sorted(_lista_de('OpcionPregunta', limite=500, fkPregunta=id_pregunta), key=lambda o: o['orden'])
    return render_template('admin/pregunta_form.html', modo=modo, pregunta=pregunta, errores=errores,
                           encuestas=_opciones('Encuesta'), tipos=_opciones('TipoPregunta'),
                           filas_opciones=list(filas_opciones), opciones_existentes=existentes)


def _encuesta_o_none(id_encuesta):
    """La encuesta (para el título y las migas de pan), o None."""
    if not id_encuesta:
        return None
    try:
        return obtener('Encuesta', id_encuesta)
    except ErrorApi:
        return None


@admin_bp.route('/preguntas')
def preguntas_listar():
    fk_encuesta = _entero_o_none(request.args.get('fk_encuesta'))
    try:
        preguntas = listar('Pregunta', limite=200, fkEncuesta=fk_encuesta)
    except ErrorApi as ex:
        _avisar(ex)
        preguntas = []
    return render_template('admin/pregunta_lista.html', preguntas=preguntas, fk_encuesta=fk_encuesta,
                           encuestas=_opciones('Encuesta'))


@admin_bp.route('/preguntas/nueva', methods=['GET', 'POST'])
def preguntas_nueva():
    if request.method == 'GET':
        # Desde la pantalla de la encuesta llega '?fk_encuesta=': queda preseleccionada.
        fk_encuesta = _entero_o_none(request.args.get('fk_encuesta'))
        return _render_pregunta_form('nueva', {'fk_encuesta': fk_encuesta} if fk_encuesta else None, [])

    datos = _leer_form_pregunta()
    datos['fk_encuesta'] = _entero_o_none(request.form.get('fk_encuesta'))
    # Retrofit v6 (Guía IA, Regla 1): las opciones de las FILAS del
    # formulario viajan en el MISMO POST que la pregunta -> sp_guardar_pregunta
    # las guarda juntas en una sola transacción. Si alguna falla, NO se crea
    # ni la pregunta (todo o nada). El orden lo pone la posición de la fila.
    filas = _filas('opc', CAMPOS_OPCION_FILA, clave='etiqueta')
    opciones = [_opcion_de_fila(f, n) for n, f in enumerate(filas, start=1)]
    if opciones:
        datos['opciones'] = opciones
    try:
        respuesta = crear('Pregunta', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return _render_pregunta_form('nueva', datos, ex.errores, filas)

    cantidad = len(opciones)
    sufijo = f" con {cantidad} opciones" if cantidad else ''
    flash(f"Pregunta creada{sufijo}.", 'exito')
    return redirect(url_for('admin.preguntas_detalle', id_pregunta=respuesta['id_pregunta']))


@admin_bp.route('/preguntas/<int:id_pregunta>')
def preguntas_detalle(id_pregunta):
    """Maestro-detalle #7: la pregunta con sus opciones (en su orden,
    uq_opcion_orden). Desde aquí se agregan, editan y borran."""
    try:
        pregunta = obtener('Pregunta', id_pregunta)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.preguntas_listar'))
    opciones = sorted(_lista_de('OpcionPregunta', limite=500, fkPregunta=id_pregunta),
                      key=lambda o: o['orden'])
    return render_template('admin/pregunta_detalle.html', pregunta=pregunta, opciones=opciones,
                           encuesta=_encuesta_o_none(pregunta['fk_encuesta']))


@admin_bp.route('/preguntas/<int:id_pregunta>/editar', methods=['GET', 'POST'])
def preguntas_editar(id_pregunta):
    if request.method == 'GET':
        try:
            pregunta = obtener('Pregunta', id_pregunta)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.preguntas_listar'))
        return _render_pregunta_form('editar', pregunta, [], id_pregunta=id_pregunta)

    datos = _leer_form_pregunta()
    filas = _filas('opc', CAMPOS_OPCION_FILA, clave='etiqueta')
    try:
        reemplazar('Pregunta', id_pregunta, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_pregunta'] = id_pregunta
        return _render_pregunta_form('editar', datos, ex.errores, filas, id_pregunta)

    flash(f"Pregunta {id_pregunta} actualizada.", 'exito')
    datos['id_pregunta'] = id_pregunta
    # Opciones nuevas: cada una con su POST /api/OpcionPregunta; el orden
    # sigue después de la última que ya tiene la pregunta (uq_opcion_orden).
    ultimo = max((o['orden'] for o in _lista_de('OpcionPregunta', limite=500, fkPregunta=id_pregunta)), default=0)
    fallidas = _crear_detalles('OpcionPregunta', filas,
                               lambda f, n: {**_opcion_de_fila(f, ultimo + n), 'fk_pregunta': id_pregunta},
                               'Opción', 'opciones')
    if fallidas:
        return _render_pregunta_form('editar', datos, [], fallidas, id_pregunta)
    return redirect(url_for('admin.preguntas_detalle', id_pregunta=id_pregunta))


@admin_bp.route('/preguntas/<int:id_pregunta>/eliminar', methods=['POST'])
def preguntas_eliminar(id_pregunta):
    try:
        eliminar('Pregunta', id_pregunta)
        flash(f"Pregunta {id_pregunta} eliminada.", 'exito')
    except ErrorApi as ex:
        # 500 típico: ya tiene respuestas de ejemplo (respuesta_item,
        # RESTRICT) — la cascada hacia sus opciones no llega a ejecutarse.
        _avisar(ex)
    return _redirigir('admin.preguntas_listar')


# ==========================================================================
#  v6 · OPCIÓN DE PREGUNTA  (recurso API: "OpcionPregunta", clave: id_opcion)
#  CRUD completo (D33): a diferencia de AliasPrograma/ConsentimientoDatos,
#  aquí SÍ hace falta editar — corregir la etiqueta o reordenar una
#  opción es una edición legítima, no "una nueva versión del hecho".
# ==========================================================================

def _leer_form_opcion():
    datos = {
        'etiqueta': request.form.get('etiqueta', '').strip(),
        'orden': _entero_o_none(request.form.get('orden')),
    }
    valor = _entero_o_none(request.form.get('valor'))
    if valor is not None:
        datos['valor'] = valor
    return datos


@admin_bp.route('/opciones-pregunta')
def opciones_listar():
    fk_pregunta = _entero_o_none(request.args.get('fk_pregunta'))
    try:
        opciones = listar('OpcionPregunta', limite=200, fkPregunta=fk_pregunta)
    except ErrorApi as ex:
        _avisar(ex)
        opciones = []
    return render_template('admin/opcion_pregunta_lista.html', opciones=opciones, fk_pregunta=fk_pregunta)


@admin_bp.route('/opciones-pregunta/nueva', methods=['GET', 'POST'])
def opciones_nueva():
    if request.method == 'GET':
        fk_pregunta = _entero_o_none(request.args.get('fk_pregunta'))
        return render_template('admin/opcion_pregunta_form.html', modo='nueva', opcion=None, errores=[],
                               fk_pregunta=fk_pregunta, preguntas=_opciones('Pregunta'))

    datos = _leer_form_opcion()
    datos['fk_pregunta'] = _entero_o_none(request.form.get('fk_pregunta'))
    try:
        respuesta = crear('OpcionPregunta', datos)
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('admin/opcion_pregunta_form.html', modo='nueva', opcion=datos, errores=ex.errores,
                               fk_pregunta=datos['fk_pregunta'], preguntas=_opciones('Pregunta'))

    flash(f"Opción creada con id {respuesta['id_opcion']}.", 'exito')
    return _redirigir('admin.opciones_listar', fk_pregunta=datos['fk_pregunta'])


@admin_bp.route('/opciones-pregunta/<int:id_opcion>/editar', methods=['GET', 'POST'])
def opciones_editar(id_opcion):
    if request.method == 'GET':
        try:
            opcion = obtener('OpcionPregunta', id_opcion)
        except ErrorApi as ex:
            _avisar(ex)
            return redirect(url_for('admin.opciones_listar'))
        return render_template('admin/opcion_pregunta_form.html', modo='editar', opcion=opcion, errores=[],
                               fk_pregunta=None, preguntas=[])

    datos = _leer_form_opcion()
    try:
        reemplazar('OpcionPregunta', id_opcion, datos)
    except ErrorApi as ex:
        _avisar(ex)
        datos['id_opcion'] = id_opcion
        return render_template('admin/opcion_pregunta_form.html', modo='editar', opcion=datos, errores=ex.errores,
                               fk_pregunta=None, preguntas=[])

    flash(f"Opción {id_opcion} actualizada.", 'exito')
    return _redirigir('admin.opciones_listar')


@admin_bp.route('/opciones-pregunta/<int:id_opcion>/eliminar', methods=['POST'])
def opciones_eliminar(id_opcion):
    try:
        eliminar('OpcionPregunta', id_opcion)
        flash(f"Opción {id_opcion} eliminada.", 'exito')
    except ErrorApi as ex:
        # 500 típico: ya fue elegida en alguna respuesta de ejemplo
        # (respuesta_item.fk_opcion, RESTRICT).
        _avisar(ex)
    return _redirigir('admin.opciones_listar')


# ==========================================================================
#  v6 · RESPUESTA DE ENCUESTA  (recurso API: "RespuestaEncuesta")  ·  SOLO LECTURA
#  Nace cuando un asistente responde su encuesta (front público, fuera de
#  /admin — ver routes/encuestas.py). Es un hecho auditable (D32, igual
#  criterio que ConsentimientoDatos en v3 y RegistroAsistencia en v5):
#  sin editar ni eliminar, ni aquí ni en la API.
# ==========================================================================

@admin_bp.route('/respuestas-encuesta')
def respuestas_listar():
    fk_encuesta = _entero_o_none(request.args.get('fk_encuesta'))
    try:
        respuestas = listar('RespuestaEncuesta', limite=200, fkEncuesta=fk_encuesta)
    except ErrorApi as ex:
        _avisar(ex)
        respuestas = []
    return render_template('admin/respuesta_encuesta_lista.html', respuestas=respuestas,
                           fk_encuesta=fk_encuesta, encuestas=_opciones('Encuesta'))


@admin_bp.route('/respuestas-encuesta/<int:id_respuesta>')
def respuestas_detalle(id_respuesta):
    """Maestro-detalle #9: la respuesta con sus ítems (uno por pregunta)."""
    try:
        respuesta = obtener('RespuestaEncuesta', id_respuesta)
    except ErrorApi as ex:
        _avisar(ex)
        return redirect(url_for('admin.respuestas_listar'))
    return render_template('admin/respuesta_encuesta_detalle.html', respuesta=respuesta,
                           items=_items_de_respuesta(id_respuesta))


def _items_de_respuesta(id_respuesta):
    """Los RespuestaItem de una respuesta, cada uno con su pregunta y su
    opción elegida, en el orden de las preguntas. Lo usan el detalle de la
    respuesta (#9) y el del registro de asistencia (#8)."""
    items = []
    try:
        items = listar('RespuestaItem', fkRespuesta=id_respuesta)
        # Cada ítem solo trae 'fk_pregunta'/'fk_opcion'; se pide cada una
        # para mostrar el enunciado y la etiqueta (pocos ítems por
        # respuesta: una llamada por pregunta es aceptable, igual
        # criterio que 'ponentes_de' en routes/sesiones.py).
        cache_preguntas = {}
        cache_opciones = {}
        for item in items:
            fk_pregunta = item['fk_pregunta']
            if fk_pregunta not in cache_preguntas:
                try:
                    cache_preguntas[fk_pregunta] = obtener('Pregunta', fk_pregunta)
                except ErrorApi:
                    cache_preguntas[fk_pregunta] = None
            item['pregunta'] = cache_preguntas[fk_pregunta]

            fk_opcion = item.get('fk_opcion')
            if fk_opcion:
                if fk_opcion not in cache_opciones:
                    try:
                        cache_opciones[fk_opcion] = obtener('OpcionPregunta', fk_opcion)
                    except ErrorApi:
                        cache_opciones[fk_opcion] = None
                item['opcion'] = cache_opciones[fk_opcion]
    except ErrorApi as ex:
        _avisar(ex)

    items.sort(key=lambda i: i['pregunta']['orden'] if i.get('pregunta') else 0)
    return items


# ==========================================================================
#  v6 · ÍTEM DE RESPUESTA  (recurso API: "RespuestaItem")  ·  SOLO LECTURA
#  Nace solo dentro de "responder" (RF178); normalmente se consulta desde
#  el detalle de su RespuestaEncuesta (arriba). Esta lista es el filtro
#  libre, igual criterio que ClaveAcceso en v5.
# ==========================================================================

@admin_bp.route('/items-respuesta')
def items_listar():
    fk_respuesta = _entero_o_none(request.args.get('fk_respuesta'))
    try:
        items = listar('RespuestaItem', limite=200, fkRespuesta=fk_respuesta)
    except ErrorApi as ex:
        _avisar(ex)
        items = []
    return render_template('admin/respuesta_item_lista.html', items=items, fk_respuesta=fk_respuesta)


# ==========================================================================
#  VERSIÓN 7 · MIGRACIÓN A ASIS, BITÁCORA Y PARÁMETROS
#  (criterio 8 de version7/2_spec.md; decisión D37-E: todo vive en ESTE
#  blueprint para heredar la guardia de rol ADMIN del before_request de
#  arriba — un blueprint nuevo no la tendría).
#
#  Cuatro bloques:
#    · PARÁMETROS        -> CRUD completo, llave de texto ('clave').
#    · LOTES DE MIGRACIÓN -> lista, detalle con sus filas, PATCH de estado
#                           y de resultado por fila, y "generar migración"
#                           de una sesión (POST bajo /admin).
#    · DETALLE DE MIGRACIÓN -> lista filtrable de las filas de los lotes.
#    · BITÁCORA          -> SOLO LECTURA (D36): la escriben los triggers.
#
#  Criterio 11 de VERSION_2.md: en estas pantallas no aparecen nombres de
#  verbos HTTP, códigos de estado ni nombres de restricciones de la BD.
#  Los errores conocidos de la API se traducen a castellano en
#  '_avisar_v7' (abajo); el resto se muestra con el mensaje de la API.
# ==========================================================================

TIPOS_DATO = ['TEXTO', 'ENTERO', 'BOOLEANO', 'FECHA', 'DECIMAL']   # refleja chk_parametro_tipo
RESULTADOS_MIGRACION = ['PENDIENTE', 'ACEPTADO', 'RECHAZADO']       # refleja chk_detalle_resultado
TABLAS_BITACORA = ['registro_asistencia', 'asistente']              # las dos con trigger de bitácora
LIMITES_BITACORA = [20, 50, 100, 500]

# Errores de la BD que ya sabemos explicar: (texto que trae la API, mensaje para la persona).
_TRADUCCIONES_V7 = [
    ('ux_detmig_aceptado', 'Ese registro de asistencia ya quedó ACEPTADO en otro lote de migración: '
                           'ASIS no puede aceptar el mismo registro dos veces.'),
    ('No hay registros migrables', 'La sesión no tiene registros que se puedan migrar a ASIS. Solo se '
                                   'migran asistentes con ID de ASIS y programa, que no sean externos.'),
    ('fk_lotemig_estado', 'Ese estado no existe en el catálogo de estados de proceso.'),
    ('pk_parametro', 'Ya existe un parámetro con esa clave.'),
    ('fk_parametro_usuario', 'El usuario que modifica no existe.'),
    ('fk_lotemig_usuario', 'El usuario que genera el lote no existe.'),
]


def _avisar_v7(ex):
    """Como '_avisar', pero en castellano y sin jerga técnica (criterio 11):
    errores conocidos -> frase clara; 'sin cambios' -> aviso; red -> la API
    no responde; cualquier otro -> el mensaje (ya en castellano) de la API."""
    texto = f"{ex.mensaje} {ex.detalle or ''}"
    for clave, frase in _TRADUCCIONES_V7:
        if clave in texto:
            flash(frase, 'error')
            return
    if ex.codigo is None:
        flash('No se pudo contactar la API de Cátedras. ¿Está levantada?', 'error')
    elif ex.codigo == 400 and 'ningún campo' in (ex.detalle or ''):
        flash('No cambiaste ningún dato.', 'error')
    elif ex.codigo == 422:
        flash('Revisa los datos del formulario.', 'error')
    else:
        flash(ex.mensaje, 'error')


def _id_admin_actual():
    """id_usuario del administrador que tiene la sesión abierta (o None).
    Se manda como 'fk_usuario' / 'fk_usuario_modifica': es una FK que no se
    digita ni se elige, la fija la sesión (Regla 2)."""
    actual = usuario_actual()
    return actual.get('id_usuario') if actual else None


# --------------------------------------------------------------------------
#  v7 · PARÁMETROS  (recurso API: "Parametro", llave: clave)
# --------------------------------------------------------------------------

def _leer_form_parametro():
    """Columnas editables (las mismas en crear y editar; 'clave' va aparte)."""
    return {
        'valor': request.form.get('valor', ''),            # puede ser '' a propósito
        'tipo_dato': request.form.get('tipo_dato', 'TEXTO'),
        'descripcion': request.form.get('descripcion', '').strip(),
        'fk_usuario_modifica': _id_admin_actual(),
    }


@admin_bp.route('/parametros')
def parametros_listar():
    try:
        parametros = listar('Parametro')
    except ErrorApi as ex:
        _avisar_v7(ex)
        parametros = []
    return render_template('admin/parametro_lista.html', parametros=parametros)


@admin_bp.route('/parametros/nuevo', methods=['GET', 'POST'])
def parametros_nuevo():
    if request.method == 'GET':
        return render_template('admin/parametro_form.html', modo='nuevo', parametro=None,
                               errores=[], tipos=TIPOS_DATO)

    datos = _leer_form_parametro()
    datos['clave'] = request.form.get('clave', '').strip().upper()   # la API exige MAYÚSCULAS
    try:
        crear('Parametro', datos)
    except ErrorApi as ex:
        _avisar_v7(ex)
        return render_template('admin/parametro_form.html', modo='nuevo', parametro=datos,
                               errores=ex.errores, tipos=TIPOS_DATO)

    flash(f"Parámetro '{datos['clave']}' creado.", 'exito')
    return redirect(url_for('admin.parametros_listar'))


@admin_bp.route('/parametros/<clave>/editar', methods=['GET', 'POST'])
def parametros_editar(clave):
    if request.method == 'GET':
        try:
            parametro = obtener('Parametro', clave)
        except ErrorApi as ex:
            _avisar_v7(ex)
            return redirect(url_for('admin.parametros_listar'))
        return render_template('admin/parametro_form.html', modo='editar', parametro=parametro,
                               errores=[], tipos=TIPOS_DATO)

    # Reemplazo completo: el formulario siempre trae los 3 campos. La clave
    # NO viaja en el cuerpo: es la llave de la ruta (no se edita).
    datos = _leer_form_parametro()
    try:
        reemplazar('Parametro', clave, datos)
    except ErrorApi as ex:
        _avisar_v7(ex)
        datos['clave'] = clave
        return render_template('admin/parametro_form.html', modo='editar', parametro=datos,
                               errores=ex.errores, tipos=TIPOS_DATO)

    flash(f"Parámetro '{clave}' actualizado.", 'exito')
    return redirect(url_for('admin.parametros_listar'))


@admin_bp.route('/parametros/<clave>/eliminar', methods=['POST'])
def parametros_eliminar(clave):
    try:
        eliminar('Parametro', clave)
        flash(f"Parámetro '{clave}' eliminado. Las funciones que lo leían usan su valor por defecto.", 'exito')
    except ErrorApi as ex:
        _avisar_v7(ex)
    return redirect(url_for('admin.parametros_listar'))


# --------------------------------------------------------------------------
#  v7 · LOTES DE MIGRACIÓN  (recursos API: "LoteMigracion", "DetalleMigracion")
#  Generar un lote es UNA llamada (POST /api/LoteMigracion): la BD crea el
#  lote, sus filas y deja la sesión en MIGRADA (RF184). El front NO toca
#  la sesión por su cuenta.
# --------------------------------------------------------------------------

def _estados_proceso():
    """Catálogo de estados para el <select> (Regla 2), con su bandera de final."""
    return _opciones('EstadoProceso')


@admin_bp.route('/lotes-migracion')
def lotes_migracion_listar():
    fk_sesion = _entero_o_none(request.args.get('fk_sesion'))
    try:
        lotes = listar('LoteMigracion', limite=200, fkSesion=fk_sesion)
        # Para el <select> del filtro: solo las sesiones que tienen lotes
        # (no las miles de sesiones del catálogo).
        sesiones = sorted({l['fk_sesion'] for l in listar('LoteMigracion', limite=1000)})
    except ErrorApi as ex:
        _avisar_v7(ex)
        lotes, sesiones = [], []
    return render_template('admin/lote_migracion_lista.html', lotes=lotes, fk_sesion=fk_sesion,
                           sesiones=sesiones)


@admin_bp.route('/lotes-migracion/<int:id_lote>')
def lotes_migracion_detalle(id_lote):
    try:
        lote = obtener('LoteMigracion', id_lote)
    except ErrorApi as ex:
        _avisar_v7(ex)
        return redirect(url_for('admin.lotes_migracion_listar'))

    filtro = request.args.get('resultado') or None
    if filtro not in RESULTADOS_MIGRACION:
        filtro = None
    detalles = []
    conteo = {r: 0 for r in RESULTADOS_MIGRACION}
    try:
        detalles = listar('DetalleMigracion', limite=10000, fkLoteMigracion=id_lote)
        for d in detalles:
            conteo[d['resultado']] = conteo.get(d['resultado'], 0) + 1
    except ErrorApi as ex:
        _avisar_v7(ex)
    visibles = [d for d in detalles if filtro is None or d['resultado'] == filtro]
    return render_template('admin/lote_migracion_detalle.html', lote=lote, detalles=visibles,
                           total_detalles=len(detalles), conteo=conteo, filtro=filtro,
                           estados=_estados_proceso(), resultados=RESULTADOS_MIGRACION)


@admin_bp.route('/lotes-migracion/<int:id_lote>/estado', methods=['POST'])
def lotes_migracion_estado(id_lote):
    # Actualización PARCIAL: solo lo que la persona puede cambiar (D35).
    datos = {'fk_estado_proceso': request.form.get('fk_estado_proceso', '')}
    observaciones = request.form.get('observaciones', '').strip()
    if observaciones:
        datos['observaciones'] = observaciones
    try:
        actualizar_parcial('LoteMigracion', id_lote, datos)
        flash(f"Lote {id_lote}: estado {datos['fk_estado_proceso']} guardado.", 'exito')
    except ErrorApi as ex:
        _avisar_v7(ex)
    return redirect(url_for('admin.lotes_migracion_detalle', id_lote=id_lote))


@admin_bp.route('/lotes-migracion/<int:id_lote>/detalle/<int:id_detalle>', methods=['POST'])
def lotes_migracion_resultado(id_lote, id_detalle):
    # Simula la respuesta de ASIS para UNA fila (RF188).
    datos = {'resultado': request.form.get('resultado', '')}
    mensaje = request.form.get('mensaje', '').strip()
    if mensaje:
        datos['mensaje'] = mensaje
    try:
        actualizar_parcial('DetalleMigracion', id_detalle, datos)
        flash(f"Fila {id_detalle}: {datos['resultado']}.", 'exito')
    except ErrorApi as ex:
        _avisar_v7(ex)    # p. ej. ese registro ya fue ACEPTADO en otro lote
    filtro = request.form.get('filtro') or None
    return redirect(url_for('admin.lotes_migracion_detalle', id_lote=id_lote, resultado=filtro) + f'#fila-{id_detalle}')


@admin_bp.route('/sesiones/<int:id_sesion>/migrar', methods=['POST'])
def sesiones_migrar(id_sesion):
    """'Generar migración' (botón de detalle_sesion.html). Bajo /admin para
    heredar la guardia de rol ADMIN (D37-E)."""
    try:
        respuesta = crear('LoteMigracion', {'fk_sesion': id_sesion, 'fk_usuario': _id_admin_actual()})
    except ErrorApi as ex:
        _avisar_v7(ex)    # p. ej. la sesión no tiene registros migrables
        return redirect(url_for('sesiones.detalle', id_sesion=id_sesion))

    lote = respuesta['lote']
    flash(f"Lote {lote['id_lote_migracion']} generado: archivo {lote['nombre_archivo']} con "
          f"{lote['total_filas']} filas. La sesión quedó marcada como migrada.", 'exito')
    return redirect(url_for('admin.lotes_migracion_detalle', id_lote=lote['id_lote_migracion']))


# --------------------------------------------------------------------------
#  v7 · DETALLE DE MIGRACIÓN  (recurso API: "DetalleMigracion")
#  Lista filtrable de las filas de todos los lotes. Se editan desde el
#  detalle de su lote (arriba), donde se ve el contexto.
# --------------------------------------------------------------------------

@admin_bp.route('/detalles-migracion')
def detalles_migracion_listar():
    fk_lote = _entero_o_none(request.args.get('fk_lote_migracion'))
    filtro = request.args.get('resultado') or None
    if filtro not in RESULTADOS_MIGRACION:
        filtro = None
    try:
        detalles = listar('DetalleMigracion', limite=500, fkLoteMigracion=fk_lote)
        lotes = listar('LoteMigracion', limite=200)
    except ErrorApi as ex:
        _avisar_v7(ex)
        detalles, lotes = [], []
    detalles = [d for d in detalles if filtro is None or d['resultado'] == filtro]
    return render_template('admin/detalle_migracion_lista.html', detalles=detalles, lotes=lotes,
                           fk_lote=fk_lote, filtro=filtro, resultados=RESULTADOS_MIGRACION)


# --------------------------------------------------------------------------
#  v7 · BITÁCORA  (recurso API: "Bitacora")  ·  SOLO LECTURA (ADR D36)
#  La llenan los triggers de la BD; ni la API ni el front escriben en ella.
#  Sin filtro por usuario: 'fk_usuario' es nulo en todas las filas (D37-C).
# --------------------------------------------------------------------------

@admin_bp.route('/bitacora')
def bitacora_listar():
    tabla = request.args.get('tabla') or None
    if tabla not in TABLAS_BITACORA:
        tabla = None
    limite = _entero_o_none(request.args.get('limite'))
    if limite not in LIMITES_BITACORA:
        limite = LIMITES_BITACORA[1]
    try:
        entradas = listar('Bitacora', limite=limite, nombreTabla=tabla)
    except ErrorApi as ex:
        _avisar_v7(ex)
        entradas = []
    return render_template('admin/bitacora_lista.html', entradas=entradas, tabla=tabla, limite=limite,
                           tablas=TABLAS_BITACORA, limites=LIMITES_BITACORA)


# ==========================================================================
#  v7.1 · D52/D53/D55 · QR DE ASISTENCIA DE UNA SESIÓN Y DISPONIBILIDAD DE HORARIO
# --------------------------------------------------------------------------
#  Antes lo hacía el ponente desde su panel; como ahora solo el administrador inicia sesión, vive aquí.
#  El administrador abre la pantalla del QR de la sesión (para proyectarlo o compartirlo al final) y ve en
#  vivo quién registró su asistencia. Un horario NO está disponible si otra sesión se traslapa con él en la
#  misma sede FÍSICA y el mismo salón (sin salón escrito cuenta como choque, es lo prudente; una sede virtual
#  no tiene salón y no se revisa) o, desde la D59, si queda a menos de una hora de CUALQUIER otra sesión
#  (de cualquier sede o modalidad: todas usan el mismo enlace). La regla la calcula la BD (sp_sesiones_en_conflicto,
#  migraciones 050 y 059).
# ==========================================================================
_MOTIVO = {'LUGAR': 'el mismo salón en esa sede', 'PONENTE': 'ya participa la misma persona'}

# v7.1 · D59: entre dos sesiones (de cualquier cátedra, sede o modalidad) debe haber al menos esto, porque todas usan el
# mismo enlace de ingreso. La BD manda (parámetro MINUTOS_ENTRE_SESIONES, 60 por defecto); aquí solo se usa para
# avisar de las sesiones escritas en el MISMO formulario, que la BD todavía no ve.
MINUTOS_ENTRE_SESIONES = 60


def _motivo_choque(m, c):
    if m == 'HORARIO':
        return ('es a la misma hora (no puede haber dos cátedras a la vez)' if c.get('solapa')
                else 'queda a menos de una hora de distancia (todas las cátedras usan el mismo enlace)')
    return _MOTIVO.get(m, m)


def _describe_choque(c):
    """Una sesión que choca, en una línea legible (hora de Colombia)."""
    motivos = ' y '.join(_motivo_choque(m, c) for m in (c.get('motivos') or []))
    donde = ', '.join(x for x in (c.get('fk_sede'), c.get('lugar')) if x)
    return (f"«{c['nombre_catedra']}» (reunión {c['numero_reunion']}, "
            f"{fecha_hora(c['inicio'], '%Y-%m-%d %H:%M')}–{fecha_hora(c['fin'], '%H:%M')}"
            f"{', ' + donde if donde else ''}): {motivos}")


def _conflictos_de_horario(inicio, fin, sede=None, lugar=None):
    """Sesiones que chocan con ese horario por salón. 'inicio'/'fin': texto ISO con zona. Lanza ErrorApi."""
    r = consultar('Disponibilidad/conflictos', inicio=inicio, fin=fin, fkSede=sede or None, lugar=lugar or None)
    return r['conflictos']


def _choques_del_formulario(filas):
    """Textos con los choques de las sesiones escritas: contra la BD y entre ellas mismas (dos que se
    traslapan en la misma sede física y el mismo salón, o sin salón escrito)."""
    avisos = []
    rangos = []
    for n, f in enumerate(filas, start=1):
        inicio, fin = desde_input(f['inicio']), desde_input(f['fin'])
        for c in _conflictos_de_horario(inicio, fin, f.get('fk_sede'), f.get('lugar')):
            avisos.append(f"Sesión {n} choca con {_describe_choque(c)}.")
        try:
            rangos.append((n, datetime.fromisoformat(inicio), datetime.fromisoformat(fin), f.get('fk_sede'),
                           (f.get('lugar') or '').strip().lower()))
        except (TypeError, ValueError):
            pass        # fecha ilegible: la API la rechaza con su 422 al crear
    margen = timedelta(minutes=MINUTOS_ENTRE_SESIONES)
    for i, (n1, a1, b1, s1, l1) in enumerate(rangos):
        for n2, a2, b2, s2, l2 in rangos[i + 1:]:
            if a1 < b2 + margen and b1 + margen > a2:
                avisos.append(f"Las sesiones {n1} y {n2} de esta misma cátedra se cruzan o quedan a menos de una hora "
                              "una de otra: todas las cátedras usan el mismo enlace, así que no pueden estar pegadas.")
    return avisos


def _hora_choque(c):
    return {**c, 'hora': f"{fecha_hora(c['inicio'], '%H:%M')}–{fecha_hora(c['fin'], '%H:%M')}",
            'fecha': fecha_hora(c['inicio'], '%Y-%m-%d'),
            'texto': _describe_choque(c) if c.get('motivos') else None}


@admin_bp.route('/catedras/disponibilidad')
def catedras_disponibilidad():
    """¿Está libre este horario? ?inicio=&fin= (datetime-local) &sede= &lugar= . JSON para static/disponibilidad.js."""
    inicio, fin = desde_input(request.args.get('inicio')), desde_input(request.args.get('fin'))
    if not inicio or not fin:
        return jsonify(error='Escribe el inicio y el fin para consultar.'), 400
    try:
        conflictos = _conflictos_de_horario(inicio, fin, request.args.get('sede'), request.args.get('lugar'))
    except ErrorApi as ex:
        return jsonify(error=f"{ex.mensaje} {ex.detalle or ''}".strip()), (400 if ex.codigo in (400, 422) else 502)
    # v7.1 · D57: además de los cruces, ¿el horario cabe en los periodos y horarios de programación? (None = sí)
    fuera = admin_restricciones.fuera_de_periodo(inicio, fin)
    return jsonify(disponible=not conflictos and not fuera, conflictos=[_hora_choque(c) for c in conflictos],
                   fuera_de_periodo=fuera)


@admin_bp.route('/catedras/agenda')
def catedras_agenda():
    """Qué hay ocupado en una fecha: ?fecha=AAAA-MM-DD &sede= (opcional)."""
    try:
        dia = datetime.strptime(request.args.get('fecha', ''), '%Y-%m-%d').replace(tzinfo=ZONA_COLOMBIA)
    except ValueError:
        return jsonify(error='Elige una fecha.'), 400
    try:
        r = consultar('Disponibilidad/agenda', desde=dia.isoformat(), hasta=(dia + timedelta(days=1)).isoformat(),
                      fkSede=request.args.get('sede') or None)
    except ErrorApi as ex:
        return jsonify(error=f"{ex.mensaje} {ex.detalle or ''}".strip()), 502
    return jsonify(fecha=dia.strftime('%Y-%m-%d'), sesiones=[_hora_choque(c) for c in r['datos']])


def _resumen_asistencia(id_sesion):
    """(filas, total) de quienes registraron su asistencia a la sesión (con hora y ubicación)."""
    r = consultar('QrSesion/asistencia', fkSesion=id_sesion)
    filas = r['datos'] if r else []
    return filas, len(filas)


@admin_bp.route('/sesiones/<int:id_sesion>/qr', methods=['GET', 'POST'])
def sesiones_qr(id_sesion):
    # v7.2 · D73: quien decide qué se muestra es el servidor. La lista de asistentes (datos de personas)
    # es solo del ADMIN; para el ASISTENTE ni siquiera se consulta a la API.
    puede_ver_asistencia = tiene_rol(ROL_ADMIN)
    try:
        sesion = obtener('Sesion', id_sesion)
        catedra = obtener('Catedra', sesion['fk_catedra'])
    except ErrorApi as ex:
        if ex.codigo == 404:
            abort(404)
        _avisar(ex)
        return redirect(url_for('admin.sesiones_listar' if puede_ver_asistencia else 'admin.catedras_listar'))

    if request.method == 'POST':
        regenerar = request.form.get('regenerar') == '1'
        try:
            qr = emitir_qr(id_sesion, usuario_actual().get('id_usuario'), regenerar)
            flash('Se generó un QR nuevo (el anterior ya no sirve).' if regenerar and not qr['ya_existia']
                  else 'El QR de la sesión está listo.', 'exito')
        except ErrorApi as ex:
            _avisar(ex)
        return redirect(url_for('admin.sesiones_qr', id_sesion=id_sesion))

    error = None
    qr = None
    try:
        qr = qr_de_sesion(id_sesion)
    except ErrorApi as ex:
        error = ex
    filas, total = [], 0
    if puede_ver_asistencia:
        try:
            filas, total = _resumen_asistencia(id_sesion)
        except ErrorApi as ex:
            error = error or ex

    url = svg = None
    if qr:
        url = url_de_registro(qr['token'])
        svg = svg_qr(url)
    return render_template('admin/sesion_qr.html', sesion=sesion, catedra=catedra, qr=qr, estado=estado_de_ventana(qr),
                           url=url, svg=svg, filas=filas, total=total, error=error,
                           puede_ver_asistencia=puede_ver_asistencia,
                           desde=a_local(qr['ventana_desde']) if qr else None,
                           hasta=a_local(qr['ventana_hasta']) if qr else None)


@admin_bp.route('/sesiones/<int:id_sesion>/asistencia')
def sesiones_asistencia_json(id_sesion):
    """JSON para refrescar la lista sin recargar la página (lo llama la pantalla del QR)."""
    try:
        filas, total = _resumen_asistencia(id_sesion)
        estado = estado_de_ventana(qr_de_sesion(id_sesion))
    except ErrorApi as ex:
        return jsonify(error=f"{ex.mensaje} {ex.detalle or ''}".strip()), 502
    for f in filas:
        f['hora'] = fecha_hora(f['registrado_en'], '%H:%M:%S') if f.get('registrado_en') else None
    return jsonify(filas=filas, total=total, estado=estado)
