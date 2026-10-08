# ==========================================================================
#  routes/catedras.py  ·  Front Cátedras · Versión 2
# --------------------------------------------------------------------------
#  Páginas PÚBLICAS: inicio, catálogo de cátedras y detalle de una cátedra.
#
#  Catálogo (RF90, ADR D13 de version2/4_research.md): la ruta pide a la
#  API TODAS las cátedras en UNA sola llamada (GET /api/Catedra sin
#  'limite') y resuelve aquí, en Python, las dos cosas que la pantalla
#  necesita:
#    1. el FILTRO  ('?q=' texto en el nombre, '?tipo=' tipo de evento),
#       sobre el catálogo COMPLETO, no solo sobre la página visible;
#    2. la PAGINACIÓN ('?pagina='), cortando la lista con slicing.
#  Sin JavaScript nuevo y sin cambiar el contrato de la API.
#
#  RF93: si la API falla, se MUESTRA el error. Nunca se inventan datos.
#
#  v4: el detalle de la cátedra agrega su CALENDARIO de sesiones
#  (GET /api/Sesion?fkCatedra={id}); cada sesión enlaza a su detalle
#  (routes/sesiones.py).
# ==========================================================================

from flask import Blueprint, abort, render_template, request

from servicios.api_catedras import ErrorApi, listar, obtener

catedras_bp = Blueprint('catedras', __name__)

# Cuántas cátedras se muestran por página del catálogo.
POR_PAGINA = 24

# Segundos de espera para traer las 5502 cátedras en una sola llamada.
TIMEOUT_CATALOGO = 30


@catedras_bp.route('/')
def inicio():
    return render_template('inicio.html')


def _numero_pagina(texto):
    """Convierte '?pagina=' a entero >= 1; cualquier cosa rara -> 1."""
    try:
        return max(1, int(texto))
    except (TypeError, ValueError):
        return 1


@catedras_bp.route('/catedras')
def catalogo():
    # Parámetros de la URL. '.strip()' quita espacios sobrantes.
    texto_busqueda = request.args.get('q', '').strip()
    tipo = request.args.get('tipo', '').strip()
    pagina = _numero_pagina(request.args.get('pagina'))

    try:
        # UNA sola llamada: las 5502 cátedras.
        catedras = listar('Catedra', timeout=TIMEOUT_CATALOGO)
        # Tipos de evento (tabla de la v1) para el <select> del filtro.
        tipos_evento = listar('TipoEvento')
    except ErrorApi as ex:
        # RF93: el error se muestra tal cual; la lista queda vacía.
        return render_template('catedras.html', error=ex, catedras=[], tipos_evento=[],
                               q=texto_busqueda, tipo=tipo, pagina=1, total_paginas=0,
                               total_filtrado=0, total_catalogo=0), 502

    total_catalogo = len(catedras)

    # 1 · FILTRO por texto libre en el nombre (sin distinguir mayúsculas).
    if texto_busqueda:
        buscado = texto_busqueda.lower()
        catedras = [c for c in catedras if buscado in c['nombre'].lower()]

    # 1b · FILTRO por tipo de evento. CHAR(4) puede traer espacios ('MTG '),
    #      por eso se comparan los dos lados sin espacios.
    if tipo:
        catedras = [c for c in catedras if c['fk_tipo_evento'].strip() == tipo.strip()]

    # 2 · PAGINACIÓN con slicing. '-(-a // b)' = división redondeando hacia arriba.
    total_filtrado = len(catedras)
    total_paginas = max(1, -(-total_filtrado // POR_PAGINA))
    pagina = min(pagina, total_paginas)
    inicio_corte = (pagina - 1) * POR_PAGINA
    pagina_actual = catedras[inicio_corte:inicio_corte + POR_PAGINA]

    # Diccionario código -> nombre para mostrar el tipo de evento legible.
    nombres_tipo = {t['id_tipo_evento'].strip(): t['nombre'] for t in tipos_evento}

    return render_template('catedras.html', error=None, catedras=pagina_actual,
                           tipos_evento=tipos_evento, nombres_tipo=nombres_tipo,
                           q=texto_busqueda, tipo=tipo, pagina=pagina,
                           total_paginas=total_paginas, total_filtrado=total_filtrado,
                           total_catalogo=total_catalogo)


@catedras_bp.route('/catedras/<int:id_catedra>')
def detalle(id_catedra):
    # RF91: la cátedra, y si tiene dependencia, también su nombre.
    try:
        catedra = obtener('Catedra', id_catedra)
    except ErrorApi as ex:
        if ex.codigo == 404:
            abort(404)  # cátedra inexistente -> página 404 del front
        return render_template('detalle_catedra.html', error=ex, catedra=None,
                               dependencia=None, tipo_evento=None, sesiones=[],
                               error_sesiones=None), 502

    dependencia = None
    tipo_evento = None
    error = None
    try:
        if catedra.get('fk_dependencia'):
            dependencia = obtener('Dependencia', catedra['fk_dependencia'])
        tipo_evento = obtener('TipoEvento', catedra['fk_tipo_evento'])
    except ErrorApi as ex:
        # La cátedra se muestra igual; el dato secundario que falló, se avisa.
        error = ex

    # v4 · Calendario de la cátedra (criterio 8 de version4/2_spec.md): la
    # API filtra en SQL con '?fkCatedra=' — nunca se bajan las 5714 sesiones.
    # Un error aquí se muestra APARTE: la ficha de la cátedra sigue visible.
    sesiones = []
    error_sesiones = None
    try:
        sesiones = listar('Sesion', fkCatedra=id_catedra)
    except ErrorApi as ex:
        error_sesiones = ex

    return render_template('detalle_catedra.html', error=error, catedra=catedra,
                           dependencia=dependencia, tipo_evento=tipo_evento,
                           sesiones=sesiones, error_sesiones=error_sesiones)
