# Especificación — Versión 6 del proyecto: encuestas de calidad

> ⚠️ **BORRADOR.** Escrito mientras V2-V5 todavía no tienen su tag. El
> Artículo 1 de la constitución dice que la spec siguiente se escribe
> **después** de cerrar la versión en curso con su tag. Este documento
> se revisa una vez más contra lo que realmente quedó en V5 antes de
> considerarse spec oficial — puede cambiar.
>
> Rige la constitución del proyecto:
> [../1_constitution.md](../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md). Mapa completo de
> versiones: [../0_mapa_versiones.md](../0_mapa_versiones.md).
>
> **Corrección importante frente al plan original de esta versión:**
> se asumía que V6 necesitaba **diseñar un SP nuevo** para
> `respuesta_encuesta`/`respuesta_item`. Al revisar
> `script_bdcatedras.sql` (Artículo 5: la BD viene dada completa desde
> v1) resultó que ese SP **ya existe** —
> `sp_responder_encuesta`/`fn_responder_encuesta_api`, con su propio
> `sp_cerrar_encuesta` interno—. El SP nuevo que esta versión SÍ
> necesita (Guía IA, Regla 1) es para **`pregunta` + `opcion_pregunta`**,
> una pareja maestro-detalle que el plan original no había identificado.
> Ver ADR **D29**.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v6 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API y el SP nuevo de `Pregunta` |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional)* |
> | [5_data_model.md](5_data_model.md) | Las 4 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v6 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |

---

## 1. Propósito de la v6

V1-V5 dejaron catálogos, estructura académica, personas, sesiones y el
registro real de asistencia, pero ninguna sesión tenía todavía forma de
**medir su calidad**. V6 agrega las preguntas de una encuesta
(`pregunta`, `opcion_pregunta`) y las respuestas que un asistente ya
registrado da sobre la sesión a la que asistió
(`respuesta_encuesta`, `respuesta_item`) — cerrando el ciclo completo
"me registré → respondo cómo estuvo".

```
encuesta (v1) ──< pregunta >── tipo_pregunta (v1)
                     │
                     └──< opcion_pregunta

registro_asistencia (v5) ──< respuesta_encuesta >── encuesta (v1)
                                   │
                                   └──< respuesta_item >── pregunta, opcion_pregunta (esta versión)
```

Una relación maestro-detalle **nueva**, resuelta con un SP (Guía IA,
Regla 1 — ver ADR D29 y [3_plan.md](3_plan.md) §4.1):

1. **`pregunta` + sus `opcion_pregunta` iniciales.** Crear una pregunta
   junto con sus opciones de respuesta (si es de tipo `OPCION_UNICA`),
   en una sola transacción — mismo espíritu que el retrofit de
   `ProgramaAcademico` (v2).

La otra relación maestro-detalle de esta versión, `respuesta_encuesta`
+ `respuesta_item`, **no necesita un SP nuevo**: `sp_responder_encuesta`
ya existe desde v1, ya inserta el maestro y todos los detalles en una
sola transacción, y además ya llama internamente a `sp_cerrar_encuesta`
— así que "responder" y "cerrar" son, para la API, **una sola
operación** (ver ADR D30).

## 2. Alcance

**Incluye:**
- `Pregunta`: CRUD con un `POST` que acepta `opciones` iniciales
  opcionales (mismo espíritu que el retrofit de `ProgramaAcademico` en
  v2, pero de nacimiento, no como retrofit).
- `OpcionPregunta`: CRUD completo (5 verbos) — a diferencia de
  `AliasPrograma`/`ConsentimientoDatos`, una opción de respuesta SÍ se
  corrige editando su etiqueta u orden, no solo borrando y recreando.
- `RespuestaEncuesta`: **un solo verbo de escritura**, "responder"
  (`POST`), que llama a `fn_responder_encuesta_api` (ya existente).
  Lectura completa (`GET`); sin `PUT`/`PATCH`/`DELETE` (ADR D31: es un
  hecho auditable, como `ConsentimientoDatos` en v3 y
  `RegistroAsistencia` en v5).
- `RespuestaItem`: solo lectura — nace exclusivamente dentro de
  "responder".

**No incluye (es de otra versión):**
- `lote_migracion`, `detalle_migracion`, `bitacora`, `parametro` (v7).

## 3. Entidades y requisitos funcionales

Numeración de RF continúa desde v5 (terminó en RF158).

### Pregunta

> RF159-RF166. `id_tipo_pregunta` (v1) decide, vía
> `guarda_numero`/`guarda_texto`/`guarda_opcion`, qué campo de
> `respuesta_item` se espera al responder — la API no lo revalida
> (D9): lo vigila el trigger `fn_trg_respuesta_item_tipo` de la BD
> cuando alguien responde, no cuando se crea la pregunta.

- **RF159 — Listar** (`GET /api/Pregunta?fkEncuesta=`): sin el
  filtro, todas hasta el límite.
- **RF160 — Obtener por id** (`GET /api/Pregunta/{idPregunta}`): 200;
  inexistente → 404.
- **RF161 — Crear** (`POST /api/Pregunta` con **PreguntaCrear**):
  `fk_encuesta`, `enunciado`, `orden` (`>= 1`), `fk_tipo_pregunta`
  obligatorios; `obligatoria` opcional (por defecto `false`);
  `valor_min`/`valor_max` opcionales, **ambos o ninguno**, y si vienen
  `valor_max > valor_min` — validado en el **servicio**
  (`ArgumentException` → 400), mismo criterio que `Sesion.fin/inicio`
  en v4. **Retrofit de nacimiento (Guía IA, Regla 1):** acepta además
  una lista **opcional** `opciones` — mismo mecanismo que
  `ProgramaAcademico`/`alias` en v2, vía `sp_guardar_pregunta` (ver
  [3_plan.md](3_plan.md) §4.1). Si cualquier opción falla (orden
  duplicado), **no se crea ni la pregunta**. `(fk_encuesta, orden)`
  duplicado → 500 (`uq_pregunta_ord`).
- **RF162 — Reemplazar** (`PUT .../{idPregunta}` con
  **PreguntaReemplazo**): las columnas propias (sin tocar las
  opciones); inexistente → 404.
- **RF163 — Actualizar parcialmente** (`PATCH .../{idPregunta}`):
  todo opcional; body vacío → 400; inexistente → 404.
- **RF164 — Eliminar** (`DELETE .../{idPregunta}`): `filasEliminadas`;
  inexistente → 404. `opcion_pregunta` es `ON DELETE CASCADE` (se
  borran con la pregunta); `respuesta_item` es `RESTRICT` — borrar una
  pregunta que ya tiene respuestas de ejemplo (248 filas en total) →
  500, sin que la cascada hacia sus opciones llegue a ejecutarse.
- **RF165 — `(fk_encuesta, orden)` único, validado por la BD:** dos
  preguntas de la misma encuesta con el mismo `orden` → 500
  (`uq_pregunta_ord`, D9, sin revalidar en el servicio).
- **RF166 — `valor_min`/`valor_max` solo tienen sentido declarativo
  aquí:** definen el rango que el trigger de `respuesta_item`
  verificará DESPUÉS, al responder (RN-25) — `Pregunta` no valida
  ninguna respuesta todavía, solo guarda el rango esperado.

### OpcionPregunta

> RF167-RF171. CRUD completo (5 verbos) — a diferencia de
> `AliasPrograma` (v2) o `ConsentimientoDatos` (v3), aquí SÍ hace falta
> `PUT`/`PATCH`: corregir la etiqueta de una opción o reordenarla es
> una edición legítima, no una "nueva versión del hecho".

- **RF167 — Listar** (`GET /api/OpcionPregunta?fkPregunta=`): sin el
  filtro, todas hasta el límite.
- **RF168 — Obtener por id** (`GET .../{idOpcion}`): 200; inexistente
  → 404.
- **RF169 — Crear** (`POST` con **OpcionPreguntaCrear**):
  `fk_pregunta`, `etiqueta`, `orden` (`>= 1`, igual molde que
  `Pregunta.orden`) obligatorios; `valor` (el peso numérico opcional de
  la opción, p. ej. para promediar respuestas de opción única)
  opcional. `(fk_pregunta, orden)` duplicado → 500 (`uq_opcion_orden`).
- **RF170 — Reemplazar/Actualizar** (`PUT`/`PATCH .../{idOpcion}`):
  igual molde que siempre; inexistente → 404.
- **RF171 — Eliminar** (`DELETE .../{idOpcion}`): `filasEliminadas`;
  inexistente → 404. `respuesta_item.fk_opcion` es `RESTRICT` — borrar
  una opción que ya fue elegida en alguna respuesta de ejemplo → 500.

### RespuestaEncuesta

> RF172-RF175. **Un solo verbo de escritura** (ADR D30): "responder"
> llama a `fn_responder_encuesta_api`, que inserta el maestro y TODOS
> los `respuesta_item` del detalle, y al final llama ella misma a
> `sp_cerrar_encuesta` — todo en la misma transacción que el `CALL`
> original. La API nunca invoca "cerrar" por separado: no existe ese
> verbo en el contrato.

- **RF172 — Listar** (`GET /api/RespuestaEncuesta?fkEncuesta=`): sin
  el filtro, todas hasta el límite.
- **RF173 — Obtener por id** (`GET .../{idRespuesta}`): 200;
  inexistente → 404. Incluye `completa` (siempre `true` si la fila
  existe — ver nota de RF175).
- **RF174 — Responder** (`POST /api/RespuestaEncuesta` con
  **RespuestaEncuestaResponder**: `fkRegistro` obligatorio;
  `respuestas` — lista NO vacía de
  `{pregunta, numero?, texto?, opcion?}`, exactamente uno de los 3
  últimos según el tipo de la pregunta — obligatoria): llama
  `SELECT * FROM fn_responder_encuesta_api(@FkRegistro, CAST(@Respuestas
  AS jsonb))` y devuelve `idRespuesta`. `respuestas` vacía → 500
  (`R29`, el propio SP lo rechaza — D24 de v5, mismo criterio: una
  excepción de negocio de la BD cae en 500, no se revalida antes en
  C#, porque validar "la lista no está vacía" en el servicio
  duplicaría, sin necesidad real, una regla que la BD YA aplica de
  forma completa). Falta una pregunta **obligatoria** → 500 (`RN-26`,
  dentro de `sp_cerrar_encuesta`, y en ese caso **nada** queda
  guardado: ni el maestro ni ningún detalle, porque el `RAISE
  EXCEPTION` ocurre dentro del mismo `CALL`). El registro ya tenía una
  respuesta → 500 (`uq_respuesta_registro`). La sesión de ese registro
  no tiene `fk_encuesta` asignado → 500 (el propio SP lo valida).
- **RF175 — Sin `PUT`, `PATCH` ni `DELETE` (ADR D31):** una respuesta
  de encuesta es un hecho auditable, igual que `ConsentimientoDatos`
  (v3) y `RegistroAsistencia` (v5) — se corrige respondiendo de nuevo
  **no es posible** (`uq_respuesta_registro` lo impide): un registro
  de asistencia responde la encuesta una sola vez, para siempre.

### RespuestaItem

> RF176-RF178. Solo lectura — nace exclusivamente dentro de RF174.

- **RF176 — Listar** (`GET /api/RespuestaItem?fkRespuesta=`): sin el
  filtro, todas hasta el límite.
- **RF177 — Obtener por id** (`GET .../{idItem}`): 200; inexistente →
  404.
- **RF178 — Sin `POST` propio, `PUT`, `PATCH` ni `DELETE`:** mismo
  criterio que `NovedadCarga` (v3) — nace solo dentro de otra
  operación.

## 4. Requisitos no funcionales

Rigen los mismos RNF1-RNF13 de v1-v5, sin cambios. Se añade:

- **RNF14 — "Responder" y "cerrar" son una sola operación, no dos
  (D30):** ninguna spec de esta versión expone un verbo "cerrar"
  independiente para `RespuestaEncuesta` — hacerlo sería inventar una
  ruta para una operación que la BD ya resuelve atómicamente dentro de
  `sp_responder_encuesta`.

## 5. Criterios de aceptación

1. `docker compose up -d --build` deja los 33 recursos (29 de v1-v5 +
   4 de v6) respondiendo en `/swagger`.
2. `POST /api/Pregunta` con `opciones` → 200, y
   `GET /api/OpcionPregunta?fkPregunta={id}` muestra las opciones con
   el `fk_pregunta` correcto; el mismo `POST` con una opción de `orden`
   repetido → 500 y **nada** queda creado (ni la pregunta).
3. `POST /api/RespuestaEncuesta` sobre un `fkRegistro` real sin
   respuesta previa (p. ej. `id_registro: 61`, sesión con encuesta
   asignada), respondiendo las 2 preguntas obligatorias de ejemplo y
   dejando sin responder la opcional de texto libre → 200, con
   `completa: true` en un `GET` posterior. El mismo `POST` repetido
   sobre el mismo `fkRegistro` → 500 (`uq_respuesta_registro`).
4. `POST /api/RespuestaEncuesta` **sin** responder alguna de las 2
   preguntas obligatorias → 500, y `GET
   /api/RespuestaEncuesta?fkEncuesta=1` confirma que no quedó ninguna
   fila nueva (todo o nada, aunque el fallo ocurra en el paso de
   "cerrar", que corre dentro del mismo `CALL`).
5. `DELETE /api/Pregunta/{id}` sobre una de las 4 preguntas de ejemplo
   (248 respuestas en total) → 500; sus `OpcionPregunta` de ejemplo
   siguen intactas (la cascada no llegó a ejecutarse).
6. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las 33 entidades sin PostgreSQL, incluida la validación de
   `valor_min`/`valor_max` y la verificación de que `RespuestaEncuesta`
   no tiene método de "cerrar" en su interfaz de repositorio.
7. El front muestra, en el detalle de una sesión ya pasada, un enlace
   para responder su encuesta (si la sesión tiene `fk_encuesta` y el
   asistente ya tiene un `registro_asistencia` sin respuesta previa);
   el panel de administración permite crear preguntas con sus opciones
   iniciales y ver las respuestas recibidas (sin poder editarlas).
8. El panel de administración (`/admin`) muestra 4 tarjetas nuevas:
   `Pregunta` (con opciones iniciales al crear), `OpcionPregunta` (CRUD
   completo), `RespuestaEncuesta`/`RespuestaItem` (solo lectura).
