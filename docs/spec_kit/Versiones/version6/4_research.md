# Decisiones de diseño — Versión 6: encuestas de calidad

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## D30 — Dónde cae realmente la Regla 1 en esta versión (corrección del plan original)

**Contexto.** El plan original de este proyecto asumía que V6
necesitaba diseñar un SP nuevo para la pareja maestro-detalle
`respuesta_encuesta`/`respuesta_item`. Al investigar
`script_bdcatedras.sql` (como exige el Artículo 5 antes de diseñar
cualquier cosa nueva) resultó que `sp_responder_encuesta` —con su
`fn_responder_encuesta_api`— **ya existe desde v1** y ya resuelve ese
maestro-detalle por completo, incluyendo el cierre automático
(`sp_cerrar_encuesta`, ver ADR D31). En cambio, `pregunta` +
`opcion_pregunta` — una pareja maestro-detalle igual de real (crear una
pregunta de opción única sin sus opciones no tiene ningún uso
práctico) — **no tenía ningún SP**, en ninguna versión.

**Opciones evaluadas:** (a) seguir el plan original al pie de la
letra: no revisar si ya existía un SP para `respuesta_encuesta`, y no
buscar si `pregunta`/`opcion_pregunta` necesitaba uno; (b) investigar
primero qué SP ya trae la BD (Artículo 5) y diseñar solo lo que
realmente falta.

**Decisión: (b).** Seguir (a) habría producido dos errores: reescribir
en C#/SQL una regla de seguridad y de negocio que la BD ya resuelve
mejor (revalidando intentos, ventanas, cierre automático — el mismo
riesgo que D9/D20/D24 ya vienen evitando desde v2), y al mismo tiempo
dejar sin resolver una necesidad real de maestro-detalle que sí existe
(`Pregunta`+sus opciones). La Guía IA, Regla 1 no dice "cada versión
agrega un SP a la tabla que el plan haya nombrado" — dice "toda
operación que cree un maestro y sus detalles en una sola petición se
resuelve con un SP" — y esa regla, aplicada con cuidado, señala a
`pregunta`/`opcion_pregunta`, no a `respuesta_encuesta`/`respuesta_item`.

**Consecuencias.** (+) Cero código nuevo reimplementando
`sp_responder_encuesta` (que ya maneja incluso el cierre automático,
algo que una reimplementación apurada fácilmente pasaría por alto). (+)
Se identifica correctamente dónde la API SÍ tiene trabajo real de
diseño (`Pregunta`). (−) Ninguna real: la única "pérdida" es el tiempo
que habría tomado diseñar un SP innecesario, evitado por investigar
antes de construir.

## D31 — "Responder" y "cerrar" son una sola operación, no dos

**Contexto.** El esquema tiene dos procedimientos separados,
`sp_responder_encuesta` y `sp_cerrar_encuesta` — a primera vista,
sugiere dos verbos de API: "responder" (guardar los items) y "cerrar"
(marcar `completa = true`), como si fueran pasos independientes de un
flujo de varios clics (análogo a `LoteCargaAsistente`
abrir/cerrar en v3).

**Opciones evaluadas:** (a) exponer ambos como verbos separados de la
API (`POST /responder` y `POST /{id}/cerrar`), reflejando la existencia
de los 2 procedimientos; (b) exponer solo "responder": leyendo
`sp_responder_encuesta`, su último paso es `CALL sp_cerrar_encuesta
(p_id_respuesta)` — **dentro de la misma transacción**, sin que el
cliente HTTP intervenga entre uno y otro.

**Decisión: (b).** `sp_cerrar_encuesta` existe como un procedimiento
reutilizable **a nivel SQL** (quizás pensado para que otro flujo
interno de la BD lo invoque alguna vez), pero desde la perspectiva de
la API — que solo puede llamar lo que `sp_responder_encuesta` expone
como punto de entrada — "responder" y "cerrar" ocurren siempre
juntos, en el mismo `CALL`, nunca por separado. Exponer un segundo
verbo HTTP para una operación que el cliente nunca puede disparar de
forma independiente (porque la única vía de entrada ya la incluye)
sería un endpoint que existe pero nunca tiene un caso de uso real
distinto del primero.

**Consecuencias.** (+) La API reduce 2 pasos SQL a 1 verbo HTTP,
reflejando fielmente lo que de verdad puede pasar. (+) Ninguna
confusión posible sobre "¿me faltó llamar a cerrar?" — no existe esa
posibilidad. (−) Si una versión futura necesitara, por alguna razón
real, responder una encuesta en varios pasos (guardar una respuesta
parcial, cerrar después), este diseño no lo soporta sin un cambio de
contrato — aceptado porque ninguna spec hasta ahora lo pide, y el
propio `script_bdcatedras.sql` tampoco lo prevé (RN-26 exige TODAS las
preguntas obligatorias en el mismo `CALL`, no permite una respuesta a
medias).

## D32 — `RespuestaEncuesta` es de solo lectura tras "responder"

**Contexto.** ¿Debería poder editarse una respuesta ya dada (p. ej.
si el asistente se equivocó al elegir una opción)?

**Opciones evaluadas:** (a) permitir `PUT`/`PATCH` sobre
`RespuestaEncuesta`/`RespuestaItem` para corregir una respuesta; (b)
sin `PUT`/`PATCH`/`DELETE` — una respuesta, una vez dada, es un hecho
auditable.

**Decisión: (b).** Mismo criterio que ya establecieron
`ConsentimientoDatos` (v3, ADR implícito en su propio RF113) y
`RegistroAsistencia` (v5, RF157): estas filas son evidencia de que algo
ocurrió en un momento dado, no un estado editable. Además,
`uq_respuesta_registro` (único por `fk_registro`) ya impide "responder
de nuevo" como mecanismo alternativo de corrección — no hace falta ni
siquiera decidir qué pasaría con un segundo intento, la BD ya lo
rechaza.

**Consecuencias.** (+) Consistente con el resto del proyecto: cada
tabla que modela un hecho (no un estado de negocio con historia, como
`VinculacionAsistente`) sigue el mismo patrón de solo-lectura tras
crearse. (−) Un error de captura real (el asistente marcó la opción
equivocada) no tiene corrección posible por API — se documenta como
limitación conocida, consistente con que tampoco `RegistroAsistencia`
ni `ConsentimientoDatos` la tienen.

## D33 — `OpcionPregunta` SÍ tiene `PUT`/`PATCH` (a diferencia de `AliasPrograma`/`ConsentimientoDatos`)

**Contexto.** ¿`OpcionPregunta` debería seguir el molde de solo-anexo
de `AliasPrograma` (v2) o el de 5 verbos completos?

**Opciones evaluadas:** (a) solo-anexo (crear/eliminar, sin editar),
igual que `AliasPrograma`; (b) 5 verbos completos, con `PUT`/`PATCH`.

**Decisión: (b).** `AliasPrograma` es solo-anexo porque cada alias es,
en sí, un **hecho** ("alguien escribió el programa así alguna vez") —
corregirlo borraría evidencia de una variante real de escritura.
`OpcionPregunta` es distinto: una opción de respuesta
("Sí"/"No"/"Tal vez") es **configuración** de la encuesta, no un
hecho ocurrido — corregir su etiqueta (un error de tipeo, "Si" sin
tilde) o su `orden` (reordenar las opciones en pantalla) es exactamente
el tipo de edición legítima que el molde completo ya sirve para el
resto del proyecto (`Facultad`, `Catedra`, `Sesion`...).

**Consecuencias.** (+) Ninguna necesidad de borrar y recrear una
opción solo para corregir un typo. (−) Si una opción YA fue elegida en
alguna `respuesta_item` de ejemplo y se le cambia la `etiqueta`, las
respuestas antiguas "cambian de significado" retroactivamente (el
`id_opcion` en `respuesta_item` no guarda una copia del texto, solo la
referencia) — aceptado como el mismo costo que cualquier catálogo
editable del proyecto asume desde v1 (p. ej. renombrar una `Sede` no
reescribe las sesiones que ya ocurrieron ahí); no es una situación
nueva que esta versión introduzca.
