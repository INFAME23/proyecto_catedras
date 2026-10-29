# ==========================================================================
#  routes/estudiante.py  ·  Front Cátedras · Subversión v7.1 (RF-S2, RF-S3)
# --------------------------------------------------------------------------
#  PANEL DEL ESTUDIANTE. Antes era un placeholder ("depende de tablas que
#  ApiCatedras aún no expone"); eso ya no es cierto: asistente, usuario,
#  rol_por_usuario, registro_asistencia, programa_asistente y
#  vinculacion_asistente existen en la API desde la v3 y la v5.
#
#  ACCESO (mismo patrón que admin.py): todo el blueprint exige sesión con el
#  rol ESTUDIANTE (before_request, UN solo lugar). Sin sesión -> /login; con
#  sesión pero sin el rol -> 403. Es una protección del FRONT; la API sigue
#  abierta (ADR D28).
#
#  QUÉ MUESTRA (solo lectura: los datos de identidad los administra el
#  administrador con la carga de archivos, no el propio estudiante):
#    1. Mi información: nombre, correos, programa(s) y vinculación.
#    2. Mis sesiones: cada registro_asistencia de la persona, con su sesión,
#       su cátedra y la ubicación desde la que se registró.
#    3. Registrar asistencia: pegar el enlace/token de una sesión SIN cerrar
#       sesión. Reutiliza /registro (que es público y funciona igual con o
#       sin cuenta), por eso la ubicación obligatoria (RF-S1) aplica igual.
#    4. Cambiar la clave: /perfil/clave (RF-S5).
#
#  La persona se toma de la SESIÓN (usuario.fk_asistente), nunca de la URL:
#  un estudiante no puede ver los datos de otro cambiando un número.
# ==========================================================================

from flask import Blueprint, flash, redirect, render_template, url_for

from auth_helpers import ROL_ESTUDIANTE, exigir_rol, usuario_actual
from servicios.api_catedras import ErrorApi, crear, listar, obtener
from servicios.fechas import ZONA_COLOMBIA, a_local
from datetime import datetime

estudiante_bp = Blueprint('estudiante', __name__)


@estudiante_bp.before_request
def _solo_estudiantes():
    """Guardia de TODO '/estudiante' (redirección a /login o 403)."""
    return exigir_rol(ROL_ESTUDIANTE)


def _avisar(ex):
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def _nombre_de(recurso, llave, campo='nombre'):
    """El nombre legible de un código de catálogo (programa, tipo de
    vinculación). Si no se puede leer, se muestra el código tal cual."""
    try:
        return obtener(recurso, llave)[campo]
    except (ErrorApi, KeyError, TypeError):
        return llave


def _mis_sesiones(id_asistente):
    """Los registros de asistencia de la persona, cada uno con su sesión y
    su cátedra, del más reciente al más antiguo."""
    registros = listar('RegistroAsistencia', fkAsistente=id_asistente)

    sesiones = {}     # caché: una sesión / cátedra se pide una sola vez
    catedras = {}
    filas = []
    for registro in registros:
        id_sesion = registro['fk_sesion']
        try:
            if id_sesion not in sesiones:
                sesiones[id_sesion] = obtener('Sesion', id_sesion)
            sesion = sesiones[id_sesion]
            if sesion['fk_catedra'] not in catedras:
                catedras[sesion['fk_catedra']] = obtener('Catedra', sesion['fk_catedra'])
        except ErrorApi as ex:
            _avisar(ex)
            continue
        filas.append({'registro': registro, 'sesion': sesion,
                      'catedra': catedras[sesion['fk_catedra']]})

    minimo = datetime.min.replace(tzinfo=ZONA_COLOMBIA)
    filas.sort(key=lambda f: a_local(f['registro']['registrado_en']) or minimo, reverse=True)
    return filas


@estudiante_bp.route('/')
def inicio():
    id_asistente = usuario_actual()['fk_asistente']

    asistente = None
    programas = []
    vinculaciones = []
    sesiones = []

    try:
        asistente = obtener('Asistente', id_asistente)
    except ErrorApi as ex:
        _avisar(ex)

    try:
        for fila in listar('ProgramaAsistente', fkAsistente=id_asistente):
            programas.append({**fila, 'nombre': _nombre_de('ProgramaAcademico', fila['fk_programa'])})
        # El periodo más reciente primero ('2026-2' > '2026-1' como texto).
        programas.sort(key=lambda p: p['fk_periodo'], reverse=True)

        for fila in listar('VinculacionAsistente', fkAsistente=id_asistente):
            vinculaciones.append({**fila, 'nombre': _nombre_de('TipoVinculacion', fila['fk_tipo_vinculacion'])})
        vinculaciones.sort(key=lambda v: v['fecha_ini'], reverse=True)
    except ErrorApi as ex:
        _avisar(ex)

    try:
        sesiones = _mis_sesiones(id_asistente)
    except ErrorApi as ex:
        _avisar(ex)

    inscripciones = []
    try:
        inscripciones = listar('InscripcionCatedra', fkAsistente=id_asistente)   # solo las ACTIVAS
    except ErrorApi as ex:
        _avisar(ex)

    return render_template('estudiante_panel.html', asistente=asistente, programas=programas,
                           vinculaciones=vinculaciones, sesiones=sesiones, inscripciones=inscripciones)


# --------------------------------------------------------------------------
#  v7.1 · D50 · Inscribirse a una cátedra y cancelar la inscripción.
#  La persona SIEMPRE sale de la sesión (usuario_actual()), nunca de un campo
#  del formulario: un estudiante solo se inscribe (o se da de baja) a sí mismo.
# --------------------------------------------------------------------------
def _accion_inscripcion(id_catedra, recurso, exito):
    try:
        respuesta = crear(recurso, {'fk_catedra': id_catedra,
                                    'fk_asistente': usuario_actual()['fk_asistente']})
        flash(exito.get(respuesta.get('estado'), respuesta.get('mensaje', 'Listo.')), 'exito')
    except ErrorApi as ex:
        if ex.codigo == 404:
            flash('Esa cátedra ya no está disponible o no tienes una inscripción activa en ella.', 'error')
        else:
            _avisar(ex)
    return redirect(url_for('catedras.detalle', id_catedra=id_catedra))


@estudiante_bp.route('/catedras/<int:id_catedra>/inscribirse', methods=['POST'])
def inscribirse(id_catedra):
    return _accion_inscripcion(id_catedra, 'InscripcionCatedra', {
        'inscrita': 'Quedaste inscrito en la cátedra.',
        'ya_inscrita': 'Ya estabas inscrito en esta cátedra.'})


@estudiante_bp.route('/catedras/<int:id_catedra>/cancelar-inscripcion', methods=['POST'])
def cancelar_inscripcion(id_catedra):
    return _accion_inscripcion(id_catedra, 'InscripcionCatedra/cancelar', {
        'cancelada': 'Cancelaste tu inscripción.'})
