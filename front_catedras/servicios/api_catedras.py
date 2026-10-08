# ==========================================================================
#  servicios/api_catedras.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Cliente HTTP hacia ApiCatedras (la API real en C#/ASP.NET Core). Centraliza
#  las 5 operaciones (listar/obtener/crear/reemplazar/eliminar) para que las
#  rutas de Flask no repitan el manejo de errores de 'requests' ni el sobre
#  de respuesta de la API ({estado, mensaje, detalle, errores:[...]}).
#
#  Este front NO reimplementa reglas de negocio (límite > 0, fechas
#  coherentes, canal_difusion en una lista cerrada...): esas reglas ya
#  las aplica ApiCatedras y aquí solo se traduce su respuesta de error a
#  algo que la plantilla pueda mostrar.
# ==========================================================================

from urllib.parse import quote

from flask import current_app
import requests


def llave(*partes):
    """v4 · Arma el "id" de un recurso de LLAVE COMPUESTA en la URL.

    'Ponencia' no tiene una columna 'id': su llave son 3 columnas y la API
    las recibe como 3 segmentos de ruta (ADR D19):
        llave(10, 167, 'PONENTE') -> '10/167/PONENTE'
        obtener('Ponencia', llave(10, 167, 'PONENTE'))
        -> GET /api/Ponencia/10/167/PONENTE
    'quote' escapa cada segmento por si trae espacios o '/'.
    """
    return '/'.join(quote(str(parte), safe='') for parte in partes)


class ErrorApi(Exception):
    """Envuelve un error devuelto por ApiCatedras (4xx/5xx) o de red."""

    def __init__(self, mensaje, detalle=None, errores=None, codigo=None):
        super().__init__(mensaje)
        self.mensaje = mensaje
        self.detalle = detalle
        self.errores = errores or []  # lista de {campo, mensaje} del 422
        self.codigo = codigo


def _url(recurso, id_valor=None):
    base = current_app.config['API_BASE_URL'].rstrip('/')
    if id_valor is None:
        return f"{base}/{recurso}"
    return f"{base}/{recurso}/{id_valor}"


def _procesar(respuesta):
    """Devuelve el JSON en éxito; lanza ErrorApi con el sobre de la API en error."""
    if respuesta.status_code == 204:
        return None

    if respuesta.ok:
        return respuesta.json() if respuesta.content else None

    cuerpo = {}
    try:
        cuerpo = respuesta.json()
    except ValueError:
        pass

    raise ErrorApi(
        mensaje=cuerpo.get('mensaje', 'La API respondió con un error.'),
        detalle=cuerpo.get('detalle'),
        errores=cuerpo.get('errores', []),
        codigo=respuesta.status_code,
    )


def listar(recurso, limite=None, timeout=5, **filtros):
    """GET /api/{recurso}. Tabla vacía (204) -> lista vacía, no error.

    'timeout' (segundos) es configurable porque el catálogo de cátedras
    trae las 5502 filas en una sola llamada (ADR D13 de la v2) y puede
    tardar más que una tabla catálogo pequeña.

    v3: '**filtros' son parámetros de consulta extra que la API entiende,
    p. ej. listar('Asistente', limite=50, q='garcia', pagina=2) o
    listar('DocumentoAsistente', fkAsistente=7). Los que vienen en None
    no se mandan.
    """
    try:
        parametros = {'limite': limite} if limite else {}
        # '**filtros' recoge los argumentos con nombre que no están en la
        # firma; aquí se agregan solo los que traen valor.
        parametros.update({clave: valor for clave, valor in filtros.items() if valor is not None})
        respuesta = requests.get(_url(recurso), params=parametros, timeout=timeout)
    except requests.RequestException as ex:
        raise ErrorApi(f"No se pudo contactar la API en '{_url(recurso)}'.", detalle=str(ex))

    cuerpo = _procesar(respuesta)
    return cuerpo['datos'] if cuerpo else []


def obtener(recurso, id_valor):
    """GET /api/{recurso}/{id}. Inexistente -> ErrorApi(codigo=404)."""
    try:
        respuesta = requests.get(_url(recurso, id_valor), timeout=5)
    except requests.RequestException as ex:
        raise ErrorApi(f"No se pudo contactar la API en '{_url(recurso, id_valor)}'.", detalle=str(ex))

    return _procesar(respuesta)


def crear(recurso, datos):
    """POST /api/{recurso}."""
    try:
        respuesta = requests.post(_url(recurso), json=datos, timeout=5)
    except requests.RequestException as ex:
        raise ErrorApi(f"No se pudo contactar la API en '{_url(recurso)}'.", detalle=str(ex))

    return _procesar(respuesta)


def reemplazar(recurso, id_valor, datos):
    """PUT /api/{recurso}/{id} (reemplazo completo)."""
    try:
        respuesta = requests.put(_url(recurso, id_valor), json=datos, timeout=5)
    except requests.RequestException as ex:
        raise ErrorApi(f"No se pudo contactar la API en '{_url(recurso, id_valor)}'.", detalle=str(ex))

    return _procesar(respuesta)


def actualizar_parcial(recurso, id_valor, datos):
    """PATCH /api/{recurso}/{id} (actualización parcial).

    v3: también sirve para las ACCIONES de las tablas de llave compuesta,
    donde el "id" de la URL es el nombre de la acción:
      actualizar_parcial('VinculacionAsistente', 'cerrar', {...llave...})
      -> PATCH /api/VinculacionAsistente/cerrar
    """
    try:
        respuesta = requests.patch(_url(recurso, id_valor), json=datos, timeout=5)
    except requests.RequestException as ex:
        raise ErrorApi(f"No se pudo contactar la API en '{_url(recurso, id_valor)}'.", detalle=str(ex))

    return _procesar(respuesta)


def eliminar(recurso, id_valor):
    """DELETE /api/{recurso}/{id}."""
    try:
        respuesta = requests.delete(_url(recurso, id_valor), timeout=5)
    except requests.RequestException as ex:
        raise ErrorApi(f"No se pudo contactar la API en '{_url(recurso, id_valor)}'.", detalle=str(ex))

    return _procesar(respuesta)
