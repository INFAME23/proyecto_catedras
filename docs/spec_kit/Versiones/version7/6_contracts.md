# Contratos de integración — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Alineado con el
> código real en la Fase 54 (D37).
>
> **Documento 6 de 8** del spec kit. Los endpoints HTTP están en
> [2_spec.md](2_spec.md) (RF179-RF197) y se navegan en vivo desde
> `/swagger`.
>
> **Nota de alcance (v7):** sin cambios de infraestructura. Las 4
> migraciones de procedimientos de la v7 (`013`, `014`, `031`, `032`) ya
> están montadas en `docker-entrypoint-initdb.d/` desde el retrofit del
> criterio 3 (D34 enmendado). Es la última versión del mapa — al
> cerrarla, este documento (y los 6 anteriores de esta carpeta)
> describen el contrato final de las 37 tablas de `bdcatedras`.

---

## 1. Mapa de puertos

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §1.

## 2. Variables de entorno

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §2.

## 3. Contrato de la API (lo que crece en v7)

**Convenciones (D37-B):** cuerpos y respuestas en snake_case; parámetros
de consulta en camelCase. Lecturas de lista: `200 {tabla, limite, total,
datos}` o `204` sin filas. Errores: `{estado, mensaje, detalle}` (+
`errores:[…]` en el 422). `PATCH`/`PUT` responden `200
{filasAfectadas}`; `DELETE`, `200 {filasEliminadas}`. Verbos no
declarados sobre una ruta que existe → **405** de ASP.NET Core
(comprobado en la Fase 59).

### 3.1 `LoteMigracion` — 4 endpoints

| Verbo y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /api/LoteMigracion?fkSesion=&limite=` | — | 200 lista (más reciente primero) · 204 · 400 (`limite` ≤ 0) |
| `GET /api/LoteMigracion/{idLoteMigracion}` | — | 200 lote · 404 |
| `POST /api/LoteMigracion` | **LoteMigracionGenerar** `{"fk_sesion": 5712, "fk_usuario": null}` | 200 `{estado: "creado", mensaje, id_lote_migracion, lote}` · 422 (falta `fk_sesion`) · 500 (sin registros migrables o sesión inexistente; FK de usuario) |
| `PATCH /api/LoteMigracion/{idLoteMigracion}` | **LoteMigracionActualizar** `{"fk_estado_proceso": "EN_CURSO", "observaciones": "…"}`, ambos opcionales | 200 · 400 (body vacío) · 404 · 422 · 500 (`fk_lotemig_estado`) |

**Sin** `PUT` ni `DELETE` (RF183) → 405. Generar deja la sesión en
`MIGRADA` (RF184). `instancia_proceso_asis` no se acepta en el `PATCH`
(D37-F).

Ejemplo de respuesta del `POST` (BD recién creada, sesión 5712 puesta
antes en `CERRADA`):

```json
{
  "estado": "creado",
  "mensaje": "Lote de migración 'ME_CATEDRA_000099001_R1.xlsx' generado con 166 filas.",
  "id_lote_migracion": 6,
  "lote": {
    "id_lote_migracion": 6, "fk_sesion": 5712,
    "nombre_proceso": "ME_MIGRACION_CATEDRA",
    "nombre_archivo": "ME_CATEDRA_000099001_R1.xlsx",
    "generado_en": "…+00:00", "fk_usuario": null,
    "instancia_proceso_asis": null, "fk_estado_proceso": "GENERADO",
    "total_filas": 166, "observaciones": null
  }
}
```

### 3.2 `DetalleMigracion` — 3 endpoints

| Verbo y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /api/DetalleMigracion?fkLoteMigracion=&limite=` | — | 200 lista (por lote y por id) · 204 · 400 |
| `GET /api/DetalleMigracion/{idDetalle}` | — | 200 · 404 |
| `PATCH /api/DetalleMigracion/{idDetalle}` | **DetalleMigracionActualizar** `{"resultado": "ACEPTADO", "mensaje": null}` | 200 · 400 (body vacío) · 404 · 422 (`resultado` fuera de `PENDIENTE`/`ACEPTADO`/`RECHAZADO`) · 500 (`ux_detmig_aceptado`) |

**Sin** `POST`, `PUT` ni `DELETE` (RF189) → 405. Un segundo `ACEPTADO`
para el mismo `fk_registro` en cualquier lote → 500, sin revalidar en C#
(D9).

### 3.3 `Bitacora` — 2 endpoints, ningún verbo de escritura (D36, D37-A)

| Verbo y ruta | Respuesta |
|---|---|
| `GET /api/Bitacora?nombreTabla=&limite=` | 200 lista (más reciente primero) · 204 · 400 |
| `GET /api/Bitacora/{idBitacora}` | 200 · 404 |

`POST`/`PUT`/`PATCH`/`DELETE` sobre `/api/Bitacora` o
`/api/Bitacora/{id}` → **405**: el controlador solo declara `[HttpGet]`.
Sin filtro por usuario (D37-C). `datos_antes`/`datos_despues` viajan
como JSON (no como texto).

### 3.4 `Parametro` — 6 endpoints, llave de texto

| Verbo y ruta | Cuerpo | Respuesta |
|---|---|---|
| `GET /api/Parametro?limite=` | — | 200 lista (por `clave`) · 204 · 400 |
| `GET /api/Parametro/{clave}` | — | 200 · 404 |
| `POST /api/Parametro` | **ParametroCrear** `{"clave": "PRUEBA_QUICKSTART", "valor": "1", "tipo_dato": "ENTERO", "descripcion": "Prueba v7", "fk_usuario_modifica": null}` | 200 `{estado: "creado", mensaje}` · 422 · 500 (`pk_parametro`, FK de usuario) |
| `PUT /api/Parametro/{clave}` | **ParametroReemplazo** `{"valor", "tipo_dato", "descripcion", "fk_usuario_modifica"?}` | 200 · 404 · 422 (falta un obligatorio) · 500 |
| `PATCH /api/Parametro/{clave}` | **ParametroActualizar** (todos opcionales) | 200 · 400 (body vacío) · 404 · 422 · 500 |
| `DELETE /api/Parametro/{clave}` | — | 200 `{filasEliminadas}` · 404 |

Reglas de forma (D37-D): `clave` obligatoria, ≤ 60 caracteres,
`^[A-Z][A-Z0-9_]*$`; `tipo_dato` de lista cerrada (`TEXTO`, `ENTERO`,
`BOOLEANO`, `FECHA`, `DECIMAL`), opcional en `POST` (default `TEXTO`) y
`PATCH`, obligatorio en `PUT`; `valor` admite cadena vacía. `clave`
nunca va en el body de `PUT`/`PATCH`. Ninguna FK entrante bloquea un
`DELETE` (RF196). `modificado_en` lo fija el SP (RF197).

### 3.5 Errores

Mismo sobre que v1-v6 — 422/400/404/500 (+ el 405 del framework para
verbos no declarados). **Sin 409** en esta versión: el cambio 500 → 409
para FK y `UNIQUE` es una decisión pendiente de todo el proyecto
(`ESTADO_V6_Y_CUMPLIMIENTO.md`), no de la v7 sola.

## 4. Contrato API ↔ BD

Mismo molde que el resto del proyecto desde el retrofit: cada método de
repositorio es un `CALL catedras.sp_<verbo>_<tabla>(…, NULL)` que
devuelve `json` en `INOUT p_resultado` (`NULL` = no existe). Ninguna
consulta SQL suelta en los repositorios de la v7.

| Repositorio | Procedimientos que llama | En la BD pero sin llamador |
|---|---|---|
| `RepositorioLoteMigracionPostgres` | `sp_listar_`, `sp_consultar_`, `sp_crear_` (→ `sp_generar_lote_migracion`), `sp_actualizar_lote_migracion` | `sp_eliminar_lote_migracion` |
| `RepositorioDetalleMigracionPostgres` | `sp_listar_`, `sp_consultar_`, `sp_actualizar_detalle_migracion` | `sp_crear_`, `sp_eliminar_detalle_migracion` |
| `RepositorioBitacoraPostgres` | `sp_listar_bitacora(p_nombre_tabla, p_limite)`, `sp_consultar_bitacora` | `sp_crear_`, `sp_actualizar_`, `sp_eliminar_bitacora` |
| `RepositorioParametroPostgres` | los 5 de `parametro` | — |

Provistos por `script_bdcatedras.sql` y **no modificados**:
`sp_generar_lote_migracion`, `fn_exportar_asis`, `fn_generar_lote_api`
(sin uso en la API), `fn_trg_bitacora` y los triggers
`trg_bitacora_registro`/`trg_bitacora_asistente`.

Tipos en los `CALL` (ajustes del retrofit): `reunion_enviada` entra como
`integer`; los `timestamptz` que devuelve `row_to_json` se normalizan a
UTC al leer.

## 5. Healthchecks y arranque

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §5.
`GET /` responde `version: "v7"` y `contratos:
"docs/spec_kit/Versiones/version7/6_contracts.md"` (Fase 61).

## 6. Contratos de volúmenes y montajes

Sin montajes nuevos en la Fase 54. Los de la v7 ya están en
`docker-compose.yml`:

| Archivo | Montado como |
|---|---|
| `db/postgres/migraciones/013_sp_parametro.sql` | `docker-entrypoint-initdb.d/z013_sp_parametro.sql` |
| `db/postgres/migraciones/014_sp_bitacora.sql` | `docker-entrypoint-initdb.d/z014_sp_bitacora.sql` |
| `db/postgres/migraciones/031_sp_lote_migracion.sql` | `docker-entrypoint-initdb.d/z031_sp_lote_migracion.sql` |
| `db/postgres/migraciones/032_sp_detalle_migracion.sql` | `docker-entrypoint-initdb.d/z032_sp_detalle_migracion.sql` |

`z031_` corre después del script provisto (que trae
`sp_generar_lote_migracion`), por el orden alfabético de initdb.

## 7. Devcontainer

Sin cambios: no existe en v7.

## 8. Credenciales

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §8.

## 9. Contrato del front (`front_catedras/`) con la API — lo que crece en v7

Todo dentro de `routes/admin.py`, bajo la guardia `before_request` de
`admin_bp` (rol `ADMIN`, D28; D37-E). Sin blueprints nuevos.

| Ruta del front | Llama a |
|---|---|
| `GET /admin/parametros` (+ crear, editar, eliminar) | `GET/POST/PUT/PATCH/DELETE /api/Parametro…`; `tipo_dato` por `<select>`; `clave` de solo lectura al editar |
| `GET /admin/lotes-migracion?fk_sesion=` | `GET /api/LoteMigracion?fkSesion=` |
| `GET /admin/lotes-migracion/<id>` (+ formularios de estado y de resultado por fila) | `GET /api/LoteMigracion/{id}`, `GET /api/DetalleMigracion?fkLoteMigracion=`, `PATCH /api/LoteMigracion/{id}` (`<select>` desde `GET /api/EstadoProceso`), `PATCH /api/DetalleMigracion/{id}` (`<select>` PENDIENTE/ACEPTADO/RECHAZADO) |
| `GET /admin/detalles-migracion?fk_lote_migracion=&resultado=` | `GET /api/DetalleMigracion?fkLoteMigracion=` (lista filtrable; se edita desde el detalle del lote) |
| `GET /admin/bitacora?tabla=&limite=` | `GET /api/Bitacora?nombreTabla=&limite=` (solo lectura) |
| `POST /admin/sesiones/<id>/migrar` | `POST /api/LoteMigracion {"fk_sesion": id}` y redirige al lote nuevo |

- `detalle_sesion.html` (v4) muestra "Generar migración" solo a un
  usuario `ADMIN`, en una sesión que ya terminó y tiene registros.
- `panel.html` gana 4 tarjetas.
- Los errores de la API se muestran en castellano; ninguna pantalla
  muestra "PUT", "PATCH", "422" ni "FK".
- El front sigue sin estar en `docker-compose.yml`; `API_BASE_URL`
  sigue sin cambios.

## 10. Reset completo

Sin cambios: [version1/6_contracts.md](../version1/6_contracts.md) §9.
`docker compose down -v && docker compose up -d --build` recrea las 37
tablas con sus datos de ejemplo — incluidas las 5 `lote_migracion`, 823
`detalle_migracion`, 168 `bitacora` y 9 `parametro` que v7 expone por
primera vez. Después, volver a fijar la clave del administrador
(`PATCH /api/Usuario/1 {"clave": "Admin12345"}`) para entrar al front.
