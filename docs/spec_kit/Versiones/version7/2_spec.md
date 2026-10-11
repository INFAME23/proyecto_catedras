# Especificación — Versión 7 del proyecto: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR.** Escrito mientras V2-V6 todavía no tienen su tag. El
> Artículo 1 de la constitución dice que la spec siguiente se escribe
> **después** de cerrar la versión en curso con su tag. Este documento
> se revisa una vez más contra lo que realmente quedó en V6 antes de
> considerarse spec oficial — puede cambiar.
>
> Rige la constitución del proyecto:
> [../1_constitution.md](../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md). Mapa completo de
> versiones: [../0_mapa_versiones.md](../0_mapa_versiones.md). **V7 es
> la última fila del mapa** — al cerrarla, las 37 tablas de
> `bdcatedras` quedan expuestas por completo.
>
> **Corrección importante frente al plan original de esta versión:**
> se asumía que V7 necesitaba **diseñar un SP nuevo** para
> `lote_migracion`/`detalle_migracion` — el mismo tipo de suposición
> que ya resultó incorrecta en V6 para `respuesta_encuesta`. Al
> revisar `script_bdcatedras.sql` (Artículo 5) resultó que ese SP
> **también ya existe**: `sp_generar_lote_migracion`/
> `fn_generar_lote_api`, apoyado en `fn_exportar_asis`. **V7 no agrega
> ningún SP ni migración nueva** — es, junto con v4, la segunda versión
> del proyecto sin ningún `CREATE PROCEDURE` propio. Ver ADR **D34**.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v7 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API, sin SP nuevo |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional)* |
> | [5_data_model.md](5_data_model.md) | Las 4 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v7 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |

---

## 1. Propósito de la v7

V1-V6 dejaron catálogos, estructura académica, personas, sesiones,
login/registro de asistencia y encuestas — todo el ciclo operativo de
una cátedra abierta. V7 cierra el proyecto con lo que una
administración real necesita alrededor de ese ciclo: **exportar** la
asistencia ya registrada al sistema institucional ASIS
(`lote_migracion`, `detalle_migracion`), **auditar** qué cambió y
quién lo cambió (`bitacora`, de solo lectura) y **configurar** los
parámetros que ya venían usando, sin que la API lo supiera, varias
funciones desde v1 y v5 (`parametro`).

```
sesion (v4) ──< lote_migracion >──┬── estado_proceso (v1)
                     │             └── usuario (v5, opcional)
                     └──< detalle_migracion >── registro_asistencia (v5)

bitacora >── usuario (v5, opcional) — SOLO LECTURA, la llenan los triggers
parametro — tabla plana, SIN relación con ninguna otra tabla
```

**V7 no agrega ningún SP ni migración nueva** (ADR D34): la única
operación "maestro-detalle" de esta versión —generar un lote de
migración junto con todas sus filas de detalle— ya la resuelve
`sp_generar_lote_migracion`, que existe desde v1 y que, además de
insertar el maestro y los detalles, **actualiza por su cuenta el
`estado` de la `Sesion`** a `MIGRADA` (ver RF184) — un efecto
colateral real sobre una tabla de v4, documentado aquí para que no se
lea como un error.

## 2. Alcance

**Incluye:**
- `LoteMigracion`: lectura completa + **generar** (`POST`, llama a
  `fn_generar_lote_api`) + **actualizar estado** (`PATCH`, un `UPDATE`
  simple — sin SP, porque el estado de un lote avanza con el tiempo
  según lo que responda ASIS, a diferencia de `LoteCargaAsistente` en
  v3, que es un registro de un solo uso). Sin `PUT` ni `DELETE`.
- `DetalleMigracion`: lectura + **actualizar resultado** (`PATCH`, un
  `UPDATE` simple). Sin `POST` propio (nace dentro de "generar"), sin
  `PUT` ni `DELETE`.
- `Bitacora`: **solo lectura** (RF190-191) — la llenan los triggers de
  la BD (ya confirmado desde el mapa de versiones, Supuesto 3); ningún
  verbo de escritura en toda la entidad.
- `Parametro`: CRUD completo (6 verbos) con llave de texto (`clave`),
  igual patrón que `Facultad`/`Sede` desde v1/v2 — la única tabla
  nueva de v7 que sí es un CRUD tradicional de principio a fin.

**No incluye:** nada — v7 es la última fila del mapa de versiones; no
hay ninguna tabla de `bdcatedras` que quede fuera de la API al cerrar
esta versión.

## 3. Entidades y requisitos funcionales

Numeración de RF continúa desde v6 (terminó en RF178).

### LoteMigracion

> RF179-RF185. `fk_usuario` ya no tiene la salvedad de FK circular (v5
> cerró esa posibilidad): es opcional porque no hay autorización en la
> API (D28 de v5), no por una limitación técnica.

- **RF179 — Listar** (`GET /api/LoteMigracion?fkSesion=`): sin el
  filtro, todas hasta el límite.
- **RF180 — Obtener por id** (`GET .../{idLote}`): 200; inexistente →
  404.
- **RF181 — Generar** (`POST /api/LoteMigracion` con
  **LoteMigracionGenerar**: `fkSesion` obligatorio; `fkUsuario`
  opcional): llama `SELECT * FROM fn_generar_lote_api(...)` y devuelve
  `idLote`. La sesión sin ningún registro migrable (`RN-08`/`RN-28`:
  sin `id_asis`, sin programa, o `es_externo`) → **500** (la función
  lanza su propio `RAISE EXCEPTION`, D25 de v5, sin cambios).
- **RF182 — Actualizar estado** (`PATCH /api/LoteMigracion/{idLote}`
  con **LoteMigracionActualizar**: `fkEstadoProceso` y/o
  `observaciones`, ambos opcionales): `UPDATE` directo, **sin** SP
  (RF183): a diferencia del "cerrar" de `LoteCargaAsistente` (v3), un
  lote de migración SÍ cambia de estado varias veces con el tiempo
  (`GENERADO` → `EN_COLA` → `EN_CURSO` → `CORRECTO`/`ERROR`/
  `INCORRECTO`, según lo que responda ASIS) — no es un registro de un
  solo uso. Un `fk_estado_proceso` inexistente → 500 (D9). Body vacío →
  400 (mismo molde `PATCH` de siempre).
- **RF183 — Sin `PUT` ni `DELETE`:** `nombre_archivo`, `fk_sesion` y
  `total_filas` no se editan nunca (son el hecho de cuándo y sobre qué
  sesión se generó, no cambian con el tiempo); borrar un lote borraría
  evidencia de un intento de migración real.
- **RF184 — Efecto colateral documentado: `Sesion.estado` pasa a
  `MIGRADA`:** `sp_generar_lote_migracion` hace, dentro de la misma
  transacción, `UPDATE sesion SET estado = 'MIGRADA'` — la respuesta
  del `POST` de RF181 no lo menciona explícitamente (mismo criterio
  que las cascadas ocultas de `DELETE` en v2-v4: se documenta aquí y
  en el smoke test).
- **RF185 — Quién entra al archivo de migración (`RN-08`/`RN-28`, no
  revalidado en la API):** un asistente externo (`es_externo: true`)
  se **cuenta** en `total_filas` pero **nunca** aparece como fila en
  `detalle_migracion`/en lo que `fn_exportar_asis` devolvería — regla
  de negocio de la BD, D9.

### DetalleMigracion

> RF186-RF189. Nace exclusivamente dentro de RF181.

- **RF186 — Listar** (`GET /api/DetalleMigracion?fkLoteMigracion=`):
  sin el filtro, todas hasta el límite.
- **RF187 — Obtener por id** (`GET .../{idDetalle}`): 200; inexistente
  → 404.
- **RF188 — Actualizar resultado** (`PATCH .../{idDetalle}` con
  **DetalleMigracionActualizar**: `resultado` — lista cerrada
  `PENDIENTE`/`ACEPTADO`/`RECHAZADO` —, `mensaje` opcional): simula la
  confirmación que, en un entorno real, llegaría de vuelta desde ASIS.
  Marcar `ACEPTADO` una fila cuyo `fk_registro` ya tiene **otra** fila
  `ACEPTADA` en cualquier lote → 500 (`ux_detmig_aceptado` — un
  registro de asistencia no se migra exitosamente dos veces, aunque se
  haya intentado en lotes distintos).
- **RF189 — Sin `POST` propio, `PUT` ni `DELETE`:** mismo criterio
  que `NovedadCarga` (v3) y `RespuestaItem` (v6) — nace solo dentro de
  otra operación; a diferencia de esas dos, aquí SÍ hay un `PATCH`
  (RF188), porque a diferencia de ellas, esta fila SÍ cambia de estado
  después de nacer.

### Bitacora

> RF190-RF191. Solo lectura — la llenan los triggers de la BD
> (`trg_bitacora_registro` sobre `registro_asistencia`, el único que
> existe hasta ahora en el script). Ningún endpoint de ninguna otra
> entidad necesita cambiar para que la bitácora se siga llenando —
> ya ocurre desde que `registro_asistencia` existe (v5), aunque v7 sea
> la primera versión que lo expone por HTTP.

- **RF190 — Listar** (`GET /api/Bitacora?nombreTabla=&fkUsuario=`):
  ambos filtros opcionales y combinables; sin ninguno, todas hasta el
  límite (recomendado usar `?limite=` bajo, dado que ya hay 168 filas
  de ejemplo y crecerá con cada `registro_asistencia`).
- **RF191 — Obtener por id** (`GET .../{idBitacora}`): 200;
  inexistente → 404. **Sin ningún verbo de escritura**: ni `POST`, ni
  `PUT`, ni `PATCH`, ni `DELETE` — la API no decide nunca qué entra a
  la bitácora, eso es enteramente responsabilidad de la BD (Artículo
  5, llevado a su forma más estricta de todo el proyecto: ni siquiera
  existe un "camino manual" como el `POST` directo de
  `RegistroAsistencia` en v5).

### Parametro

> RF192-RF197. CRUD completo, llave de texto — primera tabla desde
> `Facultad`/`Sede` (v1/v2) con una PK que el cliente elige al crear,
> en vez de una que la BD genera.

- **RF192 — Listar** (`GET /api/Parametro`): 200; vacía → 204 (aunque
  en la práctica nunca estará vacía: hay 9 parámetros de ejemplo que
  varias funciones de v5/v6 ya leen con `COALESCE` y un valor por
  defecto si la fila faltara).
- **RF193 — Obtener por clave** (`GET /api/Parametro/{clave}`): 200;
  inexistente → 404.
- **RF194 — Crear** (`POST /api/Parametro` con **ParametroCrear**):
  `clave` (`[RegularExpression]`, mismo espíritu que los demás
  catálogos de llave de texto), `valor`, `tipoDato` (lista cerrada:
  `TEXTO`, `ENTERO`, `BOOLEANO`, `FECHA`, `DECIMAL`), `descripcion`
  obligatorios; `fkUsuarioModifica` opcional. `clave` duplicada → 500.
- **RF195 — Reemplazar/Actualizar** (`PUT`/`PATCH .../{clave}`):
  igual molde que siempre, pero **`clave` nunca se edita** (es la
  llave de la ruta, no una columna del body — mismo criterio que
  `Facultad.id_facultad` desde v2). El repositorio fija
  `modificado_en = now()` explícitamente en el `UPDATE` (RF197).
- **RF196 — Eliminar** (`DELETE .../{clave}`): `filasEliminadas`;
  inexistente → 404. Sin ninguna FK entrante — borrar un parámetro es
  **seguro**: cada función que lo lee (`sp_emitir_enlace`,
  `sp_solicitar_clave`, `fn_trg_clave_derivar`, etc., todas de v5) ya
  tiene un valor de reserva con `COALESCE` si la fila no existe (D9,
  llevado a su forma más benigna: la ausencia del dato no rompe nada).
- **RF197 — `modificado_en` no tiene trigger propio:** a diferencia de
  `creado_en` en el resto del proyecto (que un `DEFAULT now()` fija
  solo al insertar), `parametro.modificado_en` no se actualiza sola en
  cada `UPDATE` — ninguna función de la BD lo necesita, así que no
  existe un trigger para ello. El **repositorio** es quien debe
  incluir `modificado_en = now()` explícitamente en cada
  `UPDATE`/`PATCH` (ver §4.3 de [3_plan.md](3_plan.md)) — la primera
  vez en el proyecto que una columna de fecha de modificación depende
  del código C#, no de un `DEFAULT` ni de un trigger.

## 4. Requisitos no funcionales

Rigen los mismos RNF1-RNF15 de v1-v6, sin cambios. No se añade ninguno
nuevo: v7 no introduce ningún patrón de validación, seguridad ni
infraestructura distinto de lo que ya existía.

## 5. Criterios de aceptación

1. `docker compose up -d --build` deja los **37** recursos (33 de
   v1-v6 + 4 de v7) respondiendo en `/swagger` — las 37 tablas de
   `bdcatedras` quedan expuestas por completo.
2. `POST /api/LoteMigracion` con `fkSesion: 5712` (una sesión de
   ejemplo con registros migrables) → 200 con `idLote`; `GET
   /api/DetalleMigracion?fkLoteMigracion={idLote}` muestra una fila
   por cada registro migrable de esa sesión (166 de ejemplo, sin
   contar los asistentes externos, que no aparecen aunque se hayan
   registrado); `GET /api/Sesion/5712` confirma `estado: "MIGRADA"`
   (RF184).
3. `PATCH /api/LoteMigracion/{idLote}` con
   `{"fkEstadoProceso": "EN_CURSO"}` → 200; con un `fkEstadoProceso`
   inexistente → 500.
4. `PATCH /api/DetalleMigracion/{idDetalle}` con
   `{"resultado": "ACEPTADO"}` → 200; marcar `ACEPTADO` otra fila con
   el mismo `fkRegistro` (de un lote distinto, si existe en los datos
   de ejemplo) → 500 (`ux_detmig_aceptado`).
5. `GET /api/Bitacora?limite=5` → 5 de las 168+ filas de ejemplo;
   ningún verbo de escritura existe para esta entidad (`POST`/`PUT`/
   `PATCH`/`DELETE` sobre `/api/Bitacora` → 404 de ASP.NET).
6. `POST /api/Parametro` con una `clave` nueva → 200; `PATCH
   /api/Parametro/{clave}` cambia `valor` y `modificado_en` avanza;
   `DELETE /api/Parametro/{clave}` → 200, sin ningún error de
   integridad referencial.
7. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las **37** entidades sin PostgreSQL — número que coincide,
   finalmente, con las 37 tablas de `bdcatedras`.
8. El panel de administración (`/admin`) muestra 4 tarjetas nuevas:
   `LoteMigracion` (con "generar" y actualizar estado),
   `DetalleMigracion` (con actualizar resultado), `Bitacora` (de solo
   lectura, con filtros), `Parametro` (CRUD completo).
