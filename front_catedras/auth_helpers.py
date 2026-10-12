# ==========================================================================
#  auth_helpers.py  ·  Front Cátedras · Versión 5
# --------------------------------------------------------------------------
#  Ayudas de autenticación (RF92 de version2/2_spec.md; completadas en v5).
#
#  v2: las funciones respondían SIEMPRE "sin sesión" (no había tabla
#  'usuario' en la API). v5: ya existe 'POST /api/Usuario/login' y este
#  archivo lee la SESIÓN DE FLASK, que routes/auth.py llena al iniciar
#  sesión. Como prometía la v2, solo cambió ESTE archivo: el navbar y
#  'base.html' siguen preguntando "¿hay sesión?" igual que antes.
#
#  OJO (ADR D28 / D29): la sesión de Flask es un mecanismo PROPIO DEL FRONT.
#  Es una cookie firmada con SECRET_KEY; la API no la conoce ni la exige.
#  La protección de '/admin' vive aquí, en el front, y la API sigue abierta.
# ==========================================================================

from functools import wraps

from flask import abort, redirect, request, session, url_for

ROL_ADMIN = 'ADMIN'


def sesion_activa():
    """¿Hay un usuario autenticado?"""
    return 'usuario' in session


def usuario_actual():
    """El usuario autenticado como diccionario
    ({'id_usuario', 'usuario', 'fk_asistente'}), o None si no hay sesión."""
    return session.get('usuario')


def roles_actuales():
    """Lista de códigos de rol del usuario autenticado ([] si no hay sesión)."""
    return list(session.get('roles', []))


def tiene_rol(rol):
    """¿El usuario autenticado tiene este rol? (False si no hay sesión)."""
    return sesion_activa() and rol in session.get('roles', [])


def exigir_rol(rol):
    """Regla de acceso, reutilizable por un before_request:

      - sin sesión            -> redirige a /login (recordando a dónde iba)
      - con sesión, sin el rol -> 403
      - con sesión y el rol    -> None (Flask sigue con la ruta)
    """
    if not sesion_activa():
        return redirect(url_for('auth.login', siguiente=request.full_path.rstrip('?')))
    if not tiene_rol(rol):
        abort(403)
    return None


def rol_requerido(rol):
    """Decorador para proteger UNA ruta suelta con la misma regla de
    'exigir_rol'. (Para todo '/admin' se usa el before_request del blueprint.)"""
    def decorador(vista):
        @wraps(vista)
        def envuelta(*args, **kwargs):
            respuesta = exigir_rol(rol)
            return respuesta if respuesta is not None else vista(*args, **kwargs)
        return envuelta
    return decorador


def registrar_en(app):
    """Expone las ayudas a TODAS las plantillas Jinja.

    'context_processor' es un gancho de Flask: el diccionario que devuelve
    se agrega a las variables de cada render_template, así el navbar puede
    escribir {{ sesion_activa() }} sin que cada ruta lo pase a mano.
    """
    @app.context_processor
    def _inyectar_ayudas_de_sesion():
        return {
            'sesion_activa': sesion_activa,
            'usuario_actual': usuario_actual,
            'roles_actuales': roles_actuales,
            'tiene_rol': tiene_rol,
        }
