# Especificación — Versión 7 del proyecto: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR.** La v6 ya está cerrada con su tag `v6`. En la **Fase 54**
> (2026-10-15) este spec kit se revisó contra el código y la BD reales
> y se corrigió donde no coincidía (ver ADR **D37** de
> [4_research.md](4_research.md)). Fases 55-61 hechas el 2026-10-15 y
> verificadas contra PostgreSQL 16 real y la API real por HTTP (ver
> [8_tasks.md](8_tasks.md)); **falta una sola cosa**: correr
> `api_catedras/pruebas/correr_todo_v7.ps1` contra Docker. Si pasa, se
> quita este banner (en los 7 documentos) y se crea el tag `v7`.
>
> Rige la constitución del proyecto:
> [../../1_constitution.md](../../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md). Mapa completo de
> versiones: [../0_mapa_versiones.md](../0_mapa_versiones.md). Guía de
> esta versión: [guia_ia_v7.md](guia_ia_v7.md). **V7 es la última fila
> del mapa** — al cerrarla, las 37 tablas de `bdcatedras` quedan
> expuestas por completo.
>
> **Reutilizar antes que diseñar (ADR D34).** Generar un lote de
> migración junto con todas sus filas de detalle es una operación
> maestro-detalle, pero **no se diseña un SP nuevo para ella**:
> `script_bdcatedras.sql` ya trae `sp_generar_lote_migracion`, apoyado
> en `fn_exportar_asis`. Lo que sí existe son los SP por verbo del
> retrofit del criterio 3 (`013`, `014`, `031`, `032`): el de crear
> lote (`sp_crear_lote_migracion`) **llama** al provisto, no lo
> reimplementa.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v7 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API y los SP que llaman |
> | [4_research.md](4_research.md) | Decisiones y alternativas (D34-D37) |
> | [5_data_model.md](5_data_model.md) | Las 4 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v7 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |
> | [guia_ia_v7.md](guia_ia_v7.md) | Guía para trabajar la v7 con un asistente de IA |

---

## 1. Propósito de la v7

V1-V6 dejaron catálogos, estructura académica, personas, sesiones,
login/registro de asistencia y encuestas — todo el ciclo operativo de
una cátedra abierta. V7 cierra el proyecto con lo que una
administración real necesita alrededor de ese ciclo: **exportar** la
asistencia ya registrada al sistema institucional ASIS
(`lote_migracion`, `detalle_migracion`), **auditar** qué cambió
(`bitacora`, de solo lectura) y **configurar** los parámetros que ya
venían usando, sin que la API lo supiera, varias funciones desde v5
(`parametro`).

```
sesion (v4) ──< lote_migracion >──┬── estado_proceso (v1)
                     │             └── usuario (v5, opcional)
                     └──< detalle_migracion >── registro_asistencia (v5)

bitacora >── usuario (v5, opcional) — SOLO LECTURA en la API;
             la llenan trg_bitacora_registro y trg_bitacora_asistente
parametro >── usuario (v5, opcional) — tabla plana, sin FK entrantes
```

La única operación maestro-detalle de esta versión —generar un lote de
migración junto con todas sus filas de detalle— la resuelve
`sp_generar_lote_migracion` (provisto), que, además de insertar el
maestro y los detalles, **actualiza por su cuenta el `estado` de la
`Sesion`** a `MIGRADA` (ver RF184) — un efecto colateral real sobre una
tabla de v4, documentado aquí para que no se lea como un error.

## 2. Alcance

**Incluye:**
- `LoteMigracion`: lectura completa + **generar** (`POST`, que hace
  `CALL sp_crear_lote_migracion` → `CALL sp_generar_lote_migracion`) +
  **actualizar estado** (`PATCH`, por `sp_actualizar_lote_migracion`:
  el estado de un lote avanza con el tiempo según lo que responda ASIS,
  a diferencia de `LoteCargaAsistente` en v3, que es un registro de un
  solo uso — D35). Sin `PUT` ni `DELETE`.
- `DetalleMigracion`: lectura + **actualizar resultado** (`PATCH`, por
  `sp_actualizar_detalle_migracion`). Sin `POST` propio (nace dentro de
  "generar"), sin `PUT` ni `DELETE`.
- `Bitacora`: **solo lectura** (RF190-RF191, D36, D37-A) — la llenan los
  triggers de la BD; ningún verbo de escritura en la API, aunque los 5
  SP de `014` existan en la base.
- `Parametro`: CRUD completo (6 verbos) con llave de texto (`clave`),
  igual patrón que `Sede`/`Facultad` — la única tabla nueva de v7 que sí
  es un CRUD tradicional de principio a fin.

**Convenciones que no cambian (D37-B):** cuerpos y respuestas en
snake_case con `[JsonPropertyName]` (`fk_sesion`, `fk_estado_proceso`,
`tipo_dato`), igual que v1-v6; parámetros de consulta en camelCase
(`?fkSesion=`, `?fkLoteMigracion=`, `?nombreTabla=`). Códigos: 422
forma · 400 regla del servicio o `PATCH` vacío · 404 no existe · 500
todo rechazo de la BD. **Sin 409** (decisión pendiente de todo el
proyecto, no de la v7).

**No incluye:** ninguna tabla — v7 es la última fila del mapa de
versiones. Tampoco: integración real con ASIS, usuario autenticado en la
bitácora ni autorización por rol en la API (D28 de v5).

## 3. Entidades y requisitos funcionales

Numeración de RF continúa desde v6 (terminó en RF178).

### LoteMigracion

> RF179-RF185. `fk_usuario` ya no tiene la salvedad de FK circular (v5
> cerró esa posibilidad): es opcional porque no hay autorización en la
> API (D28 de v5), no por una limitación técnica.

- **RF179 — Listar** (`GET /api/LoteMigracion?fkSesion=&limite=`): sin
  el filtro, todos hasta el límite (por defecto 1000), del más reciente
  al más antiguo. Sin filas → 204. `limite` ≤ 0 → 400.
- **RF180 — Obtener por id** (`GET .../{idLoteMigracion}`): 200;
  inexistente → 404.
- **RF181 — Generar** (`POST /api/LoteMigracion` con
  **LoteMigracionGenerar**: `fk_sesion` obligatorio; `fk_usuario`
  opcional): `CALL sp_crear_lote_migracion`, que llama a
  `sp_generar_lote_migracion` y devuelve la fila completa. Respuesta
  `200 {estado, mensaje, id_lote_migracion, lote}`. Una sesión sin
  ningún registro migrable (`RN-08`/`RN-28`: sin `id_asis`, sin
  programa, o `es_externo`), o que no existe → **500** ("No hay
  registros migrables en la sesion N…", `check_violation`; D25 de v5,
  sin cambios). `fk_sesion` ausente → 422.
- **RF182 — Actualizar estado** (`PATCH /api/LoteMigracion/{idLoteMigracion}`
  con **LoteMigracionActualizar**: `fk_estado_proceso` y/o
  `observaciones`, ambos opcionales): el repositorio consulta la fila,
  mezcla lo enviado y llama a `sp_actualizar_lote_migracion` (molde de
  `sede`). Un lote de migración SÍ cambia de estado varias veces con el
  tiempo (`GENERADO` → `EN_COLA` → `EN_CURSO` → `CORRECTO`/`ERROR`/
  `INCORRECTO`, según lo que responda ASIS) — D35. Un
  `fk_estado_proceso` inexistente → 500 (`fk_lotemig_estado`, D9). Body
  vacío → 400. Lote inexistente → 404. `instancia_proceso_asis` no se
  expone (D37-F).
- **RF183 — Sin `PUT` ni `DELETE`:** `nombre_archivo`, `fk_sesion` y
  `total_filas` no se editan nunca (son el hecho de cuándo y sobre qué
  sesión se generó); borrar un lote borraría evidencia de un intento de
  migración real. `sp_eliminar_lote_migracion` existe en la BD y no se
  expone. Esas rutas responden **405** de ASP.NET Core (la ruta existe
  con otros verbos; comprobado en la Fase 59).
- **RF184 — Efecto colateral documentado: `Sesion.estado` pasa a
  `MIGRADA`:** `sp_generar_lote_migracion` hace, dentro del mismo
  `CALL`, `UPDATE sesion SET estado = 'MIGRADA'`. El servicio no toca
  `Sesion` (ni siquiera recibe su repositorio). La respuesta del `POST`
  no lo menciona: se documenta aquí y en el smoke test.
- **RF185 — Quién entra al archivo de migración (`RN-08`/`RN-28`, no
  revalidado en la API):** solo los registros cuyo asistente tiene
  `id_asis`, cuyo registro tiene programa y que **no** son externos.
  `total_filas` es el `count(*)` de `fn_exportar_asis`, así que
  **excluye** a los externos igual que `detalle_migracion`: en la sesión
  5712 hay 168 registros, 1 externo, y **166** migrables = `total_filas`
  = filas de detalle. Regla de negocio de la BD, D9.

### DetalleMigracion

> RF186-RF189. Nace exclusivamente dentro de RF181.

- **RF186 — Listar** (`GET /api/DetalleMigracion?fkLoteMigracion=&limite=`):
  sin el filtro, todas hasta el límite (por defecto 1000), ordenadas por
  lote y por id. Sin filas → 204. `limite` ≤ 0 → 400.
- **RF187 — Obtener por id** (`GET .../{idDetalle}`): 200; inexistente
  → 404.
- **RF188 — Actualizar resultado** (`PATCH .../{idDetalle}` con
  **DetalleMigracionActualizar**: `resultado` — lista cerrada
  `PENDIENTE`/`ACEPTADO`/`RECHAZADO`, otro valor → 422 —, `mensaje`
  opcional): simula la confirmación que, en un entorno real, llegaría
  de vuelta desde ASIS. Pasa por `sp_actualizar_detalle_migracion`.
  Marcar `ACEPTADO` una fila cuyo `fk_registro` ya tiene **otra** fila
  `ACEPTADO` en cualquier lote → 500 (`ux_detmig_aceptado` — un
  registro de asistencia no se migra exitosamente dos veces, aunque se
  haya intentado en lotes distintos). Body vacío → 400.
- **RF189 — Sin `POST` propio, `PUT` ni `DELETE`:** mismo criterio
  que `NovedadCarga` (v3) y `RespuestaItem` (v6) — nace solo dentro de
  otra operación; a diferencia de esas dos, aquí SÍ hay un `PATCH`
  (RF188), porque esta fila SÍ cambia de estado después de nacer.
  `sp_crear_detalle_migracion` y `sp_eliminar_detalle_migracion` existen
  en la BD y no se exponen. Esas rutas responden **405** (comprobado en
  la Fase 59).

### Bitacora

> RF190-RF191. Solo lectura — la llena `fn_trg_bitacora` a través de
> **dos** triggers: `trg_bitacora_registro` (`AFTER INSERT OR UPDATE OR
> DELETE` sobre `registro_asistencia`) y `trg_bitacora_asistente`
> (`AFTER UPDATE OR DELETE` sobre `asistente`). Ningún endpoint de otra
> entidad necesita cambiar para que la bitácora se siga llenando. Como
> el borrado de `Asistente` es lógico desde el retrofit, cada `PUT`,
> `PATCH` o `DELETE` de `/api/Asistente/{id}` agrega una fila
> `asistente / UPDATE`.

- **RF190 — Listar** (`GET /api/Bitacora?nombreTabla=&limite=`): filtro
  opcional por tabla; sin él, todas hasta el límite (por defecto 1000),
  de la más reciente a la más antigua. Sin filas → 204. `limite` ≤ 0 →
  400. **Sin filtro por usuario** (D37-C): `fk_usuario` es `NULL` en
  todas las filas porque ningún endpoint propaga el usuario autenticado
  a la conexión de PostgreSQL.
- **RF191 — Obtener por id** (`GET .../{idBitacora}`): 200;
  inexistente → 404. **Sin ningún verbo de escritura** (D36, D37-A): ni
  `POST`, ni `PUT`, ni `PATCH`, ni `DELETE` — la API no decide nunca qué
  entra a la bitácora. Los SP `sp_crear_`/`sp_actualizar_`/
  `sp_eliminar_bitacora` existen en la BD (criterio 3) y no tienen
  llamador. Esas rutas responden **405** (comprobado en la Fase 59).

### Parametro

> RF192-RF197. CRUD completo, llave de texto — como `Sede`, con una PK
> que el cliente elige al crear, en vez de una que la BD genera.

- **RF192 — Listar** (`GET /api/Parametro?limite=`): 200, ordenados por
  `clave`; vacía → 204 (en la práctica nunca: hay 9 parámetros de
  ejemplo que varias funciones de v5 ya leen con `COALESCE` y un valor
  por defecto si la fila faltara).
- **RF193 — Obtener por clave** (`GET /api/Parametro/{clave}`): 200;
  inexistente → 404.
- **RF194 — Crear** (`POST /api/Parametro` con **ParametroCrear**):
  `clave` obligatoria, hasta 60 caracteres y con
  `[RegularExpression("^[A-Z][A-Z0-9_]*$")]` (D37-D: mayúsculas, dígitos
  y guion bajo, empezando por letra, como las 9 sembradas); `valor`
  obligatorio (admite cadena vacía); `descripcion` obligatoria;
  `tipo_dato` **opcional**, de lista cerrada (`TEXTO`, `ENTERO`,
  `BOOLEANO`, `FECHA`, `DECIMAL`), `TEXTO` si no llega;
  `fk_usuario_modifica` opcional. Forma inválida → 422. Respuesta
  `200 {estado, mensaje}`. `clave` duplicada → 500 (`pk_parametro`). No
  se valida que `valor` sea coherente con `tipo_dato` (D37-D).
- **RF195 — Reemplazar/Actualizar** (`PUT`/`PATCH .../{clave}`):
  igual molde que siempre, pero **`clave` nunca va en el body** (es la
  llave de la ruta). `PUT` exige `valor`, `tipo_dato` y `descripcion`
  (falta uno → 422; `fk_usuario_modifica` ausente queda `NULL`). `PATCH`
  cambia solo lo enviado; body vacío → 400. Inexistente → 404. Ambos
  pasan por `sp_actualizar_parametro`, que fija `modificado_en = now()`
  (RF197).
- **RF196 — Eliminar** (`DELETE .../{clave}`): `200 {filasEliminadas}`;
  inexistente → 404. `sp_eliminar_parametro` es un `DELETE` físico (la
  tabla no tiene `activo`). Sin ninguna FK entrante — borrar un
  parámetro es **seguro**: cada función que lo lee (todas de v5) ya
  tiene un valor de reserva con `COALESCE` si la fila no existe (D9,
  llevado a su forma más benigna).
- **RF197 — `modificado_en` no tiene trigger propio:** a diferencia de
  `creado_en` en el resto del proyecto (que un `DEFAULT now()` fija
  solo al insertar), `parametro.modificado_en` no se actualiza sola en
  cada `UPDATE`. Lo fija **`sp_actualizar_parametro`** (`modificado_en
  = now()` dentro del SP), así que el repositorio no duplica el `now()`
  en C#.

## 4. Requisitos no funcionales

Rigen los mismos RNF1-RNF15 de v1-v6, sin cambios. No se añade ninguno
nuevo: v7 no introduce ningún patrón de validación, seguridad ni
infraestructura distinto de lo que ya existía.

## 5. Criterios de aceptación

Sobre un entorno recién reseteado (`docker compose down -v && docker
compose up -d --build`). Datos verificados en el §8 de
[guia_ia_v7.md](guia_ia_v7.md).

1. `docker compose up -d --build` deja los **37** recursos (33 de
   v1-v6 + 4 de v7) respondiendo en `/swagger` — las 37 tablas de
   `bdcatedras` quedan expuestas por completo. `GET /` dice
   `version: "v7"`.
2. `PATCH /api/Sesion/5712 {"estado": "CERRADA"}` (la sesión ya viene
   `MIGRADA` en los datos sembrados). Después, `POST /api/LoteMigracion`
   con `{"fk_sesion": 5712}` → 200 con `id_lote_migracion` (6 en una BD
   recién creada) y `lote.total_filas: 166`; `GET
   /api/DetalleMigracion?fkLoteMigracion=6` → 166 filas `PENDIENTE` (el
   externo no aparece); `GET /api/Sesion/5712` → `estado: "MIGRADA"`
   (RF184). `POST` con `{"fk_sesion": 1}` → 500 "No hay registros
   migrables".
3. `PATCH /api/LoteMigracion/6` con `{"fk_estado_proceso": "EN_CURSO"}`
   → 200; con `"NO_EXISTE"` → 500; con `{}` → 400.
4. `PATCH /api/DetalleMigracion/{idDetalle}` con `{"resultado":
   "ACEPTADO"}` sobre la fila del lote 6 cuyo `fk_registro` es **167** →
   200; sobre la fila del lote 6 cuyo `fk_registro` es **1** → 500
   (`ux_detmig_aceptado`: ya está `ACEPTADO` en el lote 1 sembrado).
5. `GET /api/Bitacora?limite=5` → 5 de las 168 filas de ejemplo; `DELETE
   /api/Asistente/2` agrega una fila `asistente / UPDATE`; `POST`/`PUT`/
   `PATCH`/`DELETE` sobre `/api/Bitacora` o `/api/Bitacora/{id}` → 405
   (comprobado en la Fase 59).
6. `POST /api/Parametro` con una `clave` nueva → 200; repetirlo → 500;
   `PATCH /api/Parametro/{clave}` cambia `valor` y `modificado_en`
   avanza; `DELETE /api/Parametro/{clave}` → 200 y un segundo `DELETE`
   → 404.
7. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las **37** entidades sin PostgreSQL, incluida la verificación por
   reflexión de que `Bitacora` no tiene métodos de escritura. Código de
   salida 0 y más de las 385 pasadas de la v6 (Fase 58: 452).
8. El panel de administración (`/admin`) muestra 4 tarjetas nuevas:
   lotes de migración (lista y estado), detalle de migración (resultado
   por fila), bitácora (solo lectura, filtro por tabla) y parámetros
   (CRUD completo). En `detalle_sesion.html`, un `ADMIN` ve el botón
   "Generar migración" en una sesión terminada con registros; generar
   desde ahí deja la sesión como migrada sin tocar `Sesion` desde el
   front.
