# ==========================================================================
#  Dockerfile  ·  api_catedras  ·  Proyecto Cátedras · Versión 1
# --------------------------------------------------------------------------
#  Imagen del SDK de .NET 10 (no la de runtime): trae el compilador, así
#  'dotnet watch' puede recompilar dentro del contenedor cuando guardas un
#  .cs (decisión D7). Una imagen "de producción" optimizada no enseña nada
#  en la v1 y rompería ese ciclo.
#
#  El CÓDIGO NO se copia aquí: llega montado como volumen desde
#  docker-compose.yml (./api_catedras -> /app). Este Dockerfile solo fija
#  la imagen, el directorio de trabajo y el comando de arranque.
# ==========================================================================

# Imagen base: SDK de .NET 10.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Carpeta de trabajo dentro del contenedor. El volumen del compose monta
# el proyecto justo aquí.
WORKDIR /app

# En bind-mounts (Windows -> contenedor Linux) los eventos de "archivo
# cambiado" no siempre llegan; el watcher por sondeo los detecta igual.
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

# La API escucha en el 8042 DENTRO del contenedor (el compose lo publica
# como 8142 en tu PC).
ENV ASPNETCORE_URLS=http://+:8042

# Entorno de desarrollo (mensajes de error detallados).
ENV ASPNETCORE_ENVIRONMENT=Development

# Documenta el puerto interno (no lo publica: eso lo hace el compose).
EXPOSE 8042

# Arranque: 'dotnet watch' vigila los .cs del proyecto y reinicia solo.
#   --project ApiCatedras.csproj  -> compila SOLO la API (no 'pruebas')
#   --non-interactive             -> no pregunta nada ante cambios grandes
CMD ["dotnet", "watch", "run", "--project", "ApiCatedras.csproj", "--non-interactive"]
