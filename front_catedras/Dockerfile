# ==========================================================================
#  Dockerfile · Front Cátedras (Flask)
# --------------------------------------------------------------------------
#  Imagen para el servicio 'front' del docker-compose.yml del proyecto.
#  Igual que 'api' usa 'dotnet watch' para recarga en caliente montando
#  './api_catedras:/app' como volumen, aquí el código también se monta
#  como volumen (ver docker-compose.yml) y esta imagen solo necesita las
#  dependencias instaladas. 'run.py' ya levanta Flask en modo debug
#  (host=0.0.0.0, port=5000), así que la recarga en caliente viene gratis.
# ==========================================================================

FROM python:3.12-slim

WORKDIR /app

# Copiamos primero solo requirements.txt para aprovechar la cache de Docker:
# si el código cambia pero las dependencias no, 'pip install' no se repite.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# El código real llega por el volumen en docker-compose.yml, pero lo
# copiamos también aquí para que la imagen funcione sola si el volumen
# no está montado (por ejemplo, en un despliegue sin hot-reload).
COPY . .

EXPOSE 5000

CMD ["python", "run.py"]
