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

from auth_helpers import ROL_ADMIN, sesion_activa
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
            return redirect(siguiente or url_for('catedras.inicio'))
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

    datos = respuesta['usuario']
    session.clear()                              # nunca se reutiliza una sesión anterior
    session['usuario'] = {
        'id_usuario': datos['id_usuario'],
        'usuario': datos['usuario'],
        'fk_asistente': datos['fk_asistente'],
    }
    session['roles'] = list(respuesta.get('roles', []))

    flash(f"Bienvenido, {datos['usuario']}.", 'exito')
    if siguiente:
        return redirect(siguiente)
    if ROL_ADMIN in session['roles']:
        return redirect(url_for('admin.panel'))
    return redirect(url_for('catedras.inicio'))


@auth_bp.route('/logout', methods=['POST'])
def logout():
    session.clear()
    flash('Sesión cerrada.', 'exito')
    return redirect(url_for('catedras.inicio'))
