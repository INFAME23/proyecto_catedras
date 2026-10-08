# Especificación — Versión 4 del proyecto: sesiones y ponencias

> ⚠️ **BORRADOR.** Escrito mientras V2 y V3 todavía no tienen su tag. El
> Artículo 1 de la constitución dice que la spec siguiente se escribe
> **después** de cerrar la versión en curso con su tag. Este documento
> se revisa una vez más contra lo que realmente quedó en V3 antes de
> considerarse spec oficial — puede cambiar.
>
> Rige la constitución del proyecto:
> [../1_constitution.md](../1_constitution.md) y la
> [guía operativa para la IA](../../guia_ia.md). Mapa completo de
> versiones: [../0_mapa_versiones.md](../0_mapa_versiones.md).
>
> En v4 el sistema ES esto: la API agrega las **2 tablas** más pequeñas
> del mapa de versiones: `sesion` (una reunión concreta de una cátedra,
> en una fecha y modalidad) y `ponencia` (quién participó en esa
> reunión como ponente, moderador, invitado u organizador). Con esto
> queda resuelto "¿cuándo y dónde se reunió cada cátedra, y quién
> habló?" — el calendario real sobre el que, en v5, se registrará la
> asistencia.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v4 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: capas de la API, sin SP nuevo esta vez |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional)* |
> | [5_data_model.md](5_data_model.md) | Las 2 tablas de esta versión, documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test de v4 |
> | [8_tasks.md](8_tasks.md) | Orden de construcción |

---

## 1. Propósito de la v4

V1-V3 dejaron catálogos, estructura académica y personas, pero ninguna
cátedra tenía todavía una reunión concreta en el calendario. V4 agrega
**cuándo** — la `sesion` — y **quién participó en ella como ponente**
— la `ponencia`. Es la versión más pequeña del mapa (2 tablas, ninguna
con detalle maestro-detalle nuevo), porque su función es puramente
estructural: sin `sesion`, v5 no tiene dónde registrar un `QR` o un
enlace, y sin `ponencia`, no hay forma de distinguir a quien dictó la
cátedra de quien solo asistió.

```
catedra (v2) ──< sesion >── modalidad, sede, periodo_academico (v1)
                   │  │
                   │  └──> encuesta (v1, opcional — la encuesta que se
                   │        aplicará al cierre de esa sesión; v6 la usa)
                   │
                   └──< ponencia >── asistente (v3)
```

A diferencia de v2 y v3, **v4 no agrega ningún SP nuevo** (Guía IA,
Regla 1 no aplica aquí: ninguna operación de v4 crea un maestro y sus
detalles en la misma petición — `sesion` y `ponencia` se crean cada una
por su cuenta, con un `INSERT` sencillo). Lo único no trivial de esta
versión es que la BD, no la API, calcula automáticamente el número de
reunión consecutivo de cada cátedra (ver RF126 y ADR D20).

## 2. Alcance

**Incluye:**
- CRUD completo de `Sesion` (6 verbos, molde estándar).
- `Ponencia`: tabla de **llave compuesta sin columna `id`**
  (`fk_sesion, fk_asistente, rol`), pero **sin** el patrón "vigencia" de
  v3 (ADR D16) — ver ADR **D19**: aquí no hay historial que cerrar,
  solo una participación que existe o no existe, así que el molde es
  `GET`/`POST`/`DELETE` (sin `PUT`/`PATCH`, nada que corregir salvo
  quitar y volver a poner con otro rol).

**No incluye (es de otra versión):**
- `usuario`, `rol_por_usuario`, `enlace_registro`, `clave_acceso`,
  `registro_asistencia` (v5): aunque la BD ya trae, desde el script de
  la v1, **datos de ejemplo sembrados** en `enlace_registro` (9 filas),
  `registro_asistencia` (168 filas) y `lote_migracion` (5 filas) que
  referencian `sesion` — v4 no los toca ni los expone, pero sus FK
  `RESTRICT`/`CASCADE` hacia `sesion` sí se sienten al hacer `DELETE`
  (ver RF124 y el criterio de aceptación 6).
- `pregunta`, `opcion_pregunta`, `respuesta_encuesta`, `respuesta_item`
  (v6): el campo `fk_encuesta` de `sesion` ya apunta a `encuesta` (v1,
  ya construida), así que **no hay** aquí ninguna salvedad de FK
  circular como la de `fk_usuario` en v3 — `fk_encuesta` se expone y
  funciona desde ya, simplemente ninguna encuesta se responde todavía.
- `lote_migracion`, `detalle_migracion`, `bitacora`, `parametro` (v7).

## 3. Entidades y requisitos funcionales

Numeración de RF continúa desde v3 (terminó en RF118).

### Sesion

> RF119-RF126. Una reunión concreta de una `catedra` (v2), en una
> `modalidad`, `sede` y `periodo_academico` (v1). `fk_encuesta` es
> opcional (la encuesta que se aplicará al cierre, si aplica).

- **RF119 — Listar** (`GET /api/Sesion`): 200; vacía → 204. Acepta
  `?limite=` y `?fkCatedra=` (para que el front muestre el calendario
  de una cátedra sin descargar las 5714 sesiones de ejemplo).
- **RF120 — Obtener por id** (`GET /api/Sesion/{idSesion}`): 200;
  inexistente → 404.
- **RF121 — Crear** (`POST /api/Sesion` con **SesionCrear**):
  `fk_catedra`, `inicio`, `fin`, `fk_modalidad`, `fk_sede`,
  `fk_periodo` obligatorios. `numero_reunion` **opcional**: ausente o
  `0` → la BD lo calcula automática y consecutivamente para esa
  `fk_catedra` (ver RF126); un valor explícito mayor a 0 se respeta tal
  cual (y compite por la misma restricción `uq_sesion_reunion` si ya
  existe). `numero_reunion_asis`, `titulo`, `lugar`, `enlace_virtual`,
  `fk_encuesta` opcionales. `cupo` opcional, `> 0` si se envía
  (`[Range(1, int.MaxValue)]`, formato — 422). `estado` opcional, lista
  cerrada (`PROGRAMADA`, `ABIERTA`, `CERRADA`, `MIGRADA`), por defecto
  `PROGRAMADA`. Un `fk_modalidad`/`fk_sede`/`fk_periodo`/`fk_catedra`/
  `fk_encuesta` inexistente → 500 (D9, sin revalidar).
- **RF122 — Reemplazar** (`PUT .../{idSesion}` con **SesionReemplazo**):
  igual molde que siempre; inexistente → 404.
- **RF123 — Actualizar parcialmente** (`PATCH .../{idSesion}`): todo
  opcional; body vacío → 400; inexistente → 404.
- **RF124 — Eliminar** (`DELETE .../{idSesion}`): `filasEliminadas`;
  inexistente → 404. `ponencia` (de esta misma versión) y
  `enlace_registro` (v5, **ya sembrada** con 9 filas de ejemplo) son
  `ON DELETE CASCADE` — se borran junto con la sesión, sin aviso en la
  respuesta (mismo patrón oculto que `Catedra` → `sesion` en v2); pero
  `registro_asistencia` (v5, 168 filas) y `lote_migracion` (v7, 5
  filas) son `RESTRICT` — borrar una sesión que ya tiene cualquiera de
  esas filas → 500, y en ese caso la cascada hacia `ponencia`/
  `enlace_registro` **tampoco llega a ejecutarse** (todo o nada, lo
  decide el motor en una sola transacción implícita del `DELETE`).
- **RF125 — `fin` debe ser posterior a `inicio`:** se valida en el
  **servicio** (`ArgumentException` → 400) antes de llegar a la BD —
  mismo criterio que `PeriodoAcademico` en v1
  (`fecha_fin > fecha_ini`), no una novedad de v4. El `CHECK
  chk_sesion_fechas` de la BD es la última muralla si algo se cuela.
- **RF126 — Numeración consecutiva automática (ADR D20):** si el
  cliente no manda `numero_reunion` (o manda `0`), la BD lo calcula ella
  misma como `MAX(numero_reunion) + 1` para esa `fk_catedra`, mediante
  un trigger (`fn_trg_sesion_consecutivo`) que además bloquea la fila de
  `catedra` (`FOR UPDATE`) mientras calcula, para que dos altas
  simultáneas de la misma cátedra no obtengan el mismo número. La API
  **no reimplementa** este cálculo en C# (extensión de D9: la BD ya
  resuelve esta regla, y resolverla además en el servicio duplicaría la
  lógica y podría desincronizarse de la BD bajo concurrencia real).

### Ponencia

> RF127-RF131. Ver ADR **D19**. Llave primaria compuesta
> `(fk_sesion, fk_asistente, rol)`, sin columna `id`. A diferencia de
> `VinculacionAsistente`/`ProgramaAsistente` (v3, ADR D16), esta tabla
> **no modela un historial de vigencias** — un `asistente` o es
> ponente de una sesión (con un rol) o no lo es; no hay "fecha de
> cierre" que proteger. Por eso el molde aquí **sí admite `DELETE`**
> (quitar una participación no reescribe ningún hecho pasado, solo
> corrige quién quedó registrado).

- **RF127 — Listar** (`GET /api/Ponencia?fkSesion=&fkAsistente=`):
  ambos filtros opcionales y combinables; sin ninguno, todas hasta el
  límite.
- **RF128 — Obtener una participación puntual**
  (`GET /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}`): 200 si existe
  esa combinación exacta; inexistente → 404. Ruta con 3 segmentos
  porque la llave tiene 3 columnas — no hay una sola que la identifique
  (mismo espíritu que D16, aplicado distinto porque aquí sí hace falta
  un `GET` puntual para que el front confirme una asignación antes de
  ofrecer "quitar").
- **RF129 — Asignar** (`POST /api/Ponencia` con **PonenciaCrear**):
  `fk_sesion`, `fk_asistente` obligatorios; `rol` opcional, lista
  cerrada (`PONENTE`, `MODERADOR`, `INVITADO`, `ORGANIZADOR`), por
  defecto `PONENTE`. La misma combinación exacta ya existente → 500
  (PK duplicada, D9) — asignar el mismo asistente con el mismo rol dos
  veces no tiene sentido de negocio; asignarlo con un rol **distinto**
  sí es una fila nueva y válida (un asistente puede ser `PONENTE` y
  `MODERADOR` de la misma sesión, en dos filas).
- **RF130 — Desasignar**
  (`DELETE /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}`):
  `filasEliminadas`; inexistente → 404. **Sin** `PUT`/`PATCH`: no hay
  ninguna columna editable fuera de la llave misma (cambiar el rol de
  alguien es, en los hechos, quitarlo y volver a ponerlo con el rol
  nuevo — son filas distintas, no una edición).
- **RF131 — FK `RESTRICT` hacia `asistente`:** `DELETE
  /api/Asistente/{id}` sobre un asistente que ya es ponente de alguna
  sesión → 500 (`fk_ponencia_asistente`), sin que la cascada de v3
  hacia `documento_asistente`/`vinculacion_asistente`/
  `programa_asistente`/`consentimiento_datos` llegue a ejecutarse —
  completa, con un caso concreto, lo que RF100 de v3 ya anticipaba en
  general ("`ponencia` ... de v4, **ya sembradas**" — en los datos de
  ejemplo esa tabla está vacía hasta que v4 la pueble, así que el
  criterio de aceptación de v3 sobre `RESTRICT` usó `programa_asistente`
  como ejemplo disponible; este es el mismo mecanismo, ahora con
  `ponencia`).

## 4. Requisitos no funcionales

Rigen los mismos RNF1-RNF9 de v1-v3, sin cambios. Se añade:

- **RNF10 — Reglas que la BD ya resuelve no se duplican en C# (D9,
  extendido):** la numeración consecutiva de `numero_reunion` (RF126)
  es, en v4, el primer caso del proyecto donde una regla de negocio
  real (no solo una restricción de integridad) vive **enteramente** en
  un trigger. El servicio no intenta "adivinar" el siguiente número
  antes de insertar ni lo recalcula después — confía en lo que la fila
  insertada trae de vuelta.

## 5. Criterios de aceptación

1. `docker compose up -d --build` deja los 24 recursos (22 de v1-v3 + 2
   de v4) respondiendo en `/swagger`.
2. `GET /api/Sesion?fkCatedra=3975` devuelve las 59 sesiones de ejemplo
   de esa cátedra, cada una con `numero_reunion` entre 1 y 59 sin
   huecos ni repetidos.
3. `POST /api/Sesion` sin `numero_reunion` sobre una cátedra **sin**
   sesiones de ejemplo (p. ej. `fk_catedra: 5498`) → 200 con
   `numero_reunion: 1`; un segundo `POST` igual sobre la misma cátedra
   → `numero_reunion: 2`, sin que el cliente haya calculado nada.
4. `POST /api/Sesion` con `fin` anterior a `inicio` → 400 (del
   servicio, sin tocar la BD).
5. `POST /api/Ponencia` asigna un asistente como `PONENTE` de una
   sesión → 200; el mismo `POST` repetido (misma sesión, mismo
   asistente, mismo rol) → 500; el mismo `POST` con `rol: MODERADOR` →
   200 (fila nueva, misma sesión y asistente). `GET
   /api/Ponencia/{fkSesion}/{fkAsistente}/PONENTE` → 200; `DELETE` sobre
   esa misma ruta → `filasEliminadas: 1`; repetir el `GET` → 404.
6. `DELETE /api/Sesion/{id}` sobre una sesión de ejemplo con
   `registro_asistencia` ya sembrado (168 filas en total) → 500; la
   misma prueba sobre una de las 5 sesiones de ejemplo sin ninguna fila
   de `enlace_registro`/`registro_asistencia`/`lote_migracion` (`id_sesion`
   1-5, ver [5_data_model.md](5_data_model.md) §2.1) → 200.
7. **Prueba de capas ampliada:** `dotnet run --project pruebas` cubre
   las 24 entidades sin PostgreSQL, incluida la validación de
   `fin > inicio` y la asignación/desasignación de `Ponencia` con
   repositorios FALSOS.
8. El front muestra, en el detalle de una cátedra, el calendario de sus
   sesiones (fecha, modalidad, estado) y, en el detalle de una sesión,
   la lista de ponentes con su rol.
9. El panel de administración (`/admin`) muestra 2 tarjetas nuevas
   (`Sesion` con CRUD completo; `Ponencia` con asignar/desasignar, sin
   "editar").
