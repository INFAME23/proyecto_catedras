# Contratos de integración — Versión 11: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).

## 1. Puertos y servicios

| Servicio | Puerto | Nuevo |
|---|---|---|
| `postgres` | 15542 → 5432 | — |
| `api` | 8142 → 8042 | — |
| `front` | 5000 | — |
| `cron` | ninguno (solo sale hacia `api:8042`) | **Sí** (D68) |

## 2. Endpoints nuevos

| Verbo y ruta | Cuerpo / consulta | Respuesta |
|---|---|---|
| `GET /api/Exportacion/asis/{idSesion}` | — | 200 `{nombre_archivo, columnas, encabezado, filas}` · 204 (sin filas migrables) · 404 · 500 (`ASIS_COLUMNAS` inválido) |
| `GET /api/Exportacion/asis/lote/{idLoteMigracion}` | — | 200 lo mismo · 404 |
| `GET /api/Exportacion/asistencia/{idSesion}` | — | 200 `{nombre_archivo, columnas, filas}` · 204 · 404 |
| `GET /api/Exportacion/ficha/{idSesion}` | — | 200 `{id_evento_asis, nombre_asis, tipo_evento, reunion, fecha, inicio, fin, sede, ponentes}` · 404 |
| `GET /api/Exportacion/ponentes?fkPeriodo=&anio=&soloExternos=` | — | 200 `{columnas, filas}` · 204 |
| `POST /api/Exportacion/jornada` | `{"fecha": "2027-02-15", "solo_si_paso_cierre": false, "fk_usuario": null}`, todo opcional | 200 `{fecha, generados, omitidas}` |
| `POST /api/Importacion/asistencia` | `{"fk_sesion": 5712, "filas": [{"id_asis": "000123456", "programa": "M0286", "cedula": "1020"}], "fk_usuario": 1}` | 200 `{importadas, ya_registradas, errores:[{fila, motivo}]}` · 404 (la sesión no existe) · 422 |
| `GET /api/Estadistica/impacto?anio=&fkPeriodo=` | — | 200 `{por_tipo, externos_por_procedencia, por_mes, catedras, sesiones, asistentes_unicos}` |

**Ejemplo** de `GET /api/Exportacion/asis/5712` con `ASIS_COLUMNAS = programa,id_asis,cedula`:

```json
{
  "nombre_archivo": "ME_CATEDRA_000099001_R1",
  "columnas": ["programa", "id_asis", "cedula"],
  "encabezado": true,
  "filas": [["M0286", "000123456", "1020456789"], ["M0301", "1000513C", ""]]
}
```

El front agrega la extensión (`.csv` o `.xlsx`). Los datos del ejemplo son
ilustrativos; se reemplazan en la Fase 81 por los reales de la sesión 5712.

## 3. API ↔ BD

| Método | SQL |
|---|---|
| Exportaciones | `CALL catedras.sp_exportar_asis_sesion(@idSesion, NULL);` · `sp_exportar_asis_lote` · `sp_exportar_asistencia` · `sp_ficha_asis` · `sp_reporte_ponentes` |
| Jornada | `CALL catedras.sp_generar_archivos_jornada(@fecha::date, @soloSiPasoCierre, @fkUsuario::bigint, NULL);` |
| Importación | `CALL catedras.sp_importar_asistencia(@fkSesion, @filas::jsonb, @fkUsuario::bigint, NULL);` |
| Estadística | `CALL catedras.sp_estadistica_impacto(@anio, @fkPeriodo, NULL);` |

## 4. Front ↔ API

| Ruta del front | Llama a |
|---|---|
| `/admin/archivos` | `GET /api/LoteMigracion?…` (v7) y `POST /api/Exportacion/jornada` |
| `/admin/archivos/asis/<s>.<ext>` y `/lote/<l>.<ext>` | `GET /api/Exportacion/asis/…` → `archivos_planos.escribir(ext, …)` |
| `/admin/archivos/asistencia/<s>.<ext>` | `GET /api/Exportacion/asistencia/{s}` |
| `/admin/importar-asistencia` | `archivos_planos.leer(…)` → `POST /api/Importacion/asistencia` |
| `/admin/reportes/ponentes` | `GET /api/Exportacion/ponentes` |
| `/admin/estadisticas` | `GET /api/Estadistica/impacto` |
| Detalle de la sesión | `GET /api/Exportacion/ficha/{id}` |

## 5. Montajes y servicio `cron`

```yaml
- ./db/postgres/migraciones/064_exportacion_asis.sql:/docker-entrypoint-initdb.d/z064_exportacion_asis.sql:ro
- ./db/postgres/migraciones/065_jornada_e_importacion.sql:/docker-entrypoint-initdb.d/z065_jornada_e_importacion.sql:ro
- ./db/postgres/migraciones/066_reportes.sql:/docker-entrypoint-initdb.d/z066_reportes.sql:ro
```

El servicio `cron` se define en [3_plan.md](3_plan.md) §4. No tiene variables
con secretos. `TZ=America/Bogota` hace que el 21:00 sea la hora local.
