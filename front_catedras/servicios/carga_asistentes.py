# ==========================================================================
#  servicios/carga_asistentes.py  ·  Front Cátedras · Versión 3
# --------------------------------------------------------------------------
#  Interpreta el archivo de personas (Excel .xlsx o .csv) y convierte cada
#  fila en el body de POST /api/Asistente.
#
#  ADR D17: el archivo se lee AQUÍ, en Python, y no en la API: leer Excel
#  en C# exigiría un paquete NuGet nuevo, que el Artículo 2 no permite.
#  Este módulo SOLO traduce formatos (columnas, números, textos): NO decide
#  si una fila es válida. Esa decisión es de la API (petición -> 422,
#  servicio -> 400, BD -> 500); si la API la rechaza, su mensaje se guarda
#  como motivo en 'novedad_carga'.
#
#  Columnas que se reconocen (la primera fila del archivo es el encabezado;
#  mayúsculas, tildes y espacios no importan):
#    id_asis · nombres · apellidos · nombre_completo · correo_institucional
#    correo_personal · celular · es_externo · tipo_documento · documento
#    tipo_vinculacion · programa · periodo
#  Cualquier otra columna se ignora (pero queda en el contenido crudo).
# ==========================================================================

import csv
import io
import unicodedata

# Para .xlsx. Es una librería del FRONT (requirements.txt), no de la API.
from openpyxl import load_workbook

# Largo máximo de 'novedad_carga.motivo' (VARCHAR(200) en la BD).
LARGO_MAXIMO_MOTIVO = 200

# Encabezado normalizado -> campo. Varios nombres posibles por campo, porque
# el archivo real del ASIS y un archivo armado a mano no siempre coinciden.
SINONIMOS = {
    'id_asis': ['id_asis', 'emplid', 'codigo_asis'],
    'nombres': ['nombres', 'nombre'],
    'apellidos': ['apellidos', 'apellido'],
    'nombre_completo_asis': ['nombre_completo', 'nombre_completo_asis'],
    'correo_institucional': ['correo_institucional', 'email_institucional', 'correo'],
    'correo_personal': ['correo_personal', 'email_personal'],
    'celular': ['celular', 'telefono', 'movil'],
    'es_externo': ['es_externo', 'externo'],
    'tipo_documento': ['tipo_documento', 'tipo_doc'],
    'documento': ['documento', 'numero_documento', 'num_documento'],
    'tipo_vinculacion': ['tipo_vinculacion', 'vinculacion'],
    'programa': ['programa', 'codigo_programa', 'programa_academico'],
    'periodo': ['periodo', 'periodo_academico'],
}

# Valores que se entienden como "sí" en la columna es_externo.
VALORES_SI = {'si', 's', 'true', '1', 'x', 'yes'}


class ErrorArchivo(Exception):
    """El archivo no se pudo leer (formato no soportado, dañado, sin encabezado)."""


def normalizar(texto):
    """'Ingeniería  de Datos' -> 'ingenieria de datos': minúsculas, sin
    tildes y con un solo espacio entre palabras."""
    # NFKD separa cada letra de su tilde ('í' -> 'i' + '´'); luego se
    # descartan las marcas ('combining') y queda la letra sola.
    sin_tildes = ''.join(c for c in unicodedata.normalize('NFKD', str(texto))
                         if not unicodedata.combining(c))
    return ' '.join(sin_tildes.lower().split())


def _encabezado_a_campo(encabezado):
    """Encabezado del archivo -> nombre de campo conocido, o None si no se reconoce."""
    clave = normalizar(encabezado).replace(' ', '_')
    for campo, nombres in SINONIMOS.items():
        if clave in nombres:
            return campo
    return None


def _celda_a_texto(valor):
    """Valor de una celda -> texto limpio ('' si está vacía).
    Excel guarda 1020304050 como número: 1020304050.0 debe volver a ser
    '1020304050' (sin '.0'), o el documento y el id_asis quedarían mal."""
    if valor is None:
        return ''
    if isinstance(valor, float) and valor.is_integer():
        valor = int(valor)
    return str(valor).strip()


def leer_filas(archivo):
    """Lee el archivo subido (un FileStorage de Flask) y devuelve una lista
    de (numero_fila, fila_original, campos):
      · numero_fila: el renglón en el archivo (el encabezado es el 1, así
        que la primera persona es la fila 2) — para encontrarla a mano.
      · fila_original: {encabezado: texto} tal como llegó (contenido crudo).
      · campos: {campo_conocido: texto} ya traducido.
    Las filas totalmente vacías se saltan."""
    nombre = (archivo.filename or '').lower()
    contenido = archivo.read()

    if nombre.endswith('.csv'):
        renglones = _renglones_csv(contenido)
    elif nombre.endswith('.xlsx'):
        renglones = _renglones_xlsx(contenido)
    else:
        raise ErrorArchivo('Formato no soportado: sube un archivo .xlsx o .csv.')

    if not renglones:
        raise ErrorArchivo('El archivo está vacío.')

    encabezados = [_celda_a_texto(h) for h in renglones[0]]
    if not any(encabezados):
        raise ErrorArchivo('La primera fila debe tener los nombres de las columnas.')
    campos_por_columna = [_encabezado_a_campo(h) for h in encabezados]

    filas = []
    # 'enumerate(..., start=2)': el primer renglón de datos es la fila 2 del archivo.
    for numero_fila, renglon in enumerate(renglones[1:], start=2):
        valores = [_celda_a_texto(v) for v in renglon]
        if not any(valores):
            continue   # renglón vacío: no es una persona

        original, campos = {}, {}
        # 'zip' recorre encabezados, campos y valores a la vez, en paralelo.
        for encabezado, campo, valor in zip(encabezados, campos_por_columna, valores):
            original[encabezado] = valor
            if campo and valor:
                campos[campo] = valor
        filas.append((numero_fila, original, campos))
    return filas


def _renglones_csv(contenido):
    # 'utf-8-sig' acepta (y quita) la marca BOM que pone Excel al guardar CSV.
    try:
        texto = contenido.decode('utf-8-sig')
    except UnicodeDecodeError:
        texto = contenido.decode('latin-1')
    # 'Sniffer' adivina el separador: Excel en español suele usar ';'.
    try:
        dialecto = csv.Sniffer().sniff(texto[:2048], delimiters=',;\t')
    except csv.Error:
        dialecto = csv.excel
    return [r for r in csv.reader(io.StringIO(texto), dialecto)]


def _renglones_xlsx(contenido):
    try:
        # 'read_only' lee sin cargar estilos (rápido con miles de filas);
        # 'data_only' devuelve el VALOR de las fórmulas, no la fórmula.
        libro = load_workbook(io.BytesIO(contenido), read_only=True, data_only=True)
    except Exception as ex:   # archivo dañado o que no es Excel de verdad
        raise ErrorArchivo(f'No se pudo abrir el Excel: {ex}')
    hoja = libro.active   # la primera hoja
    renglones = [list(r) for r in hoja.iter_rows(values_only=True)]
    libro.close()
    return renglones


# --------------------------------------------------------------------------
#  Programa en texto libre -> código (lo que hace fn_resolver_programa en la
#  BD, pero aquí en Python contra el catálogo ya cargado — ADR D17).
# --------------------------------------------------------------------------

def indice_de_programas(programas, alias):
    """Arma {texto_normalizado: codigo} con los códigos, los nombres de los
    programas y los alias de AliasPrograma (v2)."""
    indice = {}
    for p in programas:
        indice[normalizar(p['codigo'])] = p['codigo']
        indice.setdefault(normalizar(p['nombre']), p['codigo'])
    for a in alias:
        indice.setdefault(normalizar(a['texto_normalizado']), a['fk_programa'])
    return indice


def resolver_programa(texto, indice):
    """Código del programa, o el TEXTO TAL CUAL si no se pudo resolver: así
    la API lo rechaza con un motivo claro (no se inventa un programa)."""
    return indice.get(normalizar(texto), texto)


# --------------------------------------------------------------------------
#  Fila -> body de POST /api/Asistente
# --------------------------------------------------------------------------

def construir_asistente(campos, fk_lote_carga, indice_programas):
    """Traduce una fila al body que espera la API, con sus detalles
    iniciales (Guía IA, Regla 1). Solo se incluye lo que la fila trae: la
    API decide si falta algo obligatorio."""
    datos = {
        # Si la columna viene vacía se manda null (no ""): la API responde
        # un solo error claro, "campo obligatorio".
        'nombres': campos.get('nombres'),
        'apellidos': campos.get('apellidos'),
        'fk_lote_carga': fk_lote_carga,
        'es_externo': normalizar(campos.get('es_externo', '')) in VALORES_SI,
    }
    for campo in ('id_asis', 'nombre_completo_asis', 'correo_institucional', 'correo_personal', 'celular'):
        if campos.get(campo):
            datos[campo] = campos[campo]

    if campos.get('documento'):
        datos['documentos'] = [{
            # Sin tipo -> cédula ('CC'), el caso del archivo del ASIS.
            'fk_tipo_documento': campos.get('tipo_documento', 'CC').upper(),
            'numero': campos['documento'],
        }]
    if campos.get('tipo_vinculacion'):
        datos['vinculaciones'] = [{'fk_tipo_vinculacion': campos['tipo_vinculacion'].upper()}]
    if campos.get('programa'):
        datos['programas'] = [{
            'fk_programa': resolver_programa(campos['programa'], indice_programas),
            'fk_periodo': campos.get('periodo', ''),
        }]
    return datos


def contenido_crudo(fila_original):
    """La fila tal como llegó, en una línea: 'col=valor; col=valor'."""
    return '; '.join(f'{columna}={valor}' for columna, valor in fila_original.items() if columna)


def motivo_de(error_api):
    """Mensaje de rechazo de la API -> texto de máximo 200 caracteres.
    Prefiere el detalle concreto (qué campo, qué restricción) al genérico."""
    if error_api.errores:
        texto = '; '.join(f"{e.get('campo')}: {e.get('mensaje')}" for e in error_api.errores)
    else:
        texto = error_api.detalle or error_api.mensaje
    # Los errores de PostgreSQL traen saltos de línea: se dejan en una sola.
    texto = ' '.join(str(texto).split())
    if error_api.codigo:
        texto = f'[{error_api.codigo}] {texto}'
    if len(texto) > LARGO_MAXIMO_MOTIVO:
        texto = texto[:LARGO_MAXIMO_MOTIVO - 3] + '...'
    return texto
