# Decisiones de diseño — Versión 3: personas y carga de asistentes

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## D15 — El cierre del lote de carga es parcial, no todo-o-nada

Ver desarrollo completo en §4.3 de [3_plan.md](3_plan.md). Resumen de
las opciones:

(a) Un único SP que recibe TODAS las filas del archivo en un `jsonb` y
hace todo en una transacción (como el retrofit de v2);
(b) Cada fila es su propia petición/transacción (`POST /api/Asistente`)
y un segundo `CALL` separado solo cierra el lote con el resultado.

**Decisión: (b).** (a) significa que una fila mala en 16.093 tumba las
16.092 buenas — exactamente el problema que `lote_carga_asistente`
existe para resolver (su propio comentario en el esquema: "una carga de
16.093 filas necesita EVIDENCIA, no un mensaje en pantalla: sin estos
contadores, 'cargué menos filas de las que tenía' es indepurable").
(+) Cada fila buena queda guardada aunque otras fallen. (+) El cierre
(paso 2) sigue siendo atómico donde SÍ importa serlo: los contadores y
las novedades son una sola foto del resultado. (−) 16.093 peticiones
HTTP en vez de 1 — aceptado para v3 (proyecto didáctico); si el
rendimiento de la carga real demuestra ser un problema, un *bulk
insert* por lotes de N filas es candidato para una spec futura, no algo
que se resuelve aquí por anticipado (mismo criterio YAGNI que cerró el
ADR D13 de v2).

## D16 — Tablas de vigencia con llave compuesta: sin `/{id}`, con "cerrar" en vez de `DELETE`

**Contexto.** `vinculacion_asistente` y `programa_asistente` no tienen
una columna sustituta (`id`): su llave primaria son 2-3 columnas de
negocio. Es la primera vez en el proyecto (V1 y V2 tenían siempre una
PK de una sola columna, natural o generada).

**Opciones evaluadas:**
(a) agregar una columna `id BIGINT IDENTITY` a cada tabla, solo para
que el molde de 5 verbos funcione igual que siempre (cambiaría el
esquema, que el Artículo 5 prohíbe sin que una spec lo pida con una
razón real);
(b) respetar la llave compuesta tal como la diseñó la BD y adaptar el
contrato: sin `GET/{id}` de una columna, `POST` para abrir, `PATCH
.../cerrar` (o `/cambiar-estado`) con la llave completa en el body, sin
`PUT` ni `DELETE`.

**Decisión: (b).** (a) violaría el Artículo 5 ("la base de datos viene
DADA") sin ninguna necesidad real — la llave compuesta ya identifica la
fila sin ambigüedad. El patrón se repetirá en v5 (`rol_por_usuario`
tiene la misma forma: `fk_usuario, fk_rol, fecha_ini`), así que este
ADR es la referencia para esa versión también.

**Consecuencias.** (+) El esquema no cambia por conveniencia de la API.
(+) El nombre de la acción (`cerrar`, `cambiar-estado`) documenta la
regla de negocio mejor que un `PUT` genérico lo haría (un `PUT` sobre
historia sugiere "reemplazar el pasado", que no es lo que ocurre). (−)
Es la primera asimetría de rutas del proyecto (`/api/VinculacionAsistente/cerrar`
en vez de `/api/VinculacionAsistente/{id}`) — se señala aquí y en
[6_contracts.md](6_contracts.md) para que no se lea como un olvido.

## D17 — El archivo de carga se interpreta en Python, no en C#

Ver desarrollo completo en §4.4 de [3_plan.md](3_plan.md).

## D18 — Por qué el listado de `Asistente` NO repite la paginación-en-Flask de `Catedra` (D13 de v2)

**Contexto.** V2 decidió (D13) que el catálogo de `Catedra` pide TODAS
las filas una vez (5502) y pagina/filtra en Flask, sin tocar el
contrato de la API. `Asistente` tiene 14.811 filas de ejemplo — casi 3
veces más — y, a diferencia de `Catedra`, no es un catálogo público:
es una pantalla de administración donde el buscador se usa todo el
tiempo, con datos que cambian con cada carga.

**Opciones evaluadas:** (a) repetir D13 (una sola llamada sin límite,
filtrar en Flask); (b) agregar `?q=` al contrato de la API y filtrar
con `ILIKE` en SQL, pidiendo solo la página visible.

**Decisión: (b).** A diferencia de `Catedra` (datos de un semestre, que
no crecen durante el uso), `Asistente` crece con cada carga masiva —
descargar 14.811+ filas en cada visita a la pantalla de administración
escala peor que un catálogo que se consulta una vez por sesión de
estudio. El costo es que el contrato de `Asistente` SÍ cambia respecto
al molde de v1/v2 (agrega `?q=`), lo que el Artículo 7 permite porque
es la spec de ESTA versión definiéndolo desde el principio, no una
ampliación de un contrato ya cerrado.

**Consecuencias.** (+) El buscador de administración no descarga toda
la tabla en cada tecla. (−) Segunda forma de paginar/filtrar en el
proyecto (`?q=` en SQL para `Asistente`, todo-en-Flask para `Catedra`)
— aceptado porque reflejan escalas y usos distintos (3000 vs. 14811+
filas, público vs. administración), no una inconsistencia accidental.
