import os
from dotenv import load_dotenv

load_dotenv()

class Config:
    SECRET_KEY = os.environ.get('SECRET_KEY') or 'dev-key-para-pruebas'
    # ApiCatedras (C#/ASP.NET Core) publica el puerto 8142 al host
    # (docker-compose.yml, servicio 'api': "8142:8042").
    API_BASE_URL = os.environ.get('API_BASE_URL') or 'http://localhost:8142/api'
    DEBUG = True
    TEMPLATES_AUTO_RELOAD = True
