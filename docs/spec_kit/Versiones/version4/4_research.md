# Decisiones de diseño — Versión 4: sesiones y ponencias

> ✅ **Revisada y construida (2026-10-08)** — ver nota de [2_spec.md](2_spec.md).

## D19 — `Ponencia`: llave compuesta sin historial, por eso SÍ admite `DELETE`

**Contexto.** `Ponencia` tiene llave primaria compuesta
`(fk_sesion, fk_asistente, rol)`, igual en forma que
`VinculacionAsistente`/`ProgramaAsistente` de v3 (ADR D16). La pregunta
es si debe seguir el mismo molde (sin `/{id}`, sin `DELETE`, con una
acción de "cerrar") o uno distinto.

**Opciones evaluadas:**
(a) aplicar D16 tal cual: `GET` lista, `POST` para abrir, `PATCH
.../cerrar` o similar para "retirar" a alguien, sin `DELETE`;
(b) un molde más simple — `GET`, `POST`, `DELETE` — porque no hay
ningún dato temporal (`fecha_fin`, `estado`) que "cerrar": la fila
entera es el hecho completo.

**Decisión: (b).** D16 existe porque `vinculacion_asistente` y
`programa_asistente` modelan **vigencias** — intervalos de tiempo que,
una vez cerrados, son historia que no se borra (borrar un período de
vinculación pasado sería reescribir el pasado). `Ponencia` no tiene esa
forma: no existe un "desde / hasta" de ser ponente de una sesión — o
la fila existe (el asistente participó, con ese rol) o no existe. Quitar
una fila de `Ponencia` no reescribe ningún histórico: corrige quién
quedó registrado, igual que borrar una fila de `AliasPrograma` (v2) no
reescribe nada. Forzar aquí el molde de D16 (inventar una acción
`/retirar` en vez de un `DELETE`) habría sido fidelidad a la forma de
D16 sin razón de fondo — D16 se adoptó por lo que `vigencia` significa,
no por "toda llave compuesta sin `id` sigue ese patrón".

**Consecuencias.** (+) El molde se queda tan simple como el problema lo
permite: 4 verbos (`GET` lista, `GET` puntual, `POST`, `DELETE`), sin
inventar una ruta de acción que no corresponde a nada real. (+) Deja
claro, por contraste, qué distingue a D16 de un caso simple de llave
compuesta: la vigencia, no la ausencia de `id`. (−) El proyecto ahora
tiene dos formas distintas de resolver "tabla sin columna `id`"
(D16 y D19) — aceptado porque la intención de negocio detrás de cada
una es realmente distinta, y una tercera tabla futura con llave
compuesta deberá decidir cuál de las dos le corresponde, no copiar la
más reciente por costumbre.

## D20 — La numeración consecutiva de `sesion` no se calcula en C# (extensión de D9)

**Contexto.** `numero_reunion` debe ser consecutivo por `fk_catedra`
(1, 2, 3, ... para las altas nuevas; el trigger no rellena huecos
viejos, ver criterio 2) y la BD ya resuelve esto con un trigger
`BEFORE INSERT` (`fn_trg_sesion_consecutivo`) que, si el valor entrante
es `NULL` o `0`, calcula `MAX(numero_reunion) + 1` para esa cátedra —
bloqueando la fila de `catedra` (`FOR UPDATE`) mientras lo hace, para
que dos altas simultáneas de la misma cátedra no reciban el mismo
número.

**Opciones evaluadas:**
(a) el servicio C# calcula el siguiente número (`SELECT MAX(...) + 1`
antes del `INSERT`) y lo manda ya resuelto;
(b) el servicio no calcula nada: manda lo que el cliente dio (incluido
`null`/`0`) y confía en el trigger.

**Decisión: (b).** (a) duplicaría una regla que la BD ya resuelve
**de forma más segura** que lo que el servicio podría hacer sin su
propio bloqueo explícito: un `SELECT MAX(...)` desde C# seguido de un
`INSERT` por separado tiene una condición de carrera real (dos
peticiones simultáneas podrían leer el mismo `MAX` antes de que
cualquiera inserte) que el trigger evita con el `FOR UPDATE` sobre
`catedra`. Reimplementar la regla en C# no solo sería trabajo de más:
sería una versión **peor** de una regla que ya existe. Extiende el
principio de D9 (no revalidar en C# lo que la BD ya garantiza) a una
regla de negocio real, no solo a una restricción de integridad.

**Consecuencias.** (+) Cero código de concurrencia en C# para este
caso — el servicio es una línea de paso. (+) El comportamiento es
correcto incluso bajo carga simultánea real (dos administradores
creando sesiones de la misma cátedra al mismo tiempo), algo que v4 no
tendría forma barata de garantizar por su cuenta. (−) La "regla de
negocio" de la numeración no es visible leyendo solo el código C# — hay
que saber que vive en el trigger (se documenta aquí y en RF126/RNF10
de [2_spec.md](2_spec.md) exactamente para que no se busque en el lugar
equivocado).

## D21 — Fechas con hora: la petición acepta zona, el modelo guarda UTC

**Contexto.** `sesion.inicio` y `sesion.fin` son las primeras columnas
`TIMESTAMPTZ` que la API **escribe** (hasta v3 solo se leían
`creado_en`/`registrado_en`, que pone la BD). El cliente piensa en hora
de Colombia ("la sesión es a las 14:00"); la BD guarda un instante.

**Opciones evaluadas:**
(a) `DateTimeOffset` de punta a punta (petición, modelo, parámetro de
Dapper);
(b) `DateTime` sin zona (`Kind = Unspecified`) y "se entiende que es hora
de Colombia";
(c) `DateTimeOffset?` solo en la **petición** (acepta `-05:00`) y
`DateTime` en **UTC** en el modelo, como ya es `CreadoEn` desde la v2.

**Decisión: (c).** Revisado en el código fuente de las versiones que usa
el proyecto: Dapper 2.1.66 mapea `DateTimeOffset` a
`DbType.DateTimeOffset`, y Npgsql 9 **rechaza** escribir un
`DateTimeOffset` con desfase distinto de cero en `timestamptz` ("only
offset 0 (UTC) is supported") — (a) fallaría con cualquier hora de
Colombia. (b) también falla: Npgsql rechaza un `DateTime` sin `Kind`
para `timestamptz`, y además pierde el dato de la zona. Con (c), Dapper
no fija `DbType` para `DateTime` y Npgsql infiere `timestamptz` para
`Kind = Utc`: escribe sin conversiones. Leer ya funcionaba igual desde
la v2 (Npgsql devuelve `timestamptz` como `DateTime` UTC).

**Consecuencias.** (+) Un único criterio para todas las fechas con hora
del proyecto: UTC adentro, zona solo en la frontera. (+) El cliente puede
mandar la hora "como la piensa" (`14:00-05:00`) sin hacer cuentas.
(−) Las respuestas salen en UTC (`19:00Z`): quien muestra (el front)
convierte a hora de Colombia — `servicios/fechas.py`, con desfase fijo
UTC-5 porque Colombia no tiene horario de verano. (−) Un cliente que
mande la hora **sin** zona (`"2026-10-20T14:00:00"`) obtiene la zona del
servidor (UTC en el contenedor): el contrato pide siempre el desfase.

