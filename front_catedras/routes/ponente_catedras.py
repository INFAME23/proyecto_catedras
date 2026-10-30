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

from datetime import datetime, timedelta

from flask import Blueprint, abort, flash, jsonify, redirect, render_template, request, url_for

from auth_helpers import ROL_ADMIN, ROL_PONENTE, exigir_rol, tiene_rol, usuario_actual
from routes.admin import (CAMPOS_SESION_FILA, _catalogos_sesion, _filas, _opciones,
                          _sesion_de_fila)
from routes.ponente import _avisar, _calendario_de
from servicios.api_catedras import ErrorApi, consultar, crear, eliminar, listar, llave, obtener
from servicios.fechas import ZONA_COLOMBIA, a_local, desde_input, fecha_hora
from servicios.qr_sesiones import (asegurar_qr_de_catedra, emitir_qr, estado_de_ventana, qr_de_sesion,
                                   svg_qr, url_de_registro)

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


# --------------------------------------------------------------------------
#  v7.1 · D52 · DISPONIBILIDAD DE HORARIO: que no se crucen dos sesiones.
#  Un horario NO está disponible si otra sesión se traslapa con él y
#    · LUGAR: es en la misma sede física y el mismo salón (sin salón escrito
#      se cuenta como choque, es lo prudente), o
#    · PONENTE: ya participas tú en esa otra sesión (nadie está en dos a la vez).
#  La regla la calcula la BD (sp_sesiones_en_conflicto, migración 050); aquí solo
#  se consulta y se avisa. Si NO se puede consultar, no se crea nada (se falla
#  cerrado: mejor pedir reintentar que cruzar dos sesiones).
# --------------------------------------------------------------------------
_MOTIVO = {'LUGAR': 'el mismo salón en esa sede', 'PONENTE': 'ya participas tú en esa sesión'}


def _describe(c):
    """Una sesión que choca, en una línea legible (hora de Colombia)."""
    motivos = ' y '.join(_MOTIVO.get(m, m) for m in (c.get('motivos') or []))
    donde = ', '.join(x for x in (c.get('fk_sede'), c.get('lugar')) if x)
    return (f"«{c['nombre_catedra']}» (reunión {c['numero_reunion']}, "
            f"{fecha_hora(c['inicio'], '%Y-%m-%d %H:%M')}–{fecha_hora(c['fin'], '%H:%M')}"
            f"{', ' + donde if donde else ''}): {motivos}")


def _conflictos(inicio, fin, sede=None, lugar=None, excluir_sesion=None):
    """Sesiones que chocan con ese horario (para MÍ como ponente y, si hay sede, por salón).
    'inicio'/'fin': texto ISO con zona. Lanza ErrorApi si no se pudo consultar."""
    r = consultar('Disponibilidad/conflictos', inicio=inicio, fin=fin, fkSede=sede or None,
                  lugar=lugar or None, fkAsistente=_mi_asistente(), excluirSesion=excluir_sesion)
    return r['conflictos']


def _choques_del_formulario(filas):
    """Lista de textos con los choques de las sesiones escritas: contra la BD y entre ellas
    mismas (quien las crea queda en todas, así que dos que se traslapan se cruzan siempre)."""
    avisos = []
    rangos = []
    for n, f in enumerate(filas, start=1):
        inicio, fin = desde_input(f['inicio']), desde_input(f['fin'])
        for c in _conflictos(inicio, fin, f.get('fk_sede'), f.get('lugar')):
            avisos.append(f"Sesión {n} choca con {_describe(c)}.")
        try:
            rangos.append((n, datetime.fromisoformat(inicio), datetime.fromisoformat(fin)))
        except (TypeError, ValueError):
            pass        # fecha ilegible: la API la rechaza con su 422 al crear
    for i, (n1, a1, b1) in enumerate(rangos):
        for n2, a2, b2 in rangos[i + 1:]:
            if a1 < b2 and b1 > a2:
                avisos.append(f"Las sesiones {n1} y {n2} de esta misma cátedra se cruzan en el horario.")
    return avisos


def _render_nueva(datos, errores, filas=(), choques=()):
    return render_template('ponente_catedra_nueva.html', catedra=datos, errores=errores, choques=list(choques),
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
    # v7.1 · D52: antes de guardar nada, que ninguna sesión se cruce con otra.
    try:
        choques = _choques_del_formulario(filas)
    except ErrorApi as ex:
        flash('No se pudo verificar la disponibilidad del horario, así que no se creó nada. '
              f"Intenta de nuevo en un momento. {ex.detalle or ''}".strip(), 'error')
        return _render_nueva(datos, [], filas)
    if choques:
        # Fijos en la página (no flash: un flash se borra a los 5 s y esto hay que poder leerlo).
        return _render_nueva(datos, [], filas, choques)
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
    # v7.1 · D53: cada sesión nace con su QR de cierre (se abre al final de la sesión).
    qr_hechos, qr_total = asegurar_qr_de_catedra(id_catedra, usuario_actual().get('id_usuario'))
    if ex is not None or hechas != len(filas):
        # La cátedra ya existe: no se borra. Se dice qué faltó y cómo terminar.
        if ex is not None:
            _avisar(ex)
        flash(f'La cátedra se creó, pero solo quedaste asignado en {hechas} de {len(filas)} sesiones. '
              'Termina desde aquí con «Asignarme».', 'error')
    else:
        flash(f"Cátedra «{datos['nombre']}» creada con {hechas} sesion{'' if hechas == 1 else 'es'}; "
              'quedaste como ponente en todas. Otro ponente puede asignarse a ella desde «Cátedras».', 'exito')
    if qr_hechos != qr_total:
        flash(f'Se generó el QR de {qr_hechos} de {qr_total} sesiones. Las demás se generan desde «Ver QR».', 'error')
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


def _pagina_detalle(id_catedra, avisos=()):
    """La pantalla de una cátedra; 'avisos' son textos FIJOS (no flash) que se leen con calma."""
    try:
        catedra, sesiones = _catedra_y_sesiones(id_catedra)
        mias = _mis_sesiones()
    except ErrorApi as ex:
        _avisar(ex)
        return render_template('ponente_catedra_detalle.html', catedra=None, sesiones=[], mias=set(),
                               avisos=list(avisos)), 502
    try:
        inscritos = len(listar('InscripcionCatedra', limite=5000, fkCatedra=id_catedra))
    except ErrorApi:
        inscritos = None
    return render_template('ponente_catedra_detalle.html', catedra=catedra, sesiones=sesiones, mias=mias,
                           inscritos=inscritos, avisos=list(avisos))


@ponente_catedras_bp.route('/<int:id_catedra>')
def detalle(id_catedra):
    return _pagina_detalle(id_catedra)


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
        # v7.1 · D52: no te asignas a una sesión que se cruza con otra tuya.
        libres = []
        avisos = []
        for s in nuevas:
            try:
                choques = _conflictos(s['inicio'], s['fin'], excluir_sesion=s['id_sesion'])
            except ErrorApi as ex:
                _avisar(ex)
                return redirect(url_for('ponente_catedras.detalle', id_catedra=id_catedra))
            if choques:
                avisos.append(f"No te asignaste a la reunión {s['numero_reunion']} "
                              f"({fecha_hora(s['inicio'])}): se cruza con {_describe(choques[0])}.")
            else:
                libres.append(s)
        hechas, ex = _asignar_a_sesiones(libres)
        if ex is not None:
            _avisar(ex)
        if hechas:
            flash(f"Quedaste como ponente en {hechas} sesion{'' if hechas == 1 else 'es'} de «{catedra['nombre']}».", 'exito')
        if avisos:
            return _pagina_detalle(id_catedra, avisos)      # sin redirigir: los avisos quedan visibles
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


# --------------------------------------------------------------------------
#  v7.1 · D52 · JSON para la pantalla «Nueva cátedra» (lo llama static/ponente_disponibilidad.js).
#  Mismo guardia de rol que el resto del blueprint. Las horas llegan y salen en hora de Colombia.
# --------------------------------------------------------------------------
def _hora(c):
    return {**c, 'hora': f"{fecha_hora(c['inicio'], '%H:%M')}–{fecha_hora(c['fin'], '%H:%M')}",
            'fecha': fecha_hora(c['inicio'], '%Y-%m-%d'),
            'texto': _describe(c) if c.get('motivos') else None}


@ponente_catedras_bp.route('/disponibilidad')
def disponibilidad():
    """¿Está libre este horario? ?inicio=&fin= (datetime-local) &sede= &lugar= ."""
    inicio, fin = desde_input(request.args.get('inicio')), desde_input(request.args.get('fin'))
    if not inicio or not fin:
        return jsonify(error='Escribe el inicio y el fin para consultar.'), 400
    try:
        conflictos = _conflictos(inicio, fin, request.args.get('sede'), request.args.get('lugar'))
    except ErrorApi as ex:
        return jsonify(error=f"{ex.mensaje} {ex.detalle or ''}".strip()), (400 if ex.codigo in (400, 422) else 502)
    return jsonify(disponible=not conflictos, conflictos=[_hora(c) for c in conflictos])


@ponente_catedras_bp.route('/agenda')
def agenda():
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
    return jsonify(fecha=dia.strftime('%Y-%m-%d'), sesiones=[_hora(c) for c in r['datos']])


# --------------------------------------------------------------------------
#  v7.1 · D53 · QR DE CIERRE DE UNA SESIÓN
#  El ponente de la sesión (o un administrador) ve el QR, lo proyecta o comparte al FINAL, y
#  ve quién de los inscritos asistió (con hora y ubicación). El token del QR no se muestra a nadie más.
# --------------------------------------------------------------------------
ROLES_QUE_GESTIONAN = ('PONENTE', 'ORGANIZADOR')


def _sesion_gestionable(id_sesion):
    """(sesión, cátedra) si soy ADMIN o PONENTE/ORGANIZADOR de esa sesión; 404 si no existe, 403 si no."""
    try:
        sesion = obtener('Sesion', id_sesion)
        catedra = obtener('Catedra', sesion['fk_catedra'])
    except ErrorApi as ex:
        if ex.codigo == 404:
            abort(404)
        raise
    if not tiene_rol(ROL_ADMIN):
        mias = [p for p in listar('Ponencia', fkSesion=id_sesion) if p['fk_asistente'] == _mi_asistente()]
        if not any(p['rol'] in ROLES_QUE_GESTIONAN for p in mias):
            abort(403)
    return sesion, catedra


def _resumen_asistencia(id_sesion):
    """(filas, contadores) de la asistencia de la sesión."""
    r = consultar('QrSesion/asistencia', fkSesion=id_sesion)
    filas = r['datos'] if r else []
    inscritos = [f for f in filas if f['inscrito']]
    contadores = {'inscritos': len(inscritos),
                  'asistieron': sum(1 for f in inscritos if f['asistio']),
                  'sin_inscripcion': sum(1 for f in filas if f['asistio'] and not f['inscrito'])}
    contadores['faltan'] = contadores['inscritos'] - contadores['asistieron']
    return filas, contadores


@ponente_catedras_bp.route('/sesion/<int:id_sesion>/qr', methods=['GET', 'POST'])
def qr_sesion(id_sesion):
    sesion, catedra = _sesion_gestionable(id_sesion)

    if request.method == 'POST':
        regenerar = request.form.get('regenerar') == '1'
        try:
            qr = emitir_qr(id_sesion, usuario_actual().get('id_usuario'), regenerar)
            flash('Se generó un QR nuevo (el anterior ya no sirve).' if regenerar and not qr['ya_existia']
                  else 'El QR de la sesión está listo.', 'exito')
        except ErrorApi as ex:
            _avisar(ex)
        return redirect(url_for('ponente_catedras.qr_sesion', id_sesion=id_sesion))

    error = None
    qr = None
    try:
        qr = qr_de_sesion(id_sesion)
    except ErrorApi as ex:
        error = ex
    filas, contadores = [], {'inscritos': 0, 'asistieron': 0, 'sin_inscripcion': 0, 'faltan': 0}
    try:
        filas, contadores = _resumen_asistencia(id_sesion)
    except ErrorApi as ex:
        error = error or ex

    url = svg = None
    if qr:
        url = url_de_registro(qr['token'])
        svg = svg_qr(url)
    return render_template('ponente_sesion_qr.html', sesion=sesion, catedra=catedra, qr=qr, estado=estado_de_ventana(qr),
                           url=url, svg=svg, filas=filas, contadores=contadores, error=error,
                           desde=a_local(qr['ventana_desde']) if qr else None,
                           hasta=a_local(qr['ventana_hasta']) if qr else None)


@ponente_catedras_bp.route('/sesion/<int:id_sesion>/asistencia')
def asistencia_json(id_sesion):
    """JSON para refrescar la lista sin recargar la página (lo llama la pantalla del QR)."""
    _sesion_gestionable(id_sesion)
    try:
        filas, contadores = _resumen_asistencia(id_sesion)
        estado = estado_de_ventana(qr_de_sesion(id_sesion))
    except ErrorApi as ex:
        return jsonify(error=f"{ex.mensaje} {ex.detalle or ''}".strip()), 502
    for f in filas:
        f['hora'] = fecha_hora(f['registrado_en'], '%H:%M:%S') if f.get('registrado_en') else None
    return jsonify(filas=filas, contadores=contadores, estado=estado)
