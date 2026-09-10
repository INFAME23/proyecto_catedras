# ==========================================================================
#  routes/registro.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Placeholder: el registro de asistencia depende de 'sesion' y
#  'registro_asistencia', que aún no están en ApiCatedras. Existe para que
#  'app.py' registre el blueprint sin cambios.
# ==========================================================================

from flask import Blueprint, render_template

registro_bp = Blueprint('registro', __name__)


@registro_bp.route('/registro')
def inicio():
    return render_template('en_construccion.html', seccion='Registro de asistencia')
