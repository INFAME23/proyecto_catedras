# ==========================================================================
#  routes/encuestas.py  ·  Front Cátedras · Versión 6
# --------------------------------------------------------------------------
#  Responder la encuesta de calidad de una sesión (criterio 7 de
#  version6/2_spec.md RF159-RF178, §9 de version6/6_contracts.md).
#
#  "Responder" es UN solo verbo de negocio (ADR D31): POST /api/RespuestaEncuesta
#  guarda el maestro y TODOS los detalles, y cierra la encuesta, en la
#  misma transacción que el 'CALL' a 'fn_responder_encuesta_api'. No hay
#  "guardar borrador": o se manda todo, o no queda nada guardado (RF174).
#
#  Quién puede responder: el asistente de la sesión, identificado por la
#  sesión de Flask (misma mecánica que el calendario del ponente desde
#  v5+, ver routes/ponente.py). 'usuario_actual()["fk_asistente"]' debe
#  tener un 'RegistroAsistencia' en esta sesión, y ese registro no debe
#  tener ya una 'RespuestaEncuesta' (uq_respuesta_registro, D32: se
#  responde una sola vez, para siempre).
#
#  La API no filtra RespuestaEncuesta por 'fkRegistro' (solo por
#  'fkEncuesta', ver 6_contracts.md §3), así que "¿ya respondió?" se
#  revisa aquí, recorriendo las respuestas de la encuesta. La encuesta de
#  ejemplo trae pocas (62), así que es aceptable, igual criterio que
#  'ponentes_de' en routes/sesiones.py.
# ==========================================================================

from flask import session, Blueprint, abort, flash, redirect, render_template, request, url_for

from servicios.api_catedras import ErrorApi, crear, listar, obtener

encuestas_bp = Blueprint('encuestas', __name__)


def _mi_registro(id_sesion):
    """El 'RegistroAsistencia' que ESTA persona acaba de crear al registrar su asistencia a la sesión, o None.
    v7.1 · D55: ya no hay cuentas de estudiante; routes/registro.py deja en la cookie firmada
    ('registro_reciente') el registro que se acaba de crear y solo ese permite responder la encuesta."""
    reciente = session.get('registro_reciente')
    if not reciente or reciente.get('sesion') != id_sesion:
        return None
    try:
        return obtener('RegistroAsistencia', reciente['registro'])
    except ErrorApi:
        return None


def _ya_respondida(fk_encuesta, id_registro):
    """¿Ese registro ya respondió esta encuesta? (uq_respuesta_registro:
    a lo sumo una fila por registro)."""
    try:
        respuestas = listar('RespuestaEncuesta', fkEncuesta=fk_encuesta, limite=500)
    except ErrorApi:
        return False
    return any(r['fk_registro'] == id_registro for r in respuestas)


def estado_encuesta_sesion(sesion):
    """Para 'detalle_sesion.html' (criterio 7): ¿se ofrece el enlace
    "responder encuesta"? Devuelve (mostrar, motivo):
      - (True, None)             -> se puede responder, se muestra el enlace
      - (False, 'sin_encuesta')  -> la sesión no tiene encuesta asignada
      - (False, 'sin_sesion')    -> no hay sesión de Flask (se invita a iniciar sesión)
      - (False, 'sin_registro')  -> el usuario no tiene registro de asistencia aquí
      - (False, 'ya_respondida') -> su registro ya respondió
    """
    if not sesion.get('fk_encuesta'):
        return False, 'sin_encuesta'
    registro = _mi_registro(sesion['id_sesion'])
    if registro is None:
        return False, 'sin_registro'
    if _ya_respondida(sesion['fk_encuesta'], registro['id_registro']):
        return False, 'ya_respondida'
    return True, None


def _tipos_pregunta():
    """{id_tipo_pregunta: fila}: decide, por pregunta, qué widget dibujar
    (RN-25 es DATO, no código — 'guarda_numero'/'guarda_texto'/'guarda_opcion')."""
    try:
        return {t['id_tipo_pregunta']: t for t in listar('TipoPregunta')}
    except ErrorApi:
        return {}


def _preguntas_de(fk_encuesta):
    """Las preguntas de la encuesta, en orden, cada una con su tipo y (si
    es OPCION_UNICA) sus opciones ya cargadas."""
    try:
        preguntas = listar('Pregunta', fkEncuesta=fk_encuesta, limite=100)
    except ErrorApi:
        return []
    tipos = _tipos_pregunta()
    preguntas.sort(key=lambda p: p['orden'])
    for pregunta in preguntas:
        pregunta['tipo'] = tipos.get(pregunta['fk_tipo_pregunta'])
        pregunta['opciones'] = []
        if pregunta['tipo'] and pregunta['tipo']['guarda_opcion']:
            try:
                pregunta['opciones'] = sorted(
                    listar('OpcionPregunta', fkPregunta=pregunta['id_pregunta']),
                    key=lambda o: o['orden'])
            except ErrorApi:
                pass
    return preguntas


@encuestas_bp.route('/sesiones/<int:id_sesion>/encuesta', methods=['GET', 'POST'])
def responder(id_sesion):
    try:
        sesion = obtener('Sesion', id_sesion)
    except ErrorApi as ex:
        if ex.codigo == 404:
            abort(404)
        flash(f"{ex.mensaje} {ex.detalle or ''}".strip(), 'error')
        return redirect(url_for('sesiones.detalle', id_sesion=id_sesion))

    puede, motivo = estado_encuesta_sesion(sesion)
    if not puede:
        mensajes = {
            'sin_encuesta': 'Esta sesión no tiene una encuesta asignada.',
            'sin_registro': 'La encuesta se responde justo después de registrar tu asistencia con el QR de la sesión.',
            'ya_respondida': 'Ya respondiste la encuesta de esta sesión. ¡Gracias!',
        }
        categoria = 'exito' if motivo == 'ya_respondida' else 'error'
        flash(mensajes.get(motivo, 'No es posible responder esta encuesta.'), categoria)
        return redirect(url_for('sesiones.detalle', id_sesion=id_sesion))

    registro = _mi_registro(id_sesion)
    preguntas = _preguntas_de(sesion['fk_encuesta'])

    if request.method == 'GET':
        return render_template('encuesta_form.html', sesion=sesion, preguntas=preguntas)

    # POST: una respuesta por pregunta CONTESTADA. 'respuestas' no puede
    # venir vacía (RF174), pero una pregunta NO obligatoria se puede
    # omitir sin problema: la BD solo exige las marcadas 'obligatoria'.
    respuestas = []
    for pregunta in preguntas:
        campo = f"pregunta_{pregunta['id_pregunta']}"
        valor = request.form.get(campo, '').strip()
        if not valor:
            continue
        item = {'pregunta': pregunta['id_pregunta']}
        tipo = pregunta['tipo'] or {}
        if tipo.get('guarda_opcion'):
            item['opcion'] = int(valor)
        elif tipo.get('guarda_texto'):
            item['texto'] = valor
        else:   # guarda_numero (ESCALA_5, SI_NO)
            try:
                item['numero'] = int(valor)
            except ValueError:
                flash(f"«{pregunta['enunciado']}» necesita un número.", 'error')
                return render_template('encuesta_form.html', sesion=sesion, preguntas=preguntas)
        respuestas.append(item)

    if not respuestas:
        flash('Responde al menos una pregunta.', 'error')
        return render_template('encuesta_form.html', sesion=sesion, preguntas=preguntas)

    try:
        crear('RespuestaEncuesta', {'fkRegistro': registro['id_registro'], 'respuestas': respuestas})
    except ErrorApi as ex:
        # 500 típico: falta una pregunta OBLIGATORIA (RN-26, dentro de
        # sp_cerrar_encuesta) -> no quedó NADA guardado, ni el maestro ni
        # ningún detalle (todo o nada, D31). Se muestra tal cual la API
        # lo explica (ex.detalle) y se deja el formulario para reintentar.
        flash(f"{ex.mensaje} {(ex.detalle or '').strip()}".strip(), 'error')
        return render_template('encuesta_form.html', sesion=sesion, preguntas=preguntas)

    flash('¡Gracias! Tu encuesta quedó registrada.', 'exito')
    return redirect(url_for('sesiones.detalle', id_sesion=id_sesion))
