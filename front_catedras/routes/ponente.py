# ==========================================================================
#  routes/ponente.py  ·  Front Cátedras · Versión 4
# --------------------------------------------------------------------------
#  CALENDARIO DEL PONENTE (mapa de versiones §4: "V4 · Sesiones, detalle
#  de sesión y calendario del ponente").
#
#  Hasta la v5 NO hay login: el front no sabe "quién soy". Por eso, igual
#  que el selector de rol de prueba, aquí se ELIGE a la persona: se busca
#  (la API filtra con '?q=') y se abre su calendario con '?asistente=ID'.
#  En la v5, con 'usuario' y login real, ese id saldrá de la sesión
#  autenticada y la búsqueda desaparece.
#
#  Es el único lugar del front que usa el filtro 'fkAsistente' de
#  GET /api/Ponencia (RF127): "¿en qué sesiones participa esta persona?".
# ==========================================================================

from flask import Blueprint, flash, render_template, request

from servicios.api_catedras import ErrorApi, listar, obtener
from servicios.fechas import ZONA_COLOMBIA, a_local
from datetime import datetime

ponente_bp = Blueprint('ponente', __name__)

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
    texto = request.args.get('q', '').strip()
    id_asistente = request.args.get('asistente', type=int)   # None si no llega o no es número

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
                           asistente=asistente, calendario=calendario)
