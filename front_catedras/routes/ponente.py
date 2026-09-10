# ==========================================================================
#  routes/ponente.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Placeholder: depende de tablas (catedra, sesion, ponencia) que aún no
#  están en ApiCatedras. Existe para que 'app.py' registre el blueprint
#  sin cambios.
# ==========================================================================

from flask import Blueprint, render_template

ponente_bp = Blueprint('ponente', __name__)


@ponente_bp.route('/')
def inicio():
    return render_template('en_construccion.html', seccion='Panel de ponente')
