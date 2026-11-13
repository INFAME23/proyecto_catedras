# ==========================================================================
#  routes/carga.py  ·  Front Cátedras · Versión 3 (y v7.2 · RF-S8)
# --------------------------------------------------------------------------
#  Pantalla de CARGA DE DATOS: sube el archivo de personas y lo carga
#  fila por fila contra la API (ADR D15 y D17, version3/3_plan.md §5).
#
#  El flujo, en 4 pasos:
#    1. Leer el archivo COMPLETO en Python (servicios/carga_asistentes.py).
#       Si el archivo está dañado, se avisa ANTES de abrir ningún lote.
#    2. Abrir un lote:           POST /api/LoteCargaAsistente
#    3. Por cada fila:           POST /api/Asistente (con fk_lote_carga)
#       Cada fila es su PROPIA transacción: si una falla, las anteriores
#       ya quedaron guardadas (carga PARCIAL, D15). El motivo del rechazo
#       es el mensaje que devolvió la API.
#    4. Cerrar el lote:          POST /api/LoteCargaAsistente/{id}/cerrar
#       con los 3 contadores y una novedad por fila rechazada (todo o nada).
#
#  v7.2 · RF-S8: hay DOS formatos de archivo. Si el encabezado es exactamente
#  CC, Carnet_estudiante, Programa (el archivo de estudiantes), cada fila va a
#  POST /api/Asistente/cargar-estudiante, que es IDEMPOTENTE (volver a subir el
#  mismo carnet actualiza, no duplica) y usa el PERIODO que el administrador
#  elige en un <select> (Regla 2: el archivo no lo trae). Cualquier otro archivo
#  sigue el camino de siempre.
#
#  Si la API deja de responder a mitad de la carga, se detiene y se avisa:
#  el lote queda ABIERTO (sin contadores) y nada se inventa (RNF8).
# ==========================================================================

from datetime import date

from flask import Blueprint, render_template, request

from auth_helpers import ROL_ADMIN, rol_requerido
from servicios.api_catedras import ErrorApi, crear, listar
from servicios.carga_asistentes import (ErrorArchivo, construir_asistente, construir_estudiante, contenido_crudo,
                                        es_archivo_de_estudiantes, indice_de_programas, leer_archivo, motivo_de)

carga_bp = Blueprint('carga', __name__)


def _periodos():
    """Los periodos académicos para el <select> (Regla 2) y el que se propone por defecto:
    el que cubre la fecha de hoy; si ninguno, el último activo. Si la API no responde, ([], None)
    y la pantalla avisa (sin periodo no se puede cargar el archivo de estudiantes)."""
    try:
        periodos = [p for p in listar('PeriodoAcademico', limite=500) if p.get('activo', True)]
    except ErrorApi:
        return [], None

    periodos.sort(key=lambda p: p['id_periodo'])
    hoy = date.today().isoformat()
    por_defecto = next((p['id_periodo'] for p in periodos if p['fecha_ini'] <= hoy <= p['fecha_fin']), None)
    if por_defecto is None and periodos:
        por_defecto = periodos[-1]['id_periodo']
    return periodos, por_defecto


def _pantalla(resultado=None, error=None, periodo=None):
    periodos, por_defecto = _periodos()
    return render_template('carga_datos.html', resultado=resultado, error=error, periodos=periodos,
                           periodo=periodo or por_defecto)


# v7.1: cargar personas masivamente es de ADMIN (antes era público).
@carga_bp.route('/carga_datos', methods=['GET', 'POST'])
@rol_requerido(ROL_ADMIN)
def carga_datos():
    if request.method == 'GET':
        return _pantalla()

    archivo = request.files.get('archivo')
    periodo = request.form.get('periodo', '').strip()
    if archivo is None or not archivo.filename:
        return _pantalla(error='Selecciona un archivo .xlsx o .csv.', periodo=periodo)

    # ---- 1) Leer el archivo completo (antes de tocar la API) -------------
    try:
        columnas, filas = leer_archivo(archivo)
    except ErrorArchivo as ex:
        return _pantalla(error=str(ex), periodo=periodo)

    estudiantes = es_archivo_de_estudiantes(columnas)

    # El archivo de estudiantes no trae periodo: tiene que venir del <select>, y ser uno de los que existen.
    if estudiantes:
        periodos, _ = _periodos()
        if not periodo or periodo not in {p['id_periodo'] for p in periodos}:
            return _pantalla(error='El archivo de estudiantes no trae el periodo: elige uno de la lista antes de cargar.',
                             periodo=periodo)

    # Catálogo de programas y alias, UNA vez, para traducir el programa
    # escrito en texto libre a su código (ADR D17).
    try:
        indice = indice_de_programas(listar('ProgramaAcademico', limite=5000, incluirInactivos='true'),
                                     listar('AliasPrograma', limite=5000))
    except ErrorApi as ex:
        return _pantalla(error=f'{ex.mensaje} {ex.detalle or ""}'.strip(), periodo=periodo)

    # ---- 2) Abrir el lote --------------------------------------------------
    try:
        lote = crear('LoteCargaAsistente', {'nombre_archivo': archivo.filename[:200]})
    except ErrorApi as ex:
        return _pantalla(error=f'No se pudo abrir el lote de carga: {ex.mensaje} {ex.detalle or ""}'.strip(),
                         periodo=periodo)
    id_lote = lote['id_lote_carga']

    # ---- 3) Una petición por fila ------------------------------------------
    aceptadas, creadas, actualizadas, novedades, interrumpida, fila_interrumpida = 0, 0, 0, [], None, None
    for numero_fila, original, campos in filas:
        try:
            if estudiantes:
                respuesta = crear('Asistente/cargar-estudiante', construir_estudiante(campos, periodo, id_lote, indice))
                if respuesta.get('accion') == 'CREADO':
                    creadas += 1
                else:
                    actualizadas += 1
            else:
                crear('Asistente', construir_asistente(campos, id_lote, indice))
            aceptadas += 1
        except ErrorApi as ex:
            if ex.codigo is None:
                # Sin código HTTP = la API no respondió (caída, red). No es un
                # rechazo de ESTA fila: se detiene la carga y se avisa.
                interrumpida = f'{ex.mensaje} {ex.detalle or ""}'.strip()
                fila_interrumpida = numero_fila
                break
            novedades.append({'numero_fila': numero_fila,
                              'contenido_crudo': contenido_crudo(original),
                              'motivo': motivo_de(ex)})

    leidas = aceptadas + len(novedades)
    resultado = {'id_lote_carga': id_lote, 'nombre_archivo': archivo.filename,
                 'filas_leidas': leidas, 'filas_aceptadas': aceptadas,
                 'filas_rechazadas': len(novedades), 'novedades': novedades,
                 'total_archivo': len(filas), 'cerrado': False,
                 # v7.2: solo en el archivo de estudiantes
                 'modo_estudiantes': estudiantes, 'periodo': periodo if estudiantes else None,
                 'creadas': creadas, 'actualizadas': actualizadas}

    if interrumpida:
        return _pantalla(resultado=resultado, periodo=periodo,
                         error=f'La carga se detuvo en la fila {fila_interrumpida} del archivo ({leidas} de '
                               f'{len(filas)} personas procesadas) porque la API dejó de responder. El lote '
                               f'{id_lote} quedó ABIERTO; las {aceptadas} filas aceptadas sí quedaron '
                               f'guardadas. Detalle: {interrumpida}')

    # ---- 4) Cerrar el lote (contadores + novedades, una transacción) ------
    try:
        crear(f'LoteCargaAsistente/{id_lote}/cerrar', {
            'filas_leidas': leidas,
            'filas_aceptadas': aceptadas,
            'filas_rechazadas': len(novedades),
            'observaciones': f'Carga desde la pantalla de carga de datos: {len(filas)} filas en el archivo'
                             + (f' de estudiantes, periodo {periodo} ({creadas} creados, {actualizadas} actualizados).'
                                if estudiantes else '.'),
            'novedades': novedades,
        })
        resultado['cerrado'] = True
    except ErrorApi as ex:
        return _pantalla(resultado=resultado, periodo=periodo,
                         error=f'Las filas se procesaron, pero no se pudo cerrar el lote {id_lote}: '
                               f'{ex.mensaje} {ex.detalle or ""}'.strip())

    return _pantalla(resultado=resultado, periodo=periodo)
