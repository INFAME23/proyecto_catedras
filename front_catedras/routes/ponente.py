# ==========================================================================
#  routes/ponente.py  ·  Front Cátedras · Versión 4
# --------------------------------------------------------------------------
#  CALENDARIO DEL PONENTE (mapa de versiones §4: "V4 · Sesiones, detalle
#  de sesión y calendario del ponente").
#
#  v4: no había login, así que la persona se ELIGE: se busca (la API filtra
#  con '?q=') y se abre su calendario con '?asistente=ID'.
#  v5 en adelante: si hay sesión, el calendario que se abre por defecto es
#  el de la persona autenticada (su 'fk_asistente'). La búsqueda se
#  conserva para consultar el de otra persona (la API sigue abierta,
#  ADR D28-D29: se autentica, no se autoriza).
#  v7.1 (RF-S3): la pantalla ya no es pública; exige el rol PONENTE o ADMIN.
#  v7.1 (D47): un PONENTE solo ve SU calendario; buscar a otra persona o abrir
#  '?asistente=<otro>' es solo de ADMIN (un ponente no ve la agenda de los demás).
#
#  Es el único lugar del front que usa el filtro 'fkAsistente' de
#  GET /api/Ponencia (RF127): "¿en qué sesiones participa esta persona?".
# ==========================================================================

from flask import Blueprint, abort, flash, render_template, request

from auth_helpers import ROL_ADMIN, ROL_PONENTE, exigir_rol, sesion_activa, tiene_rol, usuario_actual
from servicios.api_catedras import ErrorApi, listar, obtener
from servicios.fechas import ZONA_COLOMBIA, a_local
from datetime import datetime

ponente_bp = Blueprint('ponente', __name__)


@ponente_bp.before_request
def _solo_ponentes():
    """v7.1 · RF-S3: el calendario exige sesión con el rol PONENTE (o ADMIN, que
    lo enlaza desde su panel y consulta el de cualquier persona). Antes era
    público (v4, cuando no había login); sin sesión -> /login, sin rol -> 403."""
    return exigir_rol(ROL_PONENTE, ROL_ADMIN)

# Cuántas personas se muestran en el resultado de la búsqueda.
RESULTADOS_BUSQUEDA = 20


def _avisar(ex):
    flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')


def _calendario_de(id_asistente):
    """Las participaciones de una persona, cada una con su sesión y su
    cátedra, ordenadas por fecha. Una sesión con 2 roles sale una vez con
    los 2 roles juntos."""
    ponencias = listar('Ponencia', fkAsistente=id_asistente)

    por_sesion = {}      # id_sesion -> {'sesion', 'catedra', 'roles'}
    catedras = {}        # caché: una cátedra se pide una sola vez
    for ponencia in ponencias:
        id_sesion = ponencia['fk_sesion']
        if id_sesion not in por_sesion:
            sesion = obtener('Sesion', id_sesion)
            id_catedra = sesion['fk_catedra']
            if id_catedra not in catedras:
                catedras[id_catedra] = obtener('Catedra', id_catedra)
            por_sesion[id_sesion] = {'sesion': sesion, 'catedra': catedras[id_catedra], 'roles': []}
        por_sesion[id_sesion]['roles'].append(ponencia['rol'])

    # 'sorted' con 'key': ordena por el instante de inicio de cada sesión.
    # Si una fecha no se pudiera leer, esa fila va al principio (datetime.min).
    minimo = datetime.min.replace(tzinfo=ZONA_COLOMBIA)
    return sorted(por_sesion.values(),
                  key=lambda fila: a_local(fila['sesion']['inicio']) or minimo)


@ponente_bp.route('/')
def inicio():
    mi_asistente = usuario_actual()['fk_asistente'] if sesion_activa() else None
    es_admin = tiene_rol(ROL_ADMIN)

    # D47: solo un ADMIN busca o abre el calendario de otra persona. Un PONENTE
    # que pida el de otro (?asistente=) recibe 403 y '?q=' se ignora.
    texto = request.args.get('q', '').strip() if es_admin else ''
    id_asistente = request.args.get('asistente', type=int)   # None si no llega o no es número
    if not es_admin and id_asistente is not None and id_asistente != mi_asistente:
        abort(403)

    # v5+: con sesión y sin persona elegida, se abre el calendario propio.
    # Sin ninguna búsqueda en curso: si el usuario está buscando a otra
    # persona ('?q='), no se le impone el suyo.
    if id_asistente is None and not texto and mi_asistente:
        id_asistente = mi_asistente

    personas = []
    if texto:
        try:
            personas = listar('Asistente', limite=RESULTADOS_BUSQUEDA, q=texto)
        except ErrorApi as ex:
            _avisar(ex)

    asistente = None
    calendario = []
    if id_asistente:
        try:
            asistente = obtener('Asistente', id_asistente)
            calendario = _calendario_de(id_asistente)
        except ErrorApi as ex:
            _avisar(ex)

    return render_template('ponente_calendario.html', q=texto, personas=personas,
                           asistente=asistente, calendario=calendario,
                           mi_asistente=mi_asistente, puede_buscar=es_admin)
