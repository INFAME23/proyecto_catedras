# ==========================================================================
#  routes/sesiones.py  ·  Front Cátedras · Versión 4
# --------------------------------------------------------------------------
#  Detalle de UNA sesión: sus datos, la lista de ponentes con su rol y un
#  formulario para asignar uno nuevo o quitar uno existente (criterio 8 de
#  version4/2_spec.md).
#
#  Guía IA, Regla 2 (las FK se seleccionan, no se digitan):
#    · 'fk_sesion' lo fija la URL (/sesiones/<id>), como 'fk_asistente' en
#      la ficha del asistente de la v3.
#    · 'fk_asistente' sale de un <select>. Con 14.811 personas no se puede
#      poner a todas en un <select>: primero se BUSCA (la API filtra con
#      '?q=', ADR D18 de la v3) y el <select> se llena con los resultados.
#    · 'rol' sale de un <select> con la lista cerrada.
#
#  Ponencia es de LLAVE COMPUESTA (ADR D19): para quitar a alguien se manda
#  la llave completa (sesión + asistente + rol) y la API hace un DELETE.
#
#  Las ayudas 'ponentes_de' y 'buscar_candidatos' las reutiliza también el
#  panel de administración (routes/admin.py).
# ==========================================================================

from flask import Blueprint, abort, flash, redirect, render_template, request, url_for

from datetime import datetime, timezone

from servicios.api_catedras import ErrorApi, crear, eliminar, listar, llave, obtener
from servicios.fechas import a_local
from auth_helpers import ROL_ADMIN, rol_requerido, tiene_rol
from servicios.qr_sesiones import estado_de_ventana, qr_de_sesion
from routes.encuestas import estado_encuesta_sesion   # v6: ¿se ofrece "responder encuesta"?

sesiones_bp = Blueprint('sesiones', __name__)

# Lista cerrada de 'ponencia.rol' (refleja chk_ponencia_rol).
ROLES_PONENCIA = ['PONENTE', 'MODERADOR', 'INVITADO', 'ORGANIZADOR']

# Cuántas personas se ofrecen en el <select> después de una búsqueda.
CANDIDATOS_POR_BUSQUEDA = 20


def _avisar(ex):
    """Muestra el error de la API (mensaje + detalle) como alerta roja."""
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def ponentes_de(id_sesion):
    """Las participaciones de una sesión, cada una con los datos de su asistente.

    La API devuelve solo la llave (fk_sesion, fk_asistente, rol); el nombre
    se pide aparte con GET /api/Asistente/{id}. Una sesión tiene pocos
    ponentes, así que una llamada por cada uno es aceptable aquí.
    """
    ponencias = listar('Ponencia', fkSesion=id_sesion)
    cache = {}   # un asistente con 2 roles se pide una sola vez
    for ponencia in ponencias:
        id_asistente = ponencia['fk_asistente']
        if id_asistente not in cache:
            try:
                cache[id_asistente] = obtener('Asistente', id_asistente)
            except ErrorApi:
                cache[id_asistente] = None   # se muestra solo el id
        ponencia['asistente'] = cache[id_asistente]
    return ponencias


def buscar_candidatos(texto):
    """Personas que coinciden con 'texto' para llenar el <select> (Regla 2).
    Sin texto no se busca nada: nunca se descarga la tabla completa."""
    if not texto:
        return []
    try:
        return listar('Asistente', limite=CANDIDATOS_POR_BUSQUEDA, q=texto)
    except ErrorApi as ex:
        _avisar(ex)
        return []


def asignar_ponente(id_sesion, formulario):
    """POST /api/Ponencia con la sesión fija y lo elegido en el formulario.
    Devuelve True si la API lo aceptó (los errores se muestran con flash)."""
    try:
        fk_asistente = int(formulario.get('fk_asistente', ''))
    except ValueError:
        flash('Selecciona una persona de la lista (primero búscala).', 'error')
        return False

    datos = {'fk_sesion': id_sesion, 'fk_asistente': fk_asistente,
             'rol': formulario.get('rol') or 'PONENTE'}
    try:
        respuesta = crear('Ponencia', datos)
    except ErrorApi as ex:
        # 500 típico: esa persona YA tiene ese rol en esta sesión (PK duplicada).
        _avisar(ex)
        return False
    flash(respuesta.get('mensaje', 'Participación asignada.'), 'exito')
    return True


def quitar_ponente(id_sesion, formulario):
    """DELETE /api/Ponencia/{sesion}/{asistente}/{rol}: la llave completa en la URL."""
    try:
        respuesta = eliminar('Ponencia', llave(id_sesion, formulario.get('fk_asistente', ''),
                                               formulario.get('rol', '')))
        flash(respuesta.get('mensaje', 'Participación retirada.'), 'exito')
    except ErrorApi as ex:
        _avisar(ex)


def puede_migrar(sesion):
    """v7 · ¿Se ofrece "Generar migración" en el detalle de la sesión?
    Solo a un ADMIN (la acción vive bajo /admin, D37-E), si la sesión ya
    terminó y tiene al menos un registro de asistencia. No se revisa aquí
    si los registros son migrables (con ID de ASIS y programa, no
    externos): esa regla es de la base de datos, y si no hay ninguno la
    API lo dice al generar (D9)."""
    if not tiene_rol(ROL_ADMIN):
        return False
    fin = a_local(sesion.get('fin'))
    if fin is None or fin > datetime.now(timezone.utc):
        return False
    try:
        return bool(listar('RegistroAsistencia', limite=1, fkSesion=sesion['id_sesion']))
    except ErrorApi:
        return False   # si no se puede saber, no se ofrece el botón


def ventana_del_qr(sesion):
    """v7.1 · D55 · Lo que cualquiera puede saber del QR de cierre de esta sesión: desde y hasta qué hora sirve y
    en qué estado está. NO se devuelve el token: la asistencia se registra escaneando el QR que muestra el ponente
    al final (si el token se publicara aquí, el QR no probaría que se estuvo presente)."""
    try:
        qr = qr_de_sesion(sesion['id_sesion'])
    except ErrorApi:
        return None
    if not qr:
        return None
    return {'desde': a_local(qr.get('ventana_desde')), 'hasta': a_local(qr.get('ventana_hasta')),
            'estado': estado_de_ventana(qr)}


@sesiones_bp.route('/sesiones/<int:id_sesion>')
def detalle(id_sesion):
    try:
        sesion = obtener('Sesion', id_sesion)
    except ErrorApi as ex:
        if ex.codigo == 404:
            abort(404)   # sesión inexistente -> página 404 del front
        return render_template('detalle_sesion.html', error=ex, sesion=None, catedra=None,
                               ponentes=[], candidatos=[], q='', roles=ROLES_PONENCIA,
                               mostrar_encuesta=False, motivo_encuesta=None, puede_editar=False,
                               ventana_qr=None), 502

    error = None
    catedra = None
    ponentes = []
    try:
        catedra = obtener('Catedra', sesion['fk_catedra'])
        ponentes = ponentes_de(id_sesion)
    except ErrorApi as ex:
        # La sesión se muestra igual; el dato secundario que falló, se avisa.
        error = ex

    # v6: ¿se ofrece el enlace "responder encuesta" (criterio 7)?
    mostrar_encuesta, motivo_encuesta = estado_encuesta_sesion(sesion)

    texto = request.args.get('q', '').strip()
    return render_template('detalle_sesion.html', error=error, sesion=sesion, catedra=catedra,
                           ponentes=ponentes, q=texto,
                           candidatos=buscar_candidatos(texto) if tiene_rol(ROL_ADMIN) else [],
                           roles=ROLES_PONENCIA, mostrar_encuesta=mostrar_encuesta,
                           motivo_encuesta=motivo_encuesta, migracion=puede_migrar(sesion),
                           puede_editar=tiene_rol(ROL_ADMIN),
                           ventana_qr=ventana_del_qr(sesion))


# v7.1: asignar y quitar personas de una sesión (con su rol) es de ADMIN. Antes
# estas dos rutas no pedían sesión: cualquiera, incluido un estudiante, podía
# darle a cualquier persona el rol que quisiera.
@sesiones_bp.route('/sesiones/<int:id_sesion>/ponentes', methods=['POST'])
@rol_requerido(ROL_ADMIN)
def asignar(id_sesion):
    asignar_ponente(id_sesion, request.form)
    # Post/Redirect/Get: al volver, la lista ya incluye al nuevo ponente.
    return redirect(url_for('sesiones.detalle', id_sesion=id_sesion) + '#ponentes')


@sesiones_bp.route('/sesiones/<int:id_sesion>/ponentes/quitar', methods=['POST'])
@rol_requerido(ROL_ADMIN)
def quitar(id_sesion):
    quitar_ponente(id_sesion, request.form)
    return redirect(url_for('sesiones.detalle', id_sesion=id_sesion) + '#ponentes')
