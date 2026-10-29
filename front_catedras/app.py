from flask import Flask, render_template
from flask_cors import CORS
from config import Config
from auth_helpers import registrar_en

from routes.catedras import catedras_bp
from routes.registro import registro_bp
from routes.admin import admin_bp
from routes.ponente import ponente_bp
from routes.ponente_catedras import ponente_catedras_bp   # v7.1: el ponente crea y se asigna a cátedras
from routes.estudiante import estudiante_bp
from routes.auth import auth_bp
from routes.perfil import perfil_bp   # v7.1: cambiar y recuperar la clave
from routes.carga import carga_bp   # v3: pantalla de carga de datos
from routes.sesiones import sesiones_bp   # v4: detalle de sesión y sus ponentes
from routes.encuestas import encuestas_bp   # v6: responder la encuesta de una sesión
from servicios import fechas               # v4: filtros de fecha y hora

app = Flask(__name__)
app.config.from_object(Config)

CORS(app)

# Ayudas de sesión para las plantillas (v5: leen la sesión real de Flask).
registrar_en(app)

# v4: filtros Jinja '|fecha_hora' y '|para_input' (UTC <-> hora de Colombia).
fechas.registrar_en(app)

app.register_blueprint(catedras_bp, url_prefix='/')
app.register_blueprint(registro_bp, url_prefix='/')
app.register_blueprint(auth_bp, url_prefix='/')
app.register_blueprint(perfil_bp, url_prefix='/')
app.register_blueprint(admin_bp, url_prefix='/admin')
app.register_blueprint(ponente_bp, url_prefix='/ponente')
app.register_blueprint(ponente_catedras_bp, url_prefix='/ponente/catedras')
app.register_blueprint(estudiante_bp, url_prefix='/estudiante')
app.register_blueprint(carga_bp, url_prefix='/')
app.register_blueprint(sesiones_bp, url_prefix='/')
app.register_blueprint(encuestas_bp, url_prefix='/')


@app.errorhandler(403)
def sin_permiso(e):
    # v5/v7.1: usuario con sesión pero sin el rol que pide la zona (/admin, /estudiante, /ponente).
    return render_template('errors/403.html'), 403


@app.errorhandler(404)
def page_not_found(e):
    return render_template('errors/404.html'), 404


@app.errorhandler(500)
def internal_server_error(e):
    return render_template('errors/500.html'), 500


if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
