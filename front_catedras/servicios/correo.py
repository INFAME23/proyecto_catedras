# ==========================================================================
#  servicios/correo.py  ·  Front Cátedras · Subversión v7.1 (RF-S6)
# --------------------------------------------------------------------------
#  Envío REAL de correo por SMTP, para el código de recuperación de clave.
#  (El código de la clave de acceso del autorregistro, v5, sigue
#  mostrándose en pantalla: RNF12 y routes/registro.py.)
#
#  CONFIGURACIÓN: solo por variables de entorno (config.py las lee de .env);
#  ninguna credencial vive en el código ni en git.
#
#      SMTP_HOST              servidor (vacío = correo sin configurar)
#      SMTP_PORT              587 por defecto
#      SMTP_SEGURIDAD         'starttls' (587, por defecto) | 'ssl' (465) | 'ninguna'
#      SMTP_USUARIO           cuenta con la que se autentica (vacío = sin autenticar)
#      SMTP_CLAVE             contraseña / contraseña de aplicación
#      SMTP_REMITENTE         dirección del 'From' (por defecto, SMTP_USUARIO)
#      SMTP_NOMBRE_REMITENTE  nombre visible ('Cátedras USB' por defecto)
#      CORREO_MODO_CONSOLA    '1' = NO envía: escribe el correo en el log del
#                             servidor. Solo para desarrollo sin SMTP; el
#                             código NO se muestra en pantalla ni siquiera así.
#
#  Qué proveedor usar es decisión del equipo (ver README, "Subversión v7.1").
#
#  Un correo que no sale NUNCA se ignora en silencio: 'enviar' lanza
#  CorreoNoConfigurado o ErrorEnvio y la ruta se lo dice a la persona.
# ==========================================================================

import logging
import smtplib
import ssl
from email.message import EmailMessage
from email.utils import formataddr

from flask import current_app

registro = logging.getLogger(__name__)


class CorreoNoConfigurado(Exception):
    """No hay SMTP_HOST (ni modo consola): el servicio de correo está apagado."""


class ErrorEnvio(Exception):
    """El servidor SMTP rechazó el mensaje o no se pudo contactar."""


def configurado():
    """¿Puede salir un correo? (SMTP configurado o modo consola)."""
    cfg = current_app.config
    return bool(cfg.get('SMTP_HOST')) or bool(cfg.get('CORREO_MODO_CONSOLA'))


def enviar(destino, asunto, texto):
    """Envía un correo de texto plano. Lanza CorreoNoConfigurado / ErrorEnvio."""
    cfg = current_app.config

    remitente = cfg.get('SMTP_REMITENTE') or cfg.get('SMTP_USUARIO')
    mensaje = EmailMessage()
    mensaje['Subject'] = asunto
    mensaje['To'] = destino
    mensaje['From'] = formataddr((cfg.get('SMTP_NOMBRE_REMITENTE') or 'Cátedras USB',
                                  remitente or 'no-responder@localhost'))
    mensaje.set_content(texto)

    if cfg.get('CORREO_MODO_CONSOLA'):
        registro.warning('CORREO_MODO_CONSOLA activo: no se envía, solo se muestra aquí.\n%s', mensaje)
        return

    servidor = cfg.get('SMTP_HOST')
    if not servidor:
        raise CorreoNoConfigurado(
            'El servicio de correo no está configurado (falta SMTP_HOST). Ver README, "Subversión v7.1".')
    if not remitente:
        raise CorreoNoConfigurado('Falta SMTP_REMITENTE (o SMTP_USUARIO) para el campo "From".')

    puerto = int(cfg.get('SMTP_PORT') or 587)
    seguridad = (cfg.get('SMTP_SEGURIDAD') or 'starttls').lower()
    contexto = ssl.create_default_context()

    try:
        if seguridad == 'ssl':
            conexion = smtplib.SMTP_SSL(servidor, puerto, timeout=15, context=contexto)
        else:
            conexion = smtplib.SMTP(servidor, puerto, timeout=15)
        with conexion:
            if seguridad == 'starttls':
                conexion.starttls(context=contexto)
            if cfg.get('SMTP_USUARIO'):
                conexion.login(cfg['SMTP_USUARIO'], cfg.get('SMTP_CLAVE') or '')
            conexion.send_message(mensaje)
    except (smtplib.SMTPException, OSError) as ex:
        # El texto del error del servidor puede traer el correo del destinatario;
        # se registra el tipo y el servidor, no el cuerpo del mensaje (el código).
        registro.error('Falló el envío SMTP a %s:%s (%s)', servidor, puerto, type(ex).__name__)
        raise ErrorEnvio(f'No se pudo enviar el correo ({type(ex).__name__}).') from ex


def enviar_codigo_recuperacion(destino, usuario, codigo, minutos):
    """El correo del código de un solo uso para recuperar la clave."""
    enviar(
        destino,
        'Código para recuperar tu clave · Cátedras USB',
        f"""Hola,

Pediste recuperar la clave de la cuenta «{usuario}» en el sistema de Cátedras USB.

Tu código de un solo uso es:

    {codigo}

Vence en {minutos} minutos y solo sirve una vez. Escríbelo en la página de recuperación junto con tu clave nueva.

Si no fuiste tú, ignora este mensaje: tu clave actual sigue igual y nadie puede cambiarla sin este código.

Cátedras USB
""")
