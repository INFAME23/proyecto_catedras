# Plan técnico — Versión 11: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

- **Front:** se agrega `openpyxl` a `front_catedras/requirements.txt`, con la
  versión fija, para leer y escribir `.xlsx`. El CSV usa el módulo `csv` de Python.
- **Docker:** se agrega un **4.º servicio, `cron`** (D68): `alpine` con `curl` y
  `crond` de BusyBox. No usa una imagen propia; basta con un `command`.

## 2. Archivos

```
db/postgres/migraciones/
  064_exportacion_asis.sql        parámetros ASIS_COLUMNAS, ASIS_ENCABEZADO, HORA_CIERRE_JORNADA;
                                  sp_exportar_asis_sesion, sp_exportar_asis_lote, sp_exportar_asistencia, sp_ficha_asis
  065_jornada_e_importacion.sql   sp_generar_archivos_jornada, sp_importar_asistencia, fn_dias_habiles
  066_reportes.sql                sp_reporte_ponentes, sp_estadistica_impacto

api_catedras/
  Controllers/ExportacionController.cs    NUEVO (asis, asis/lote, asistencia, ponentes, ficha, jornada)
  Controllers/ImportacionController.cs    NUEVO
  Controllers/EstadisticaController.cs    NUEVO
  (servicios, repositorios y peticiones de los tres)

front_catedras/
  servicios/archivos_planos.py    NUEVO: arma el CSV o el XLSX a partir de {columnas, filas}; lee el archivo de importación
  routes/admin.py                 /admin/archivos, /admin/importar-asistencia, /admin/reportes/ponentes, /admin/estadisticas
  templates/admin/archivos.html, importar_asistencia.html, reporte_ponentes.html, estadisticas.html   NUEVAS
  templates/admin/sesion_detalle.html   + descargas y ficha ASIS

docker-compose.yml                 + servicio cron y montajes z064_ … z066_
```

## 3. Decisiones clave

1. **La BD arma las filas y el front arma el archivo (D81).** La API devuelve
   `{columnas, filas}`. Escribir un CSV o un XLSX es presentación, así que va en
   el front. Lo mismo hace la v3 con la carga de asistentes.
2. **La jornada reutiliza la v7.** No se reimplementa la generación del lote
   (D34 de la v7): `sp_generar_archivos_jornada` llama a
   `sp_generar_lote_migracion` por cada sesión. El error de cada sesión se
   atrapa con un `BEGIN … EXCEPTION` interno, para que una sesión no tumbe a las
   demás.
3. **El `cron` llama a la API directamente** (`http://api:8042/api/Exportacion/jornada`).
   No pasa por el front y no necesita credenciales: la API no tiene autorización
   por rol (D28 de la v5), y el endpoint es idempotente.
4. **Los archivos no se guardan en disco:** se generan al descargarlos, a partir
   de la BD. No hay volumen nuevo ni archivos huérfanos.

## 4. Servicio `cron` (forma, no definitiva)

```yaml
  cron:
    image: alpine:3.20
    container_name: mi_v1_catedras_cron
    restart: unless-stopped
    depends_on: [api]
    environment:
      TZ: America/Bogota
    command: >
      sh -c "apk add --no-cache curl tzdata >/dev/null &&
             echo '0 21 * * * curl -s -X POST -H \"Content-Type: application/json\" -d {} http://api:8042/api/Exportacion/jornada' > /etc/crontabs/root &&
             crond -f -l 8"
```

La hora queda fija en el `crontab` (21:00). El parámetro `HORA_CIERRE_JORNADA`
documenta esa hora y sirve para calcular el plazo. Si el equipo quiere que el
parámetro mande, el `cron` corre cada 15 minutos y el procedimiento decide si ya
pasó la hora (D82).

## 5. Frontend

```
GET  /admin/archivos?fecha=           lotes del día + "Generar archivos del día" + plazos
GET  /admin/archivos/asis/<sesion>.<csv|xlsx>
GET  /admin/archivos/lote/<lote>.<csv|xlsx>
GET  /admin/archivos/asistencia/<sesion>.<csv|xlsx>
GET  /admin/importar-asistencia       formulario (sesión + archivo)
POST /admin/importar-asistencia       lee el archivo -> POST /api/Importacion/asistencia -> informe
GET  /admin/reportes/ponentes?anio=&soloExternos=   tabla + descarga
GET  /admin/estadisticas?anio=        tablas y barras
```
