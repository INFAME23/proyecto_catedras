# ==========================================================================
#  servicios/exportacion.py  ·  Front Cátedras · Subversión v7.2 · RF-S13 / RF-S14
# --------------------------------------------------------------------------
#  Arma los CSV y el ZIP de la exportación de fin de periodo. La API entrega los
#  datos ya clasificados y sin repetir personas (GET /api/Exportacion/...); aquí
#  solo se les da FORMATO, con los módulos 'csv' y 'zipfile' de Python (sin
#  paquetes nuevos).
#
#  QUÉ SE EXPORTA (D70): un archivo por cátedra y tipo de asistente. Un estudiante
#  sale en un archivo por cada cátedra a la que fue; contar en cuántos archivos
#  aparece su carnet da el total. El SISTEMA NO calcula ese total.
#
#    catedra_<id>_estudiantes.csv  ->  CC, Carnet_estudiante, Programa   (idénticas al archivo de carga)
#    catedra_<id>_docentes.csv     ->  CC, Nombre, Contacto, Facultad
#    catedra_<id>_externos.csv     ->  CC, Nombre, Contacto, Procedencia
#    catedras_<periodo>.csv        ->  id_catedra, titulo                (catálogo del periodo)
#
#  FORMATO (D71): UTF-8 CON BOM (Excel lo abre bien con tildes), coma como separador,
#  fin de línea \r\n. El id de la cátedra va en el NOMBRE del archivo, no como columna.
#
#  SEGURIDAD (D71): los campos de texto libre de docentes y externos los escribe un
#  desconocido y se abrirán en Excel. Si empiezan por = + - @ (o tab/retorno) Excel los
#  tomaría por una fórmula: se les antepone una comilla simple. Los CSV de estudiantes y el
#  catálogo NO se tocan: sus valores están controlados y deben ser idénticos al archivo de carga.
# ==========================================================================

import csv
import io
import re
import zipfile

from servicios.api_catedras import ErrorApi, consultar

# Cuántas cátedras se piden por página a la API (la API acepta de 1 a 500).
TAMANO_PAGINA = 100

# Tipo (como lo escribe la URL) -> (vinculación que usa la API, columnas del CSV, campos de cada asistente)
TIPOS = {
    'estudiantes': ('ESTUDIANTE', ['CC', 'Carnet_estudiante', 'Programa'], ['cc', 'carnet', 'programa']),
    'docentes': ('DOCENTE', ['CC', 'Nombre', 'Contacto', 'Facultad'], ['cc', 'nombre', 'contacto', 'facultad']),
    'externos': ('EXTERNO', ['CC', 'Nombre', 'Contacto', 'Procedencia'], ['cc', 'nombre', 'contacto', 'procedencia']),
}

# Tipos cuyo texto lo escribe una persona cualquiera: se protegen contra las fórmulas de Excel.
TIPOS_CON_TEXTO_LIBRE = {'docentes', 'externos'}

_PERIODO = re.compile(r'^[0-9A-Za-z-]{1,6}$')
_ID_CATEDRA = re.compile(r'^[0-9]{9}$')


def periodo_valido(periodo):
    return bool(periodo and _PERIODO.fullmatch(periodo))


def id_catedra_valido(id_catedra):
    return bool(id_catedra and _ID_CATEDRA.fullmatch(id_catedra))


def celda_segura(valor):
    """Neutraliza la inyección de fórmulas en Excel/Sheets: un texto que empieza por = + - @ (o por
    tab o retorno de carro) se guarda con una comilla simple delante. None -> ''."""
    if valor is None:
        return ''
    texto = str(valor)
    if texto and texto[0] in ('=', '+', '-', '@', '\t', '\r'):
        return "'" + texto
    return texto


def nombre_archivo(id_catedra, tipo):
    return f'catedra_{id_catedra}_{tipo}.csv'


def _csv(columnas, filas):
    """Texto CSV (coma, \\r\\n) con el encabezado y las filas; UTF-8 con BOM, ya en bytes."""
    salida = io.StringIO(newline='')
    escritor = csv.writer(salida, lineterminator='\r\n')
    escritor.writerow(columnas)
    escritor.writerows(filas)
    return ('﻿' + salida.getvalue()).encode('utf-8')


def csv_de_asistentes(tipo, asistentes):
    """CSV de UN tipo ('estudiantes' | 'docentes' | 'externos') con los asistentes de ese tipo de una cátedra.
    Siempre trae el encabezado, aunque no haya nadie (en el ZIP los archivos sin asistentes salen así)."""
    vinculacion, columnas, campos = TIPOS[tipo]
    protegido = tipo in TIPOS_CON_TEXTO_LIBRE
    filas = []
    for a in asistentes:
        if a.get('tipo') != vinculacion:
            continue
        valores = [a.get(c) for c in campos]
        filas.append([celda_segura(v) for v in valores] if protegido else [('' if v is None else v) for v in valores])
    return _csv(columnas, filas)


def csv_catalogo(catedras):
    """CSV del catálogo del periodo: id_catedra, titulo (TODAS las cátedras que trae la API)."""
    return _csv(['id_catedra', 'titulo'], [[c['id_catedra'], c['titulo']] for c in catedras])


# --------------------------------------------------------------------------
#  Lectura de la API
# --------------------------------------------------------------------------

def catedras_del_periodo(periodo):
    """Cátedras del periodo con sus conteos (GET /api/Exportacion/catedras). Sin cátedras (204) -> []."""
    respuesta = consultar('Exportacion/catedras', periodo=periodo)
    return respuesta['datos'] if respuesta else []


def paginas_de_asistentes(periodo):
    """Recorre GET /api/Exportacion/asistentes por páginas (de TAMANO_PAGINA cátedras) hasta que 'siguiente'
    es null, y va entregando cada cátedra {id_catedra, titulo, asistentes}. Un fallo de la API sube como ErrorApi."""
    desde = None
    while True:
        r = consultar('Exportacion/asistentes', periodo=periodo, desde=desde, limite=TAMANO_PAGINA)
        for catedra in r['datos']:
            yield catedra
        desde = r.get('siguiente')
        if not desde:
            return


def asistentes_de_una_catedra(periodo, id_catedra):
    """Los asistentes de UNA cátedra del periodo. La API pagina por cátedra: se pide la página de 1 cátedra que
    empieza justo después del id anterior; si lo que llega no es esa cátedra, no tiene asistentes en el periodo."""
    anterior = int(id_catedra) - 1
    desde = f'{anterior:09d}' if anterior >= 0 else None
    r = consultar('Exportacion/asistentes', periodo=periodo, desde=desde, limite=1)
    for catedra in r['datos']:
        if catedra['id_catedra'] == id_catedra:
            return catedra['asistentes']
    return []


# --------------------------------------------------------------------------
#  El ZIP: los tres CSV de cada cátedra del periodo
# --------------------------------------------------------------------------

def zip_del_periodo(periodo):
    """Bytes de un ZIP con los TRES CSV de CADA cátedra del periodo (sin carpetas). Los archivos se generan
    siempre, con solo el encabezado si no hay asistentes de ese tipo: el contenido es predecible
    (3 archivos por cátedra del catálogo). Devuelve (bytes, cantidad_de_catedras)."""
    memoria = io.BytesIO()
    cantidad = 0
    with zipfile.ZipFile(memoria, 'w', zipfile.ZIP_DEFLATED) as archivo:
        for catedra in paginas_de_asistentes(periodo):
            cantidad += 1
            for tipo in TIPOS:
                archivo.writestr(nombre_archivo(catedra['id_catedra'], tipo), csv_de_asistentes(tipo, catedra['asistentes']))
    return memoria.getvalue(), cantidad


__all__ = ['ErrorApi', 'TIPOS', 'TAMANO_PAGINA', 'asistentes_de_una_catedra', 'catedras_del_periodo', 'celda_segura',
           'csv_catalogo', 'csv_de_asistentes', 'id_catedra_valido', 'nombre_archivo', 'paginas_de_asistentes',
           'periodo_valido', 'zip_del_periodo']
