# ==========================================================================
#  servicios/qr_sesiones.py  ·  Front Cátedras · v7.1 · D53
# --------------------------------------------------------------------------
#  El QR de cierre de una sesión. Un QR es el enlace de registro de la v5 con
#  una ventana que se abre unos minutos ANTES de que termine la sesión y se
#  cierra cuando termina, y que solo deja registrar a quien está INSCRITO en
#  la cátedra (la regla y las fechas las pone la BD, migración 051; aquí solo
#  se pide el QR a ApiCatedras y se dibuja).
#
#  Dentro del QR va la URL del front  <base>/registro/<token>. La <base> sale de
#  URL_PUBLICA (config/entorno) y, si no está, de la dirección con la que se
#  abrió esta pantalla. OJO: el celular del estudiante tiene que poder abrir
#  esa dirección y el navegador solo entrega la ubicación en https (o
#  localhost): en producción hay que poner URL_PUBLICA con la dirección https.
# ==========================================================================

import re
from datetime import datetime, timezone

from flask import current_app, request, url_for

from servicios.api_catedras import ErrorApi, consultar, crear, listar


def svg_qr(texto):
    """El QR de 'texto' como <svg> listo para incrustar (escala con CSS: lleva viewBox).
    Corrección de errores media ('m'): se lee bien aunque la pantalla tenga brillo o reflejos."""
    import segno
    svg = segno.make(texto, error='m').svg_inline(scale=8, border=2, dark='#000000', light='#ffffff')
    ancho = re.search(r'width="(\d+)"', svg)
    alto = re.search(r'height="(\d+)"', svg)
    if ancho and alto:
        svg = svg.replace('<svg ', f'<svg viewBox="0 0 {ancho.group(1)} {alto.group(1)}" role="img" '
                                   'aria-label="Código QR de asistencia" ', 1)
    return svg


def url_de_registro(token):
    """La URL completa que va dentro del QR."""
    base = (current_app.config.get('URL_PUBLICA') or request.url_root).rstrip('/')
    return base + url_for('registro.solicitar', token=str(token))


def emitir_qr(id_sesion, id_usuario=None, regenerar=False):
    """Crea el QR de la sesión (o devuelve el vigente). Lanza ErrorApi si no se pudo."""
    return crear('QrSesion', {'fk_sesion': id_sesion, 'fk_usuario': id_usuario, 'regenerar': bool(regenerar)})


def qr_de_sesion(id_sesion):
    """El QR vigente de la sesión, o None si no tiene (la API responde 204)."""
    return consultar('QrSesion', fkSesion=id_sesion)


def asegurar_qr_de_catedra(id_catedra, id_usuario=None):
    """Emite el QR de cada sesión de una cátedra recién creada. Devuelve (hechos, total).
    No lanza: si algo falla, esas sesiones quedan sin QR y se pueden generar desde su pantalla."""
    try:
        sesiones = listar('Sesion', limite=500, fkCatedra=id_catedra)
    except ErrorApi:
        return 0, 0
    hechos = 0
    for s in sesiones:
        try:
            emitir_qr(s['id_sesion'], id_usuario)
            hechos += 1
        except ErrorApi:
            pass
    return hechos, len(sesiones)


def _momento(texto_iso):
    try:
        return datetime.fromisoformat(texto_iso.replace('Z', '+00:00'))
    except (AttributeError, ValueError):
        return None


def estado_de_ventana(qr, ahora=None):
    """'pendiente' (aún no abre), 'abierto' o 'cerrado' (o 'revocado'), según la ventana del QR."""
    if not qr:
        return 'sin_qr'
    if qr.get('revocado_en'):
        return 'revocado'
    ahora = ahora or datetime.now(timezone.utc)
    desde, hasta = _momento(qr.get('ventana_desde')), _momento(qr.get('ventana_hasta'))
    if desde is None or hasta is None:
        return 'cerrado'
    if ahora < desde:
        return 'pendiente'
    return 'abierto' if ahora < hasta else 'cerrado'
