import os
from dotenv import load_dotenv

load_dotenv()

class Config:
    SECRET_KEY = os.environ.get('SECRET_KEY') or 'dev-key-para-pruebas'
    # ApiCatedras (C#/ASP.NET Core) publica el puerto 8142 al host
    # (docker-compose.yml, servicio 'api': "8142:8042").
    API_BASE_URL = os.environ.get('API_BASE_URL') or 'https://apicatedraskipis.runasp.net/'
    DEBUG = True
    # v7.1 · D53: dirección PÚBLICA del front que va dentro del QR de asistencia
    # (p. ej. https://catedras.usbmed.edu.co). Vacía = la dirección con que se abre la
    # pantalla del QR. El celular del estudiante tiene que poder abrirla, y la
    # ubicación del navegador solo funciona en https (o localhost).
    URL_PUBLICA = (os.environ.get('URL_PUBLICA') or '').strip()
    TEMPLATES_AUTO_RELOAD = True

    # v7.2 · P15: cuántos proxies de confianza hay delante del front (0 = ninguno; en Render, 1). Ver app.py (ProxyFix).
    try:
        PROXY_CONFIABLES = int(os.environ.get('PROXY_CONFIABLES') or 0)
    except ValueError:
        PROXY_CONFIABLES = 0

    # v7.1 · RF-S6: correo real (servicios/correo.py). Todo por entorno; ninguna
    # credencial en el código. Sin SMTP_HOST el correo queda apagado y la
    # recuperación de clave avisa que no pudo enviar el código.
    SMTP_HOST = os.environ.get('SMTP_HOST') or ''
    SMTP_PORT = int(os.environ.get('SMTP_PORT') or 587)
    SMTP_SEGURIDAD = os.environ.get('SMTP_SEGURIDAD') or 'starttls'   # starttls | ssl | ninguna
    SMTP_USUARIO = os.environ.get('SMTP_USUARIO') or ''
    SMTP_CLAVE = os.environ.get('SMTP_CLAVE') or ''
    SMTP_REMITENTE = os.environ.get('SMTP_REMITENTE') or ''
    SMTP_NOMBRE_REMITENTE = os.environ.get('SMTP_NOMBRE_REMITENTE') or 'Cátedras USB'
    # '1' = no envía, escribe el correo en el log (solo desarrollo).
    CORREO_MODO_CONSOLA = os.environ.get('CORREO_MODO_CONSOLA') == '1'
