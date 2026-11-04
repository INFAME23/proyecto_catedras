# Quickstart — Versión 11: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Levantar

**BD recién creada:**

```powershell
docker compose down -v
docker compose up -d --build
docker compose ps        # 4 servicios: postgres, api, front, cron
```

**Volumen ya creado:** aplica las migraciones 064 a 066 a mano, después de la v10.

```bash
for n in 064_exportacion_asis 065_jornada_e_importacion 066_reportes; do
  docker compose cp db/postgres/migraciones/$n.sql postgres:/tmp/m.sql
  docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/m.sql
done
```

## 2. Verificación de la API (automatizada en `pruebas/verificar_v11.ps1`)

1. `GET /api/Exportacion/asis/5712` → 200 con `columnas: ["programa", "id_asis", "cedula"]`
   y tantas filas como `fn_exportar_asis(5712)`.
2. `PATCH /api/Parametro/ASIS_COLUMNAS` a `reunion,id_asis,programa` y repetir el
   paso 1 → las columnas cambian. Con `programa,xyz` → 500 con la lista de las válidas.
3. `GET /api/Exportacion/asistencia/5712` → incluye al externo (1 fila con `EXTERNO`
   o el tipo que tenga).
4. Deja una sesión con `fin` de hoy ya pasada y con registros; `PATCH` su estado
   a `CERRADA` y llama a `POST /api/Exportacion/jornada` → aparece en `generados`.
   - Repetir la llamada → ya no aparece: la sesión quedó `MIGRADA`.
5. `POST /api/Importacion/asistencia` con 3 filas: 2 IDs reales y 1 inventado →
   `importadas: 2`, con 1 error.
6. `GET /api/Exportacion/ponentes?anio=2027` y `GET /api/Estadistica/impacto?anio=2027` → 200.

## 3. Front, a mano

1. En `/admin/archivos`, «Generar archivos del día»; descarga el CSV y ábrelo en
   Excel: las tildes se ven bien y las columnas están separadas.
2. Descarga el `.xlsx` del mismo lote.
3. `/admin/importar-asistencia`: sube un CSV de prueba y lee el informe.
4. `/admin/estadisticas`: elige el año.

## 4. Probar el `cron` sin esperar a las 21:00

```bash
docker compose exec cron sh -c "curl -s -X POST -H 'Content-Type: application/json' -d '{}' http://api:8042/api/Exportacion/jornada"
docker compose logs cron
```

## 5. Problemas frecuentes

| Síntoma | Causa |
|---|---|
| El CSV se ve con caracteres raros en Excel | Falta el BOM (`utf-8-sig`) |
| El `cron` corre a las 16:00 | Falta `TZ` o `tzdata` en el contenedor |
| `ModuleNotFoundError: openpyxl` | Reconstruye el front: `docker compose up -d --build front` |
