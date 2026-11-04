# Tareas — Versión 11: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md). Las fases
> continúan las de la [v10](../version10/8_tasks.md) (termina en la Fase 80).

## Fase 81 — Preparación
- [ ] La v8 y la v10 están fusionadas en `main` (la v9 puede estar o no).
- [ ] ¿Llegó el archivo de ejemplo de ASIS (P2)? Si sí, fijar `ASIS_COLUMNAS` con él;
      si no, dejar el valor inicial.
- [ ] Confirmar en la BD:
  - [ ] la salida de `fn_exportar_asis` para la sesión 5712;
  - [ ] las columnas de `detalle_migracion`;
  - [ ] las columnas de `ponencia`.
- [ ] Decidir D82 (crontab fijo o parámetro que manda).

## Fase 82 — Migración 064: exportación
- [ ] Los parámetros y los 4 procedimientos de exportación.
- [ ] `prueba_sp_exportacion_v11.sql`:
  - [ ] las columnas cambian con el parámetro;
  - [ ] una columna inválida se rechaza;
  - [ ] el externo no entra al ASIS pero sí al archivo general.

## Fase 83 — Migración 065: jornada e importación
- [ ] `fn_dias_habiles`, `sp_generar_archivos_jornada` y `sp_importar_asistencia`.
- [ ] Pruebas SQL:
  - [ ] una sesión sin migrables queda en `omitidas` y no detiene a las demás;
  - [ ] la jornada es idempotente;
  - [ ] la importación deja sus registros con `origen = 'IMPORTADO'`;
  - [ ] el duplicado de la importación se cuenta como `ya_registradas`.

## Fase 84 — Migración 066 y API
- [ ] `sp_reporte_ponentes` y `sp_estadistica_impacto`.
- [ ] Los controladores `Exportacion`, `Importacion` y `Estadistica`, con sus capas.
- [ ] Prueba de capas: bloque "V11 · Archivos y reportes".

## Fase 85 — Front
- [ ] `openpyxl` en `requirements.txt` (versión fija) y `servicios/archivos_planos.py`.
- [ ] `/admin/archivos`, las descargas, la importación, los ponentes, las
      estadísticas y la ficha ASIS en el detalle de la sesión.

## Fase 86 — Servicio `cron`
- [ ] El servicio en `docker-compose.yml` (3_plan.md §4).
- [ ] Verificar el disparo manual (7_quickstart.md §4) y la zona horaria.

## Fase 87 — Cierre de la v11 y del plan V8–V11
- [ ] `pruebas/verificar_v11.ps1` y `correr_todo_v11.ps1` en verde.
- [ ] README: sección "Versión 11", con el servicio `cron`.
- [ ] `0_mapa_versiones.md`: filas V8–V11 completas.
- [ ] Banner fuera, tag `v11` y PR a `main`.
