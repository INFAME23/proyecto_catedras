# Investigación y decisiones — Versión 2: estructura académica y catálogo de cátedras

> **Versión 2** · **Lectura opcional** (el porqué de las decisiones del
> plan, con las alternativas que se evaluaron y descartaron). Complementa
> a [3_plan.md](3_plan.md); el orden de trabajo está en
> [8_tasks.md](8_tasks.md). Continúa la numeración de ADRs de
> [version1/4_research.md](../version1/4_research.md) (D1-D8).

---

## D9 — FK hacia tablas que ya existen: no se revalida, se deja fallar

**Contexto.** Es la primera vez en el proyecto que un `INSERT` de una
tabla puede violar una foreign key hacia otra (`programa_academico →
facultad`, `alias_programa → programa_academico`, `catedra →
tipo_evento`/`dependencia`). Hay que decidir dónde se detecta un padre
inexistente.

**Opciones evaluadas:**
(a) el servicio hace un `SELECT` al repositorio padre antes de insertar,
y lanza `ArgumentException` (400) si no existe;
(b) el repositorio deja que el `INSERT` falle y la `NpgsqlException` sube
tal cual al controller (500);
(c) un trigger de la BD que traduzca el error a un formato propio.

**Criterios:** una sola fuente de verdad · número de ida y vueltas a la
BD por `POST` · consistencia con cómo ya se trata un `UNIQUE` violado en
v1.

| Criterio | (a) SELECT previo | (b) dejar fallar | (c) trigger |
|---|---|---|---|
| Una sola fuente de verdad | ✗ (dos consultas pueden desincronizarse en condiciones de carrera) | ✓ (la FK del motor es la única autoridad) | ✓, pero añade lógica a la BD que la constitución no autoriza para v2 |
| Viajes a la BD por `POST` | 2 | 1 | 1 |
| Consistencia con `UNIQUE` en v1 | distinta (ahí también se deja fallar) | ✓ misma regla aplicada dos veces | — |
| Código nuevo | un método de verificación por cada FK | ninguno | un trigger por tabla |

**Decisión: (b).** El repositorio no verifica nada que la BD ya vaya a
verificar. Es el mismo criterio que ya rige para un `id_sede` duplicado
en v1 (§5, criterio 5 de [version1/2_spec.md](../version1/2_spec.md)):
el error del motor sube como 500 con el mensaje en `detalle`.

**Consecuencias.** (+) Un solo camino de error para "algo que el motor
rechazó" (duplicado o FK), fácil de explicar y de probar. (+) Sin
condición de carrera entre el `SELECT` de verificación y el `INSERT`
real. (−) El mensaje de un 500 por FK es el texto crudo de Npgsql
(`violates foreign key constraint "fk_programa_academico_facultad"`),
menos amigable que un 400 a medida — costo aceptado, igual que ya se
aceptó para los duplicados en v1.

## D10 — Autorreferencia: se valida un nivel, no toda la cadena

**Contexto.** `facultad.fk_facultad_padre` y
`dependencia.fk_dependencia_padre` apuntan a la misma tabla. Una fila no
puede ser su propio padre (`CHECK` de la BD), pero nada impide, en
teoría, un ciclo más largo (`A` es padre de `B`, `B` es padre de `A`) si
alguien edita el padre de una fila ya existente.

**Opciones evaluadas:**
(a) validar solo que `fk_padre ≠ id` (un nivel), en el servicio;
(b) recorrer la cadena de padres en cada escritura para detectar
ciclos de cualquier longitud;
(c) no validar nada en el servicio y confiar solo en el `CHECK` de la BD.

**Decisión: (a).** Un ciclo de más de un nivel exigiría una consulta
recursiva (`WITH RECURSIVE`) en cada `POST`/`PUT`/`PATCH` de
`Facultad`/`Dependencia`, para un caso que ni la jerarquía real de la
universidad (9 facultades, 8 dependencias, ambas de 2 niveles) ni los
datos de ejemplo necesitan hoy. (c) se descarta porque, como se explica
en §4.2 de [3_plan.md](3_plan.md), el `CHECK` no protege un `POST`
nuevo — dejar solo el `CHECK` deja pasar el caso más común (una fila
que se apunta a sí misma al crearse) sin aviso claro.

**Consecuencias.** (+) Validación simple, consistente con el resto del
proyecto (nada de recursión en v2). (−) Un ciclo de 2 o más niveles,
creado editando el padre de una fila existente, no lo detecta el
servicio — lo detendría el `CHECK` solo si alguno de los pasos
intermedios vuelve a apuntar literalmente a sí mismo, lo cual un ciclo
largo no hace. **Riesgo documentado, no resuelto**: si una versión
futura necesita jerarquías más profundas con esta garantía, es un buen
candidato para su propia spec (Artículo 1).

## D11 — La columna generada de `Catedra`: un solo `RETURNING`

**Contexto.** `nombre_asis` es `GENERATED ALWAYS AS (left(nombre, 30))
STORED` — existe en la BD, no en el C#. Hay que decidir cómo el
repositorio obtiene su valor justo después de crear una cátedra.

**Opciones evaluadas:**
(a) duplicar la lógica en C# (`nombre.Length > 30 ? nombre[..30] :
nombre`) y asumir que coincide con lo que calculó Postgres;
(b) un `INSERT ... RETURNING id_catedra, nombre_asis` en una sola
sentencia;
(c) `INSERT` normal seguido de un `SELECT` aparte por el `id_catedra`
recién creado.

**Decisión: (b).** PostgreSQL permite devolver columnas generadas en el
mismo `RETURNING` que una `IDENTITY`, así que no hay motivo para (c) —
sería un viaje extra a la BD sin ninguna ventaja. (a) se descarta porque
duplica una regla que ya vive en la BD (viola el mismo principio por el
que `canal_difusion` no se revalida en dos sitios): si el `LEFT(...,
30)` cambiara algún día (p. ej. por tildes o normalización Unicode), el
C# quedaría desincronizado en silencio.

**Consecuencias.** (+) Una sola fuente de verdad para `nombre_asis`: la
BD. (+) Un solo viaje a la base. (−) `IRepositorioCatedra.CrearAsync`
devuelve una tupla en vez de un solo valor, un poco menos uniforme que
`IRepositorioEncuesta.CrearAsync` de v1 — diferencia aceptada porque
refleja una diferencia real entre las dos tablas (una columna más que
la BD calcula).

## D12 — `AliasPrograma`: verbos incompletos a propósito

**Contexto.** El molde de 5 verbos (listar, obtener, crear, reemplazar,
actualizar, eliminar — en realidad 6 incluyendo "obtener por id") se usó
igual en las 15 entidades hasta ahora. `alias_programa` es la primera
tabla sin necesidad real de `PUT`/`PATCH`: el propio comentario del
esquema la describe como un registro histórico de texto libre que crece
con el tiempo, no una fila que se "corrija" in situ.

**Opciones evaluadas:**
(a) implementar los 6 verbos en los 15 recursos por igual, "por
consistencia de la API";
(b) implementar solo listar, obtener, crear y eliminar — sin `PUT` ni
`PATCH` — para esta tabla específicamente.

**Decisión: (b).** El Artículo 1 (YAGNI) pesa más que la uniformidad
superficial: agregar un `PUT`/`PATCH` que ninguna historia de usuario
pide, solo para que las 15 tablas "se vean iguales" en Swagger, es
construir código que nadie va a ejercitar y que however, al existir,
alguien terminaría usando para "corregir" un alias — exactamente lo que
el diseño de la tabla quiere evitar (perder el rastro de qué se
escribió realmente en el formulario).

**Consecuencias.** (+) El código documenta la regla de negocio con su
propia forma: quien lea `IRepositorioAliasPrograma` ve, sin leer la
spec, que esta tabla es de solo-anexo. (−) Es la primera asimetría del
proyecto entre tablas — se señala explícitamente en 3 documentos
(`2_spec.md`, `3_plan.md`, aquí) para que no se lea como un olvido en
ningún momento futuro.

## D13 — Paginación del catálogo público: una sola llamada, filtrado y paginado en el front

**Contexto.** `Catedra` tiene 5502 filas de ejemplo. El catálogo público
(RF90) necesita mostrar la lista en páginas manejables Y permitir buscar
por texto libre — y la spec exige que la búsqueda no agregue un
endpoint nuevo a la API (RF90: "filtrado en el propio template").

**Opciones evaluadas:**
(a) agregar `offset` al contrato de `GET /api/Catedra` y que el front
pida página por página a la API;
(b) el front pide TODA la lista una vez (`GET /api/Catedra`, sin
`limite`) y filtra + pagina en JavaScript, en el navegador;
(c) el front pide TODA la lista una vez y filtra + pagina en la propia
ruta Flask (Python), antes de renderizar la plantilla — sin JavaScript
nuevo.

**Decisión: (c).** (a) se descarta porque cambia el contrato de la API
por conveniencia de una sola pantalla (Artículo 7: los contratos de
cada versión son los que su `6_contracts.md`/`2_spec.md` ya fijaron; no
se amplían a mitad de versión). Entre (b) y (c), (c) gana porque el
proyecto no tiene un framework de JavaScript y el front ya resuelve todo
del lado del servidor (Flask + Jinja, sin fetch desde el navegador) —
agregar JavaScript solo para esta pantalla rompería la consistencia del
resto de `front_catedras/`. El tamaño de la respuesta (5502 filas,
~800 KB de JSON) es aceptable para una sola llamada por visita al
catálogo en un proyecto didáctico.

**Consecuencias.** (+) El contrato de la API no cambia. (+) Cero
JavaScript nuevo: la ruta Flask recibe `?q=` y `?pagina=`, filtra la
lista en memoria con una comprensión simple y corta con slicing antes de
pasarla a la plantilla. (−) Cada visita al catálogo descarga las 5502
filas aunque solo se muestren 24 — costo aceptado para v2; si el
catálogo demuestra ser lento en uso real, cachear esa lista en el front
(con invalidación simple por tiempo) es candidato para una spec futura,
no para resolver aquí por anticipado.

> **Nota de consistencia:** esta decisión reemplaza la idea de "límite
> creciente" que había quedado esbozada en una versión anterior del §4.7
> de [3_plan.md](3_plan.md); ese documento ya se corrigió para que
> ambos queden alineados.

## D14 — Retrofit: programa y alias iniciales en una sola transacción (SP)

**Contexto.** En octubre de 2026 se adoptó la
[guía operativa para la IA](../../guia_ia.md), cuya Regla 1 exige que
una operación que crea un **maestro junto con sus detalles** se haga con
un único procedimiento almacenado. En la v2 el par maestro-detalle es
`programa_academico` → `alias_programa`. Hasta ahora, para crear un
programa con sus alias había que hacer 1 + N peticiones separadas: si el
segundo alias fallaba, el programa y el primer alias ya estaban
guardados (estado a medias).

**Opciones evaluadas:**
(a) dejarlo como está (no hay una petición que una maestro y detalle);
(b) agregar `alias` opcional al POST del programa y resolverlo con un SP
explícito `sp_guardar_programa_academico(..., p_alias jsonb)`;
(c) un SP genérico con SQL dinámico que sirva para cualquier
maestro-detalle.

**Decisión: (b).** (a) no cumple la Regla 1. (c) se descarta porque el
SQL dinámico deja de estar "a la vista" (Artículo 2) y es mucho más
difícil de explicar línea a línea (Artículo 6): **un SP explícito por
cada par maestro-detalle**. El estilo copia el de
`sp_responder_encuesta`, que ya trae la BD (parámetro `jsonb` con el
arreglo de detalles).

**Relación con D12.** D12 **sigue vigente**: `AliasPrograma` continúa
sin `PUT`/`PATCH`, y `POST /api/AliasPrograma` sigue siendo la forma de
agregar un alias a un programa que ya existe (ahí no hay maestro y
detalle en la misma petición, así que la Regla 1 no aplica). Lo único
que cambia es que, ADEMÁS, los alias pueden nacer junto con su programa.

**Consecuencias.** (+) Atomicidad real (ACID · A): o se guardan el
programa y todos sus alias, o nada. (+) El POST sin `alias` no cambia,
así que ningún cliente existente se rompe (Artículo 7). (−) La BD gana un
objeto que no venía en el script provisto (Artículo 5): se agrega como
**migración aparte** (`db/postgres/migraciones/`), sin tocar
`script_bdcatedras.sql`, y queda documentado aquí. (−) Una BD ya creada
(volumen con datos) necesita aplicar la migración a mano una vez (ver
[7_quickstart.md](7_quickstart.md)).
