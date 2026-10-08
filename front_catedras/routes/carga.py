# ==========================================================================
#  routes/carga.py  ·  Front Cátedras · Versión 3
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
#  Si la API deja de responder a mitad de la carga, se detiene y se avisa:
#  el lote queda ABIERTO (sin contadores) y nada se inventa (RNF8).
# ==========================================================================

from flask import Blueprint, render_template, request

from servicios.api_catedras import ErrorApi, crear, listar
from servicios.carga_asistentes import (ErrorArchivo, construir_asistente, contenido_crudo,
                                        indice_de_programas, leer_filas, motivo_de)

carga_bp = Blueprint('carga', __name__)


@carga_bp.route('/carga_datos', methods=['GET', 'POST'])
def carga_datos():
    if request.method == 'GET':
        return render_template('carga_datos.html', resultado=None, error=None)

    archivo = request.files.get('archivo')
    if archivo is None or not archivo.filename:
        return render_template('carga_datos.html', resultado=None, error='Selecciona un archivo .xlsx o .csv.')

    # ---- 1) Leer el archivo completo (antes de tocar la API) -------------
    try:
        filas = leer_filas(archivo)
    except ErrorArchivo as ex:
        return render_template('carga_datos.html', resultado=None, error=str(ex))

    # Catálogo de programas y alias, UNA vez, para traducir el programa
    # escrito en texto libre a su código (ADR D17).
    try:
        indice = indice_de_programas(listar('ProgramaAcademico', limite=5000),
                                     listar('AliasPrograma', limite=5000))
    except ErrorApi as ex:
        return render_template('carga_datos.html', resultado=None,
                               error=f'{ex.mensaje} {ex.detalle or ""}'.strip())

    # ---- 2) Abrir el lote --------------------------------------------------
    try:
        lote = crear('LoteCargaAsistente', {'nombre_archivo': archivo.filename[:200]})
    except ErrorApi as ex:
        return render_template('carga_datos.html', resultado=None,
                               error=f'No se pudo abrir el lote de carga: {ex.mensaje} {ex.detalle or ""}'.strip())
    id_lote = lote['id_lote_carga']

    # ---- 3) Una petición por fila ------------------------------------------
    aceptadas, novedades, interrumpida, fila_interrumpida = 0, [], None, None
    for numero_fila, original, campos in filas:
        datos = construir_asistente(campos, id_lote, indice)
        try:
            crear('Asistente', datos)
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
                 'total_archivo': len(filas), 'cerrado': False}

    if interrumpida:
        return render_template('carga_datos.html', resultado=resultado,
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
            'observaciones': f'Carga desde la pantalla de carga de datos: {len(filas)} filas en el archivo.',
            'novedades': novedades,
        })
        resultado['cerrado'] = True
    except ErrorApi as ex:
        return render_template('carga_datos.html', resultado=resultado,
                               error=f'Las filas se procesaron, pero no se pudo cerrar el lote {id_lote}: '
                                     f'{ex.mensaje} {ex.detalle or ""}'.strip())

    return render_template('carga_datos.html', resultado=resultado, error=None)
