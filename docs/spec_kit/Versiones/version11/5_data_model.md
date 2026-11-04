# Modelo de datos — Versión 11: archivos planos, importación y reportes

> ⚠️ **BORRADOR · por construir** — ver nota de [2_spec.md](2_spec.md).
> Sin tablas nuevas. Las migraciones 064–066 traen parámetros y procedimientos,
> montados como `z064_` … `z066_`.

## 1. Lo que se reutiliza

| Objeto | Versión | Para qué |
|---|---|---|
| `sp_generar_lote_migracion`, `lote_migracion`, `detalle_migracion` | Script / v7 | La jornada genera lotes |
| `fn_exportar_asis` (sus filtros RN-08 y RN-28) | Script | Mismo criterio de "migrable" |
| `registro_asistencia` (fotos de vinculación, programa y facultad, geo) | v5 / v8 | Archivo general y estadísticas |
| `documento_asistente` (vigente) | v3 | Columna `cedula` |
| `asistente.procedencia`, `asistente.fk_facultad` | v8 (054) | Archivo general y estadísticas |
| `perfil_ponente`, `solicitud_catedra` | v10 (062) | Reporte de ponentes |
| `ponencia`, `catedra.id_evento_asis`, `catedra.nombre_asis` | v4 / v2 | Ficha ASIS |

## 2. Migración 064 — exportación

```sql
INSERT INTO catedras.parametro (clave, valor, tipo_dato, descripcion) VALUES
 ('ASIS_COLUMNAS',       'programa,id_asis,cedula', 'TEXTO',
  'v11. Columnas del archivo plano ASIS, en orden. Válidas: programa,id_asis,cedula,reunion,nombres,apellidos,correo'),
 ('ASIS_ENCABEZADO',     'true',  'BOOLEANO', 'v11. Si el archivo ASIS lleva la fila de títulos'),
 ('HORA_CIERRE_JORNADA', '21:00', 'TEXTO',    'v11. Hora en que se generan los archivos del día (formato HH:MM)')
ON CONFLICT (clave) DO NOTHING;
```

| Procedimiento | Devuelve (`INOUT p_json`) |
|---|---|
| `sp_exportar_asis_sesion(p_id_sesion, INOUT p_json)` | `{nombre_archivo, columnas:[…], encabezado:bool, filas:[[…]]}`. Valida `ASIS_COLUMNAS` contra la lista permitida |
| `sp_exportar_asis_lote(p_id_lote, INOUT p_json)` | Lo mismo, desde `detalle_migracion` (`id_asis_enviado`, `programa_enviado`, `reunion_enviada`) + la cédula |
| `sp_exportar_asistencia(p_id_sesion, INOUT p_json)` | `{nombre_archivo, columnas, filas}`, con todos los registros (RF256) |
| `sp_ficha_asis(p_id_sesion, INOUT p_json)` | `{id_evento_asis, nombre_asis, tipo_evento, reunion, fecha, inicio, fin, sede, ponentes:[…]}` |

El `nombre_archivo` sigue la convención de la v7:
`ME_CATEDRA_<id_evento_asis>_R<reunion>.csv` (o `.xlsx`, según lo que pida el front).

## 3. Migración 065 — jornada e importación

```sql
-- fn_dias_habiles(p_desde date, p_n int) RETURNS date          (D84: salta sábado y domingo)
-- sp_generar_archivos_jornada(p_fecha date, p_solo_si_paso_cierre boolean, p_usuario bigint, INOUT p_json)
--   FOR s IN sesiones con fin::date = p_fecha AND fin <= now() AND estado <> 'MIGRADA' LOOP
--     BEGIN CALL sp_generar_lote_migracion(s.id_sesion, p_usuario, v_lote); -> generados
--     EXCEPTION WHEN others THEN -> omitidas (SQLERRM) END;
--   devuelve {fecha, generados:[{id_sesion,id_lote,limite}], omitidas:[{id_sesion,motivo}]}
-- sp_importar_asistencia(p_fk_sesion bigint, p_filas jsonb, p_usuario bigint, INOUT p_json)
--   por fila: fn_resolver_asistente(id_asis o cedula); INSERT registro_asistencia (origen 'IMPORTADO',
--   sin enlace ni clave); 23505 (uq_registro_sesion_asis) -> ya_registradas; otros -> errores
```

Los registros `IMPORTADO` no tienen `fk_enlace`, así que `trg_registro_validar`
no valida la ventana (RN-15 solo aplica con enlace) y `trg_registro_inscripcion`
no actúa. La foto de la vinculación sí se toma: si la persona no tiene una
vinculación vigente, la fila va a `errores`.

## 4. Migración 066 — reportes

| Procedimiento | Devuelve |
|---|---|
| `sp_reporte_ponentes(p_fk_periodo, p_anio, p_solo_externos, INOUT p_json)` | `{columnas, filas}`: una por ponencia, con `perfil_ponente`, el documento vigente, la cátedra, la fecha y el `resumen_ponencia` de la solicitud aprobada |
| `sp_estadistica_impacto(p_anio, p_fk_periodo, INOUT p_json)` | `{por_tipo:[{tipo, total}], externos_por_procedencia:[{procedencia, total}], por_mes:[{mes, total}], catedras, sesiones, asistentes_unicos}` |

`por_tipo` se cuenta con `registro_asistencia.fk_vinculacion_snapshot`, que
refleja el tipo en el momento del registro.
