# ==========================================================================
#  servicios/fechas.py  ·  Front Cátedras · Versión 4
# --------------------------------------------------------------------------
#  La v4 trae las primeras columnas con FECHA Y HORA: 'inicio' y 'fin' de
#  una sesión (TIMESTAMPTZ). La API las devuelve en UTC
#  ("2026-10-20T19:00:00Z") y las recibe con su zona
#  ("2026-10-20T14:00:00-05:00") — ADR D21 de version4/4_research.md.
#
#  Este módulo hace las DOS traducciones que necesita el front:
#    · API -> pantalla: de UTC a la hora de Colombia, para mostrar.
#    · formulario -> API: el <input type="datetime-local"> no trae zona
#      ("2026-10-20T14:00"); se le agrega la de Colombia antes de enviarlo.
#
#  Colombia no tiene horario de verano: su zona es SIEMPRE UTC-5. Por eso
#  se usa un desfase fijo y no hace falta instalar la base de datos de
#  zonas horarias (tzdata) en la imagen del front.
# ==========================================================================

import re
from datetime import datetime, timedelta, timezone

# UTC-5 fijo, con nombre para mostrar.
ZONA_COLOMBIA = timezone(timedelta(hours=-5), 'COT')

# .NET puede escribir hasta 7 decimales en los segundos; Python acepta hasta 6.
_FRACCION_LARGA = re.compile(r'(\.\d{6})\d+')


def a_local(texto_iso):
    """'2026-10-20T19:00:00Z' -> datetime de las 14:00 en Colombia. None si no se puede leer."""
    if not texto_iso:
        return None
    try:
        texto = _FRACCION_LARGA.sub(r'\1', texto_iso.replace('Z', '+00:00'))
        momento = datetime.fromisoformat(texto)
    except (TypeError, ValueError):
        return None
    if momento.tzinfo is None:          # sin zona: se asume que ya es UTC
        momento = momento.replace(tzinfo=timezone.utc)
    return momento.astimezone(ZONA_COLOMBIA)


def fecha_hora(texto_iso, formato='%Y-%m-%d %H:%M'):
    """Filtro Jinja: muestra un instante de la API en hora de Colombia."""
    momento = a_local(texto_iso)
    return momento.strftime(formato) if momento else (texto_iso or '')


def para_input(texto_iso):
    """Filtro Jinja: valor para un <input type="datetime-local"> ('2026-10-20T14:00')."""
    momento = a_local(texto_iso)
    return momento.strftime('%Y-%m-%dT%H:%M') if momento else ''


def desde_input(texto):
    """Lo que escribe el <input type="datetime-local"> -> texto ISO CON zona
    para la API: '2026-10-20T14:00' -> '2026-10-20T14:00:00-05:00'.
    Vacío -> None (la API responderá 422 si el campo era obligatorio)."""
    texto = (texto or '').strip()
    if not texto:
        return None
    try:
        momento = datetime.fromisoformat(texto)
    except ValueError:
        return texto                     # que la API lo rechace con su 422
    return momento.replace(tzinfo=ZONA_COLOMBIA).isoformat()


def registrar_en(app):
    """Expone 'fecha_hora' y 'para_input' como filtros de TODAS las plantillas."""
    app.add_template_filter(fecha_hora, 'fecha_hora')
    app.add_template_filter(para_input, 'para_input')
