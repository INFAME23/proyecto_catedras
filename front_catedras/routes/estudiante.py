# ==========================================================================
#  routes/estudiante.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Placeholder: depende de tablas (asistente, registro_asistencia) que aún
#  no están en ApiCatedras. Existe para que 'app.py' registre el blueprint
#  sin cambios.
# ==========================================================================

from flask import Blueprint, render_template

estudiante_bp = Blueprint('estudiante', __name__)


@estudiante_bp.route('/')
def inicio():
    return render_template('en_construccion.html', seccion='Panel de estudiante')
