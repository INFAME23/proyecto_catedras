# ==========================================================================
#  routes/auth.py  ·  Front Cátedras · Versión 5
# --------------------------------------------------------------------------
#  Login real. Llama 'POST /api/Usuario/login' y, si la API responde 200,
#  guarda el usuario y sus roles en la SESIÓN DE FLASK (cookie firmada con
#  SECRET_KEY). La API no guarda sesión ni emite token (ADR D28/D29).
#
#  Si la API responde 401 se muestra SIEMPRE el mismo mensaje genérico, sin
#  distinguir usuario inexistente / inactivo / clave incorrecta (ADR D27).
# ==========================================================================

from flask import Blueprint, flash, redirect, render_template, request, session, url_for

from auth_helpers import endpoint_de_inicio, sesion_activa
from servicios.api_catedras import ErrorApi, crear

auth_bp = Blueprint('auth', __name__)

MENSAJE_GENERICO = 'Usuario o clave incorrectos.'


def _destino_seguro(siguiente):
    """Solo se acepta volver a una ruta INTERNA ('/admin/usuarios'); nunca a
    otro sitio ('//malo.com', 'http://malo.com'): sería un redireccionamiento abierto."""
    if siguiente and siguiente.startswith('/') and not siguiente.startswith('//') and '\\' not in siguiente:
        return siguiente
    return None


@auth_bp.route('/login', methods=['GET', 'POST'])
def login():
    siguiente = _destino_seguro(request.values.get('siguiente'))

    if request.method == 'GET':
        if sesion_activa():
            return redirect(siguiente or url_for(endpoint_de_inicio()))
        return render_template('login.html', siguiente=siguiente, usuario='')

    usuario = request.form.get('usuario', '').strip()
    clave = request.form.get('clave', '')      # la clave NO se recorta: los espacios cuentan

    try:
        respuesta = crear('Usuario/login', {'usuario': usuario, 'clave': clave})
    except ErrorApi as ex:
        if ex.codigo in (401, 422):
            # 401 = datos incorrectos (D27: mensaje único). 422 = campo vacío.
            flash(MENSAJE_GENERICO, 'error')
        else:
            # La API caída o con error: no es culpa de las credenciales.
            flash(f"No se pudo iniciar sesión. {ex.mensaje}", 'error')
        return render_template('login.html', siguiente=siguiente, usuario=usuario), (
            401 if ex.codigo == 401 else 200)

    # v7.1 · D55: solo el administrador inicia sesión (la API ya lo exige; esto es el cinturón del front).
    if 'ADMIN' not in list(respuesta.get('roles', [])):
        flash(MENSAJE_GENERICO, 'error')
        return render_template('login.html', siguiente=siguiente, usuario=usuario), 401

    datos = respuesta['usuario']
    session.clear()                              # nunca se reutiliza una sesión anterior
    session['usuario'] = {
        'id_usuario': datos['id_usuario'],
        'usuario': datos['usuario'],
        'fk_asistente': datos['fk_asistente'],
    }
    session['roles'] = list(respuesta.get('roles', []))
    # v7.1 · D54: si a esta cuenta se le exigió cambiar la clave (clave temporal), hasta que la cambie, el
    # before_request de app.py solo le deja ver la pantalla de cambiar la clave.
    session['debe_cambiar'] = bool(datos.get('debe_cambiar_clave'))
    if session['debe_cambiar']:
        return redirect(url_for('perfil.cambiar_clave'))

    flash(f"Bienvenido, {datos['usuario']}.", 'exito')
    if siguiente:
        return redirect(siguiente)
    # v7.1 · D55: solo el administrador inicia sesión: va al panel de administración.
    return redirect(url_for(endpoint_de_inicio()))


@auth_bp.route('/logout', methods=['POST'])
def logout():
    session.clear()
    flash('Sesión cerrada.', 'exito')
    return redirect(url_for('catedras.inicio'))
