# Guía IA — Versión 11: archivos planos, importación y reportes

> **Jerarquía:** [constitución](../../1_constitution.md) › [guía IA](../../guia_ia.md)
> › spec kit v11 › esta guía. Si el spec contradice la BD real, gana la BD.
> **Estado de partida:** v8 y v10 en `main`.

## 1. Prompt de arranque

```
Vas a trabajar la versión 11 del Proyecto Cátedras (archivos planos ASIS,
importación, reportes y estadísticas). Lee docs/spec_kit/1_constitution.md,
docs/spec_kit/guia_ia.md, docs/spec_kit/Versiones/plan_v8_v11.md y todo
docs/spec_kit/Versiones/version11/. Reglas: no edites script_bdcatedras.sql ni
fn_exportar_asis; migraciones 064-066; la jornada REUTILIZA
sp_generar_lote_migracion de la v7; la BD devuelve {columnas, filas} y el front
escribe el CSV/XLSX; JSON en snake_case; todo en español. No contradigas D65, D68
ni D81-D84. Trabaja solo la fase que te indique.
```

## 2. La v11 en una página

- **Archivo ASIS por sesión o por lote:**
  - las columnas salen de `ASIS_COLUMNAS` (`programa,id_asis,cedula` al inicio);
  - solo entran los estudiantes migrables, el mismo filtro de la v7.
- **Jornada:**
  - un 4.º servicio, `cron`, llama a `POST /api/Exportacion/jornada` a las 21:00;
  - genera un lote por cada sesión terminada del día y es idempotente.
- **Otros archivos y reportes:**
  - un archivo general con todos los asistentes;
  - la importación de CSV o Excel, con origen `IMPORTADO`;
  - el reporte de ponentes para certificados y SNIES;
  - la ficha ASIS de la sesión;
  - las estadísticas de impacto social.

## 3. Lo que la IA tiende a "arreglar" mal

| Tentación | Por qué no |
|---|---|
| Cambiar `fn_exportar_asis` para agregarle la cédula | Es del script (Artículo 5) y su contrato es de 3 columnas; la v11 tiene sus propios SP |
| Reimplementar la generación del lote | D34 de la v7: se llama a `sp_generar_lote_migracion` |
| Generar el XLSX en C# con un NuGet | D81: el archivo es presentación y va en el front |
| Guardar los archivos en disco o en un volumen | Se generan al descargarlos |
| "Arreglar" que la jornada deje la sesión `MIGRADA` | Es el efecto documentado de la v7 (RF184) y lo que hace idempotente la jornada |
| Poner credenciales en el `cron` | RNF-V11-2: la API no tiene autorización por rol y el endpoint es idempotente |
| Contar los tipos de usuario con la vinculación vigente de hoy | Se cuentan con `fk_vinculacion_snapshot`, el tipo en el momento del registro |
| Tumbar toda la importación por una fila mala | RF259: se reporta la fila y se sigue |

## 4. Prompts por fase

- **Fase 81:** «Fase 81 de version11/8_tasks.md. Sin código. Ejecuta fn_exportar_asis(5712) y muestra las columnas reales de detalle_migracion y de ponencia. Propón D82.»
- **Fase 82:** «Escribe 064_exportacion_asis.sql según 5_data_model.md §2 y su prueba SQL.»
- **Fase 83:** «Escribe 065_jornada_e_importacion.sql según §3. Cada sesión de la jornada va en su propio BEGIN…EXCEPTION.»
- **Fase 84:** «Escribe 066_reportes.sql y las capas de Exportacion, Importacion y Estadistica según 6_contracts.md.»
- **Fase 85:** «Front de la v11 según 3_plan.md §5. CSV en utf-8-sig con ';'; XLSX con openpyxl.»
- **Fase 86:** «Agrega el servicio cron a docker-compose.yml según 3_plan.md §4 y verifica el disparo manual.»
- **Fase 87:** «verificar_v11.ps1, README y mapa de versiones. No quites el banner ni crees el tag.»
