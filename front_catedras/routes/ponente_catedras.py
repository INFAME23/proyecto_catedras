# ==========================================================================
#  routes/ponente_catedras.py  ·  Front Cátedras · v7.1 · D49
# --------------------------------------------------------------------------
#  EL PONENTE CREA CÁTEDRAS Y SE ASIGNA A LAS EXISTENTES.
#
#  La participación (Ponencia) es por SESIÓN, no por cátedra: "asignarse a una
#  cátedra" es quedar como PONENTE en sus sesiones (todas o las que elija).
#
#    · /ponente/catedras/nueva      crea la cátedra con sus sesiones (el mismo
#                                   maestro-detalle de /admin, sin encuestas)
#                                   y deja al creador como PONENTE de todas.
#    · /ponente/catedras            busca cátedras activas ya creadas (por otro
#                                   ponente o por el administrador).
#    · /ponente/catedras/<id>       sus sesiones, con casillas para asignarse
#                                   («Asignarme») o retirarse («Quitarme»).
#
#  Siempre actúa sobre la persona de la SESIÓN (usuario_actual()), nunca sobre
#  un id que llegue en el formulario: un ponente no puede asignar ni quitar a
#  otro. Los ids de sesión que llegan se comparan contra las sesiones reales de
#  esa cátedra. Asignar a TERCEROS sigue siendo del administrador.
# ==========================================================================

from flask import Blueprint, abort, flash, redirect, render_template, request, url_for

from auth_helpers import ROL_ADMIN, ROL_PONENTE, exigir_rol, usuario_actual
from routes.admin import (CAMPOS_SESION_FILA, _catalogos_sesion, _filas, _opciones,
                          _sesion_de_fila)
from routes.ponente import _avisar, _calendario_de
from servicios.api_catedras import ErrorApi, crear, eliminar, listar, llave, obtener

ponente_catedras_bp = Blueprint('ponente_catedras', __name__)

CATEDRAS_POR_PAGINA = 20


@ponente_catedras_bp.before_request
def _solo_ponentes():
    """Misma regla que el resto de /ponente: rol PONENTE (o ADMIN)."""
    return exigir_rol(ROL_PONENTE, ROL_ADMIN)


def _mi_asistente():
    return usuario_actual()['fk_asistente']


def _mis_sesiones():
    """{id_sesion} donde YO figuro como PONENTE (de cualquier cátedra)."""
    return {p['fk_sesion'] for p in listar('Ponencia', fkAsistente=_mi_asistente()) if p['rol'] == 'PONENTE'}


def _catedras_donde_participo():
    """{id_catedra} de las cátedras donde tengo al menos una participación."""
    return {fila['catedra']['id_catedra'] for fila in _calendario_de(_mi_asistente())}


def _asignar_a_sesiones(sesiones):
    """Me deja como PONENTE en cada sesión de la lista. Devuelve (hechas, error)."""
    hechas = 0
    for s in sesiones:
        try:
            crear('Ponencia', {'fk_sesion': s['id_sesion'], 'fk_asistente': _mi_asistente(), 'rol': 'PONENTE'})
            hechas += 1
        except ErrorApi as ex:
            return hechas, ex
    return hechas, None


def _render_nueva(datos, errores, filas=()):
    return render_template('ponente_catedra_nueva.html', catedra=datos, errores=errores,
                           filas_sesiones=list(filas), tipos_evento=_opciones('TipoEvento'),
                           dependencias=_opciones('Dependencia'), **_catalogos_sesion())


@ponente_catedras_bp.route('/nueva', methods=['GET', 'POST'])
def nueva():
    if request.method == 'GET':
        return _render_nueva(None, [])

    datos = {
        'id_evento_asis': request.form.get('id_evento_asis', '').strip(),
        'nombre': request.form.get('nombre', '').strip(),
        'fk_tipo_evento': request.form.get('fk_tipo_evento', ''),
        'fk_dependencia': request.form.get('fk_dependencia', '').strip() or None,
        'activo': True,
    }
    filas = _filas('ses', CAMPOS_SESION_FILA, clave='inicio')
    if not filas:
        # Sin sesiones no habría a qué asignarse: la participación es por sesión.
        flash('Agrega al menos una sesión: es a ellas a las que te asignas como ponente.', 'error')
        return _render_nueva(datos, [], filas)
    datos['sesiones'] = [_sesion_de_fila(f) for f in filas]

    try:
        respuesta = crear('Catedra', datos)    # cátedra + sesiones en un solo CALL: todo o nada
    except ErrorApi as ex:
        _avisar(ex)
        return _render_nueva(datos, ex.errores, filas)

    id_catedra = respuesta['id_catedra']
    try:
        sesiones = listar('Sesion', limite=500, fkCatedra=id_catedra)
    except ErrorApi as ex:
        sesiones = []
        _avisar(ex)
    hechas, ex = _asignar_a_sesiones(sesiones)
    if ex is not None or hechas != len(filas):
        # La cátedra ya existe: no se borra. Se dice qué faltó y cómo terminar.
        if ex is not None:
            _avisar(ex)
        flash(f'La cátedra se creó, pero solo quedaste asignado en {hechas} de {len(filas)} sesiones. '
              'Termina desde aquí con «Asignarme».', 'error')
    else:
        flash(f"Cátedra «{datos['nombre']}» creada con {hechas} sesion{'' if hechas == 1 else 'es'}; "
              'quedaste como ponente en todas. Otro ponente puede asignarse a ella desde «Cátedras».', 'exito')
    return redirect(url_for('ponente_catedras.detalle', id_catedra=id_catedra))


@ponente_catedras_bp.route('/')
def lista():
    texto = request.args.get('q', '').strip()
    try:
        pagina = max(1, int(request.args.get('pagina', 1)))
    except ValueError:
        pagina = 1
    try:
        todas = listar('Catedra', timeout=30)           # solo las activas (por defecto)
        mias = _catedras_donde_participo()
    except ErrorApi as ex:
        _avisar(ex)
        todas, mias = [], set()
    if texto:
        buscado = texto.lower()
        todas = [c for c in todas if buscado in c['nombre'].lower() or buscado in c['id_evento_asis']]
    total = len(todas)
    total_paginas = max(1, -(-total // CATEDRAS_POR_PAGINA))
    pagina = min(pagina, total_paginas)
    corte = (pagina - 1) * CATEDRAS_POR_PAGINA
    return render_template('ponente_catedras.html', catedras=todas[corte:corte + CATEDRAS_POR_PAGINA],
                           mias=mias, q=texto, pagina=pagina, total_paginas=total_paginas, total=total)


def _catedra_y_sesiones(id_catedra):
    """(cátedra, sesiones por fecha) o 404 si no existe o está inactiva."""
    try:
        catedra = obtener('Catedra', id_catedra)
        sesiones = listar('Sesion', limite=500, fkCatedra=id_catedra)
    except ErrorApi as ex:
        if ex.codigo == 404:
            abort(404)
        raise
    if not catedra.get('activo', True):
        abort(404)
    return catedra, sorted(sesiones, key=lambda s: s['inicio'])


@ponente_catedras_bp.route('/<int:id_catedra>')
def detalle(id_catedra):
    try:
        catedra, sesiones = _catedra_y_sesiones(id_catedra)
        mias = _mis_sesiones()
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('ponente_catedra_detalle.html', catedra=None, sesiones=[], mias=set()), 502
    try:
        inscritos = len(listar('InscripcionCatedra', limite=5000, fkCatedra=id_catedra))
    except ErrorApi:
        inscritos = None
    return render_template('ponente_catedra_detalle.html', catedra=catedra, sesiones=sesiones, mias=mias,
                           inscritos=inscritos)


def _sesiones_elegidas(sesiones):
    """Las sesiones de ESTA cátedra que vienen marcadas. Un id ajeno se ignora."""
    pedidas = set()
    for texto in request.form.getlist('sesiones'):
        try:
            pedidas.add(int(texto))
        except ValueError:
            pass
    return [s for s in sesiones if s['id_sesion'] in pedidas]


@ponente_catedras_bp.route('/<int:id_catedra>/asignarme', methods=['POST'])
def asignarme(id_catedra):
    catedra, sesiones = _catedra_y_sesiones(id_catedra)
    ya = _mis_sesiones()
    nuevas = [s for s in _sesiones_elegidas(sesiones) if s['id_sesion'] not in ya]
    if not nuevas:
        flash('No marcaste ninguna sesión nueva (las que ya tienes no se repiten).', 'error')
    else:
        hechas, ex = _asignar_a_sesiones(nuevas)
        if ex is not None:
            _avisar(ex)
        if hechas:
            flash(f"Quedaste como ponente en {hechas} sesion{'' if hechas == 1 else 'es'} de «{catedra['nombre']}».", 'exito')
    return redirect(url_for('ponente_catedras.detalle', id_catedra=id_catedra))


@ponente_catedras_bp.route('/<int:id_catedra>/quitarme', methods=['POST'])
def quitarme(id_catedra):
    catedra, sesiones = _catedra_y_sesiones(id_catedra)
    ya = _mis_sesiones()
    quitar = [s for s in _sesiones_elegidas(sesiones) if s['id_sesion'] in ya]
    hechas = 0
    for s in quitar:
        try:
            # Solo MI llave (sesión + mi asistente + PONENTE): nunca la de otra persona.
            eliminar('Ponencia', llave(s['id_sesion'], _mi_asistente(), 'PONENTE'))
            hechas += 1
        except ErrorApi as ex:
            _avisar(ex)
            break
    if hechas:
        flash(f"Dejaste de ser ponente en {hechas} sesion{'' if hechas == 1 else 'es'}.", 'exito')
    elif not quitar:
        flash('Marca las sesiones de las que quieres salir (solo las tuyas).', 'error')
    return redirect(url_for('ponente_catedras.detalle', id_catedra=id_catedra))
