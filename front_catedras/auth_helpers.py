# ==========================================================================
#  auth_helpers.py  ·  Front Cátedras · Versión 2
# --------------------------------------------------------------------------
#  Ayudas de autenticación para las plantillas (RF92 de version2/2_spec.md).
#
#  En la v2 NO hay login real: la tabla 'usuario' llega a la API en la v5
#  (mapa de versiones). Por eso estas funciones responden SIEMPRE
#  "sin sesión". Existen ya para que 'base.html' y el navbar pregunten
#  "¿hay sesión?" desde ahora, y en la v5 solo cambie ESTE archivo.
# ==========================================================================


def sesion_activa():
    """¿Hay un usuario autenticado? En la v2, siempre False."""
    return False


def usuario_actual():
    """El usuario autenticado, o None. En la v2, siempre None."""
    return None


def registrar_en(app):
    """Expone las ayudas a TODAS las plantillas Jinja.

    'context_processor' es un gancho de Flask: el diccionario que devuelve
    se agrega a las variables de cada render_template, así el navbar puede
    escribir {{ sesion_activa() }} sin que cada ruta lo pase a mano.
    """
    @app.context_processor
    def _inyectar_ayudas_de_sesion():
        return {'sesion_activa': sesion_activa, 'usuario_actual': usuario_actual}
