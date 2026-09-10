# ==========================================================================
#  routes/catedras.py  ·  Front Cátedras
# --------------------------------------------------------------------------
#  Página de inicio pública. Por ahora solo enlaza al panel de
#  administración (§ las 4 tablas ya construidas en ApiCatedras); el resto
#  de la aplicación (registro de asistencia, ponente, estudiante, auth)
#  llega cuando la API tenga esas tablas.
# ==========================================================================

from flask import Blueprint, render_template

catedras_bp = Blueprint('catedras', __name__)


@catedras_bp.route('/')
def inicio():
    return render_template('inicio.html')
