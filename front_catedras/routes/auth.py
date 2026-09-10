# ==========================================================================
#  routes/auth.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Placeholder: ApiCatedras todavía no tiene autenticación (tabla 'usuario'
#  es de otra versión). Existe para que 'app.py' registre el blueprint sin
#  cambios; se completa cuando la API exponga login real.
# ==========================================================================

from flask import Blueprint, render_template

auth_bp = Blueprint('auth', __name__)


@auth_bp.route('/login')
def login():
    return render_template('en_construccion.html', seccion='Inicio de sesión')
