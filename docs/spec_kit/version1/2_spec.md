# Especificación — Versión 1 del proyecto: ApiCatedras + PostgreSQL

> **Versión 1** del desarrollo incremental. Rige la constitución del
> proyecto: [1_constitution.md](1_constitution.md).
> En v1 el sistema ES esto: la API solo conoce las tablas **SIN foreign
> key saliente** del esquema (10 en total), incorporándolas una por una;
> hoy están **construidas** `Sede`, `Encuesta`, `EstadoProceso` y
> `Modalidad`, y **especificadas pero aún sin implementar** las 6
> restantes (`PeriodoAcademico`, `Rol`, `TipoDocumento`, `TipoEvento`,
> `TipoPregunta`, `TipoVinculacion` — ver el bloque "Pendientes de
> implementación" al final de la sección 3). (La BD
> `bdcatedras_postgres_local` sí se crea COMPLETA desde el inicio — es
> infraestructura dada, ver [5_data_model.md](5_data_model.md); lo que
> crece dentro de esta misma versión es la API, entidad por entidad.)
>
> **Frontend:** ya existe un front funcional (`front_catedras/`, Flask +
> Bootstrap con la identidad de la Universidad San Buenaventura Medellín)
> que consume esta API para el CRUD de las 4 entidades construidas. Vive
> como proyecto aparte (no es parte de este documento, que especifica
> `ApiCatedras`) y todavía no está integrado a `docker-compose.yml` — se
> arranca por fuera con `python run.py`.
>
> | Documento de esta versión | Contenido |
> |---|---|
> | **2_spec.md** (este) | QUÉ construir en v1 y sus criterios de aceptación |
> | [3_plan.md](3_plan.md) | CÓMO: stack, estructura y diseño de las capas |
> | [4_research.md](4_research.md) | Decisiones y alternativas *(lectura opcional)* |
> | [5_data_model.md](5_data_model.md) | La BD completa (dada); las 4 tablas construidas y las 6 pendientes, todas documentadas |
> | [6_contracts.md](6_contracts.md) | Contratos de integración (puertos, variables, healthchecks) |
> | [7_quickstart.md](7_quickstart.md) | Arranque y smoke test |
> | [8_tasks.md](8_tasks.md) | Orden de construcción por fases verificables |

---

## 1. Propósito de la v1

Construir, **rebanada vertical por rebanada vertical**, la API de
Cátedras en **C# / ASP.NET Core** contra **un solo motor (PostgreSQL)**
— con la **arquitectura en capas completa desde el primer día**:
controlador → servicio → repositorio, comunicados por **interfaces de
C#**. La primera rebanada fue el CRUD completo de `Sede`; esta misma
versión sigue agregando rebanadas para las demás tablas **que no tienen
ninguna foreign key saliente** (mismo nivel de complejidad que `Sede`):
`Encuesta`, `EstadoProceso` y `Modalidad`.

> **¿Qué es una "rebanada vertical"?** En lugar de construir el sistema por
> capas horizontales ("primero TODOS los repositorios, luego TODOS los
> servicios…" — donde nada funciona hasta el final), se construye un corte
> que **atraviesa todas las capas de arriba a abajo** para UNA funcionalidad.
> Como una rebanada de pastel: un solo corte, pero con todas las capas.
>
> ```
> ┌─────────────────────────── el sistema completo ───────────────────────────┐
> │  CONTROLLER  │ Sede █ │ tabla2    │ tabla3    │ ...las demás (v2+)  │
> │  SERVICIO    │ Sede █ │ tabla2    │ tabla3    │ ...                 │
> │  REPOSITORIO │ Sede █ │ tabla2    │ tabla3    │ ...                 │
> │  BD          │ Sede █ │ table2    │ tabla3    │ ...                 │
> └──────────────┴─────▲──────┴────────────┴────────────┴─────────────────────┘
>                      └── la v1 ES esta rebanada: funciona de punta a punta
> ```
>
> Ventaja: algo funciona **desde la v1** y la arquitectura queda validada —
> si las capas encajan para `Sede`, las siguientes rebanadas (v2+) caen
> en surcos ya hechos.

La v1 es pequeña a propósito: su valor no está en la funcionalidad sino en
dejar el **esqueleto arquitectónico correcto** sobre el que las versiones
siguientes agregan tablas (v2+) y, más adelante, otros motores o un
frontend, **sin reescribir lo construido**.

## 2. Alcance

**Incluye:**
- CRUD de `Sede`, `Encuesta`, `EstadoProceso` y `Modalidad`: listar,
  obtener por identificador, crear, reemplazar, actualizar parcialmente,
  eliminar — las mismas 6 operaciones para cada una.
- **Modelo entidad** por cada tabla (`Sede`, `Encuesta`, `EstadoProceso`,
  `Modalidad`): la clase con las propiedades tipadas (en C#, las
  propiedades `{ get; set; }` SON los getters/setters del lenguaje).
- **Una petición por verbo como frontera de entrada**, por cada entidad
  (p. ej. `EncuestaCrear`, `EncuestaReemplazo`, `EncuestaActualizar`):
  declaran sus reglas con anotaciones (`[Required]`, `[StringLength]`,
  `[Range]`) y ASP.NET valida el body contra ellas → **422 con lista de
  errores** antes de tocar el controlador.
- Capas con interfaces: `IRepositorioXxx` implementada por
  `RepositorioXxxPostgres` (Dapper: SQL a mano) para cada entidad; el
  servicio depende de la interfaz.
- Configuración por `appsettings.json`, sobrescribible por variables de
  entorno (`ConnectionStrings__Postgres`) — la vía natural en Docker.
- **Un solo comando** (Artículo 4): `docker-compose.yml` con PostgreSQL +
  su inicializador + la API, de modo que `docker compose up -d --build`
  deja todo funcionando.
- Endpoint `/` de diagnóstico y **documentación interactiva Swagger** en
  `/swagger` (Swashbuckle): los endpoints se ven y se prueban desde el
  navegador.

**No incluye todavía (especificado, pendiente del próximo commit):**
- El **código** (Modelos/Peticiones/Repositorios/Servicios/Controllers) de
  `PeriodoAcademico`, `Rol`, `TipoDocumento`, `TipoEvento`, `TipoPregunta`
  y `TipoVinculacion` — sus requisitos funcionales SÍ quedan escritos en
  esta versión del documento (ver el bloque "Pendientes de
  implementación" en la sección 3), precisamente para que el próximo
  commit los use como guía al construirlos.

**No incluye (y es deliberado):**
- Pantallas nuevas en `front_catedras` para las 6 tablas de arriba —
  llegan junto con su API.
- Endpoints para las tablas que SÍ tienen alguna foreign key saliente
  (`catedra`, `sesion`, `asistente`, etc.) — quedan para cuando la
  especificación lo pida explícitamente.
- Otros motores y la fábrica de repositorios.
- ORM de entidades (Entity Framework) y autenticación — no son de esta
  versión (Dapper NO es ORM de entidades: es el micro-ejecutor del Art. 2).

## 3. Requisitos funcionales

> La v1 usa **los cinco verbos HTTP** (GET, POST, PUT, PATCH, DELETE) y las
> **tres vías de envío de datos**: parámetro de ruta (`/{idSede}`), query
> string (`?limite=N`) y body JSON. Es parte del objetivo didáctico.

### RF1 — Listar Sede (GET + query string)
`GET /api/Sede` → 200 con envoltura `{tabla, limite, total, datos:[…]}`.
- Query param opcional `limite` (entero > 0, por defecto 1000).
- Tabla vacía → **204** sin cuerpo.

### RF2 — Obtener por código (GET + parámetro de ruta)
`GET /api/Sede/{idSede}` → 200 con la Sede; inexistente → 404.

### RF3 — Crear Sede (POST + body)
`POST /api/Sede` con body validado por la petición **SedeCrear**
(`id_sede` 1–15 caracteres, `nombre` 1–80 caracteres, `direccion` opcional
hasta 200 caracteres, `es_virtual` y `activo` booleanos obligatorios).
Éxito → 200 `{estado, mensaje}`; body inválido → **422 con la lista de
errores**; código o nombre duplicado → 500 con el error del motor en
`detalle`.

### RF4 — Reemplazar Sede (PUT + body completo)
`PUT /api/Sede/{idSede}` con body de la petición **SedeReemplazo**:
**todos los campos obligatorios** (`nombre`, `es_virtual`, `activo`;
`direccion` admite `null`) — PUT reemplaza el recurso completo; omitir un
campo obligatorio es 422, no "dejarlo como estaba". Devuelve
`filasAfectadas`; código inexistente → 404.

### RF5 — Actualizar parcialmente (PATCH + body parcial)
`PATCH /api/Sede/{idSede}` con body de la petición **SedeActualizar**:
**campos opcionales** — solo se modifican los enviados (cada uno validado
si llega). Es el contraste didáctico con PUT. Devuelve `filasAfectadas`;
inexistente → 404; body vacío → 400.

### RF6 — Eliminar Sede (DELETE)
`DELETE /api/Sede/{idSede}`. Devuelve `filasEliminadas`;
inexistente → 404.

### RF7 — Diagnóstico
`GET /` → JSON con mensaje, versión (`"v1"`) y la ruta de los contratos.

---

### Encuesta

> **Nota de diseño:** a diferencia de `Sede`, `id_encuesta` **NO lo manda
> el cliente** — la columna es `GENERATED ALWAYS AS IDENTITY` (la BD la
> autogenera). Por eso `EncuestaCrear` no tiene ese campo, el repositorio
> inserta con `INSERT ... RETURNING id_encuesta` y la respuesta del POST
> incluye el id generado. El parámetro de ruta `{idEncuesta}` es numérico
> (`long`), no texto.
>
> **Nota de diseño (regla de coherencia de fechas):** la tabla exige
> `vigente_hasta IS NULL OR vigente_hasta >= vigente_desde`
> (`chk_encuesta_vig`). El servicio la valida como regla de negocio
> (`ArgumentException` → 400) **cuando tiene las dos fechas en la mano**:
> siempre en POST y PUT (una llega o toma su valor por defecto, la otra es
> la enviada), y en PATCH solo si el cliente manda las dos en la misma
> petición. Si un PATCH cambia una sola fecha, la coherencia con el valor
> ya guardado la vigila el CHECK de la BD (violación → 500) — igual que
> con cualquier restricción que la API no duplica.

### RF8 — Listar Encuesta (GET + query string)
`GET /api/Encuesta` → 200 `{tabla, limite, total, datos:[…]}`; `limite`
opcional (entero > 0, por defecto 1000); vacía → 204.

### RF9 — Obtener por id (GET + parámetro de ruta)
`GET /api/Encuesta/{idEncuesta}` → 200 con la Encuesta; inexistente → 404.

### RF10 — Crear Encuesta (POST + body)
`POST /api/Encuesta` con body **EncuestaCrear**: `nombre` obligatorio
(1–120 caracteres), `version` opcional (entero ≥ 1; si no llega, 1),
`vigente_desde` opcional (fecha; si no llega, la fecha del servidor),
`vigente_hasta` opcional (fecha, admite `null`), `activa` opcional
(booleano; si no llega, `true`). Éxito → 200
`{estado, mensaje, id_encuesta}`; body inválido → 422;
`vigente_hasta < vigente_desde` → 400; `(nombre, version)` duplicado
(`uq_encuesta_ver`) → 500.

### RF11 — Reemplazar Encuesta (PUT + body completo)
`PUT /api/Encuesta/{idEncuesta}` con **EncuestaReemplazo**: `nombre`,
`version`, `vigente_desde` y `activa` obligatorios; `vigente_hasta`
obligatorio en el body pero admite `null`. `vigente_hasta < vigente_desde`
→ 400. Devuelve `filasAfectadas`; inexistente → 404.

### RF12 — Actualizar parcialmente Encuesta (PATCH + body parcial)
`PATCH /api/Encuesta/{idEncuesta}` con **EncuestaActualizar**: todos los
campos opcionales; solo se modifican los enviados. Body vacío → 400;
inexistente → 404.

### RF13 — Eliminar Encuesta (DELETE)
`DELETE /api/Encuesta/{idEncuesta}`. Devuelve `filasEliminadas`;
inexistente → 404.

---

### EstadoProceso

> **Nota de diseño (regla de coherencia):** la tabla exige
> `es_exitoso = false OR es_final = true` (`chk_estado_coherente`): un
> estado no puede ser "exitoso" sin ser también "final". El servicio la
> valida (`ArgumentException` → 400) cuando ambos campos llegan juntos en
> la misma petición (siempre en POST/PUT; en PATCH solo si el cliente
> manda los dos). Si un PATCH toca uno solo, la coherencia con el valor ya
> guardado la vigila el CHECK de la BD (violación → 500).

### RF14 — Listar EstadoProceso (GET + query string)
`GET /api/EstadoProceso` → 200 `{tabla, limite, total, datos:[…]}`;
`limite` opcional; vacía → 204.

### RF15 — Obtener por código (GET + parámetro de ruta)
`GET /api/EstadoProceso/{idEstado}` → 200; inexistente → 404.

### RF16 — Crear EstadoProceso (POST + body)
`POST /api/EstadoProceso` con **EstadoProcesoCrear**: `id_estado`
obligatorio (1–12 caracteres), `nombre` obligatorio (1–40 caracteres),
`es_final` y `es_exitoso` opcionales (booleanos; si no llegan, `false`).
Éxito → 200 `{estado, mensaje}`; body inválido → 422; coherencia
`es_exitoso`/`es_final` violada → 400; `id_estado` duplicado → 500.

### RF17 — Reemplazar EstadoProceso (PUT + body completo)
`PUT /api/EstadoProceso/{idEstado}` con **EstadoProcesoReemplazo**:
`nombre`, `es_final`, `es_exitoso` obligatorios. Coherencia violada → 400;
inexistente → 404.

### RF18 — Actualizar parcialmente EstadoProceso (PATCH + body parcial)
`PATCH /api/EstadoProceso/{idEstado}` con **EstadoProcesoActualizar**:
todos opcionales. Body vacío → 400; inexistente → 404.

### RF19 — Eliminar EstadoProceso (DELETE)
`DELETE /api/EstadoProceso/{idEstado}`. Devuelve `filasEliminadas`;
inexistente → 404.

---

### Modalidad

> **Nota de diseño:** `canal_difusion` solo admite `QR`, `ENLACE` o
> `AMBOS` (`chk_modalidad_canal`). A diferencia de las reglas de
> coherencia de arriba, esta es una comprobación de **forma** (un valor
> dentro de una lista cerrada, no una relación entre dos campos): se
> valida en la PETICIÓN misma (no en el servicio), con el mismo espíritu
> de "el tipo también es regla".

### RF20 — Listar Modalidad (GET + query string)
`GET /api/Modalidad` → 200 `{tabla, limite, total, datos:[…]}`; `limite`
opcional; vacía → 204.

### RF21 — Obtener por código (GET + parámetro de ruta)
`GET /api/Modalidad/{idModalidad}` → 200; inexistente → 404.

### RF22 — Crear Modalidad (POST + body)
`POST /api/Modalidad` con **ModalidadCrear**: `id_modalidad` obligatorio
(1–15 caracteres), `nombre` obligatorio (1–60 caracteres),
`canal_difusion` obligatorio (`QR`|`ENLACE`|`AMBOS`), `activo` opcional
(booleano; si no llega, `true`). Éxito → 200 `{estado, mensaje}`;
body inválido o `canal_difusion` fuera de la lista → 422; `id_modalidad`
duplicado → 500.

### RF23 — Reemplazar Modalidad (PUT + body completo)
`PUT /api/Modalidad/{idModalidad}` con **ModalidadReemplazo**: `nombre`,
`canal_difusion`, `activo` obligatorios. Inexistente → 404.

### RF24 — Actualizar parcialmente Modalidad (PATCH + body parcial)
`PATCH /api/Modalidad/{idModalidad}` con **ModalidadActualizar**: todos
opcionales; si llega `canal_difusion` se valida contra la misma lista
cerrada. Body vacío → 400; inexistente → 404.

### RF25 — Eliminar Modalidad (DELETE)
`DELETE /api/Modalidad/{idModalidad}`. Devuelve `filasEliminadas`;
inexistente → 404.

---

## Pendientes de implementación (próximo commit de v1)

> Las 6 tablas de aquí abajo completan las **10 tablas sin foreign key
> saliente** del esquema (§2 de [5_data_model.md](5_data_model.md)). Se
> ESPECIFICAN ya (mismo formato y nivel de detalle que las 4 de arriba)
> para que el próximo commit las use como guía al implementarlas — pero
> **el código todavía no existe** (ver `8_tasks.md`, Fase 12).

### PeriodoAcademico

> **Nota de diseño (formato del código):** `id_periodo` es `CHAR(6)` con
> `CHECK (id_periodo ~ '^[0-9]{4}-[1-3]$')` — un año de 4 dígitos, un
> guion y un semestre 1, 2 o 3 (p. ej. `2026-1`). Es una comprobación de
> **forma** (una lista/patrón cerrado), igual que `canal_difusion` de
> `Modalidad`: se valida en la PETICIÓN con `[RegularExpression("^[0-9]{4}-[1-3]$")]`,
> no en el servicio.
>
> **Nota de diseño (coherencia de fechas):** `CHECK (fecha_fin > fecha_ini)`
> — a diferencia de `Encuesta`, aquí NINGUNA de las dos fechas es
> opcional ni tiene `DEFAULT` en la BD, así que **siempre** llegan ambas
> en POST y PUT; el servicio valida `fecha_fin > fecha_ini`
> (`ArgumentException` → 400) siempre que las tiene, y en PATCH solo
> cuando el cliente manda las dos juntas (mismo criterio que en
> `Encuesta`, 3_plan.md §4.8). Reutiliza el mismo `ManejadorTipoDateOnly`
> (Dapper) que ya existe para `Encuesta` — no hay que crear otro.

- **RF26 — Listar** (`GET /api/PeriodoAcademico`): 200
  `{tabla, limite, total, datos}`; `limite` opcional; vacía → 204.
- **RF27 — Obtener por código** (`GET /api/PeriodoAcademico/{idPeriodo}`):
  200; inexistente → 404.
- **RF28 — Crear** (`POST /api/PeriodoAcademico` con
  **PeriodoAcademicoCrear**): `id_periodo` obligatorio (formato
  `AAAA-N`, N∈{1,2,3}), `fecha_ini` y `fecha_fin` obligatorias,
  `activo` opcional (por defecto `true`). `fecha_fin <= fecha_ini` → 400;
  formato de `id_periodo` inválido → 422; duplicado → 500.
- **RF29 — Reemplazar** (`PUT /api/PeriodoAcademico/{idPeriodo}` con
  **PeriodoAcademicoReemplazo**): `fecha_ini`, `fecha_fin`, `activo`
  obligatorios. Coherencia violada → 400; inexistente → 404.
- **RF30 — Actualizar parcialmente** (`PATCH .../{idPeriodo}` con
  **PeriodoAcademicoActualizar**): todos opcionales. Body vacío → 400;
  inexistente → 404.
- **RF31 — Eliminar** (`DELETE .../{idPeriodo}`): `filasEliminadas`;
  inexistente → 404.

### Rol

> Tabla simple, sin reglas de negocio propias — mismo nivel que `Sede`:
> `descripcion` es opcional (admite `null`), igual que `Sede.direccion`.

- **RF32 — Listar** (`GET /api/Rol`): 200 `{tabla, limite, total, datos}`;
  vacía → 204.
- **RF33 — Obtener por código** (`GET /api/Rol/{idRol}`): 200;
  inexistente → 404.
- **RF34 — Crear** (`POST /api/Rol` con **RolCrear**): `id_rol`
  obligatorio (1–20 caracteres), `nombre` obligatorio (1–60), `descripcion`
  opcional (hasta 200, admite ausencia/`null`). `id_rol` o `nombre`
  duplicado (`uq_rol_nombre`) → 500.
- **RF35 — Reemplazar** (`PUT /api/Rol/{idRol}` con **RolReemplazo**):
  `nombre` obligatorio; `descripcion` va en el body y admite `null`.
  Inexistente → 404.
- **RF36 — Actualizar parcialmente** (`PATCH /api/Rol/{idRol}` con
  **RolActualizar**): todo opcional. Body vacío → 400; inexistente → 404.
- **RF37 — Eliminar** (`DELETE /api/Rol/{idRol}`): `filasEliminadas`;
  inexistente → 404.

### TipoDocumento

> Mismo patrón que `Modalidad` sin la lista cerrada: `id_tipo_documento`
> (1–5 caracteres), `nombre` (único, `uq_tipo_documento_nom`), `activo`
> (por defecto `true`).

- **RF38 — Listar** (`GET /api/TipoDocumento`): 200; vacía → 204.
- **RF39 — Obtener por código** (`GET /api/TipoDocumento/{idTipoDocumento}`):
  200; inexistente → 404.
- **RF40 — Crear** (`POST /api/TipoDocumento` con **TipoDocumentoCrear**):
  `id_tipo_documento` obligatorio (1–5), `nombre` obligatorio (1–60),
  `activo` opcional (por defecto `true`). Duplicado → 500.
- **RF41 — Reemplazar** (`PUT .../{idTipoDocumento}` con
  **TipoDocumentoReemplazo**): `nombre` y `activo` obligatorios.
  Inexistente → 404.
- **RF42 — Actualizar parcialmente** (`PATCH .../{idTipoDocumento}` con
  **TipoDocumentoActualizar**): todo opcional. Body vacío → 400;
  inexistente → 404.
- **RF43 — Eliminar** (`DELETE .../{idTipoDocumento}`): `filasEliminadas`;
  inexistente → 404.

### TipoEvento

> **Nota de diseño:** `id_tipo_evento` es `CHAR(4)` — a diferencia de los
> códigos `VARCHAR` de las demás tablas (que aceptan 1 a N caracteres),
> aquí la longitud es **fija**: la petición valida
> `[StringLength(4, MinimumLength = 4)]` (exactamente 4, no "hasta 4").
> Además, a diferencia de `Sede`/`Rol`/`TipoDocumento`, **`nombre` NO es
> único** en esta tabla (no existe `uq_tipo_evento_nom`) — dos tipos de
> evento SÍ pueden compartir nombre; solo `id_tipo_evento` duplicado da
> 500.

- **RF44 — Listar** (`GET /api/TipoEvento`): 200; vacía → 204.
- **RF45 — Obtener por código** (`GET /api/TipoEvento/{idTipoEvento}`):
  200; inexistente → 404.
- **RF46 — Crear** (`POST /api/TipoEvento` con **TipoEventoCrear**):
  `id_tipo_evento` obligatorio (exactamente 4 caracteres), `nombre`
  obligatorio (1–80), `activo` opcional (por defecto `true`).
  `id_tipo_evento` duplicado → 500 (`nombre` repetido SÍ se permite).
- **RF47 — Reemplazar** (`PUT .../{idTipoEvento}` con
  **TipoEventoReemplazo**): `nombre` y `activo` obligatorios.
  Inexistente → 404.
- **RF48 — Actualizar parcialmente** (`PATCH .../{idTipoEvento}` con
  **TipoEventoActualizar**): todo opcional. Body vacío → 400;
  inexistente → 404.
- **RF49 — Eliminar** (`DELETE .../{idTipoEvento}`): `filasEliminadas`;
  inexistente → 404.

### TipoPregunta

> **Nota de diseño (regla de coherencia de TRES campos):** `CHECK
> (guarda_numero::int + guarda_texto::int + guarda_opcion::int = 1)` —
> exactamente UNA de las tres columnas debe ser `true`, nunca cero ni
> dos o más. Es la misma familia de regla que `EstadoProceso`
> (`es_exitoso` → `es_final`, 3_plan.md §4.8), extendida a tres campos en
> vez de dos: el servicio la valida (`ArgumentException` → 400) en
> POST/PUT (donde siempre tiene los tres valores, enviados o por
> defecto `false`) y en PATCH solo cuando el cliente manda **los tres**
> campos juntos en la misma petición — si manda uno o dos, la coherencia
> final la vigila el CHECK de la BD (violación → 500).

- **RF50 — Listar** (`GET /api/TipoPregunta`): 200; vacía → 204.
- **RF51 — Obtener por código** (`GET /api/TipoPregunta/{idTipoPregunta}`):
  200; inexistente → 404.
- **RF52 — Crear** (`POST /api/TipoPregunta` con **TipoPreguntaCrear**):
  `id_tipo_pregunta` obligatorio (1–15), `nombre` obligatorio (1–60),
  `guarda_numero`/`guarda_texto`/`guarda_opcion` opcionales (booleanos;
  si no llegan, `false`, igual que la columna). Exactamente una de las
  tres en `true` tras aplicar los defaults, si no → 400. Duplicado →
  500.
- **RF53 — Reemplazar** (`PUT .../{idTipoPregunta}` con
  **TipoPreguntaReemplazo**): `nombre`, `guarda_numero`, `guarda_texto`,
  `guarda_opcion` obligatorios (mismo criterio que `Sede.EsVirtual`: PUT
  = reemplazo completo, aunque la columna tenga `DEFAULT`). Coherencia
  violada → 400; inexistente → 404.
- **RF54 — Actualizar parcialmente** (`PATCH .../{idTipoPregunta}` con
  **TipoPreguntaActualizar**): todo opcional; coherencia se valida solo
  si llegan los TRES campos juntos. Body vacío → 400; inexistente → 404.
- **RF55 — Eliminar** (`DELETE .../{idTipoPregunta}`): `filasEliminadas`;
  inexistente → 404.

### TipoVinculacion

> **Nota de diseño:** `es_interno` es `BOOLEAN NOT NULL` **sin
> `DEFAULT`** en la BD — a diferencia de `exige_programa` y `activo`
> (ambos `DEFAULT true`), `es_interno` es **obligatorio** en
> `TipoVinculacionCrear` (no tiene valor por defecto que el servicio
> pueda aplicar).

- **RF56 — Listar** (`GET /api/TipoVinculacion`): 200; vacía → 204.
- **RF57 — Obtener por código** (`GET /api/TipoVinculacion/{idTipoVinculacion}`):
  200; inexistente → 404.
- **RF58 — Crear** (`POST /api/TipoVinculacion` con
  **TipoVinculacionCrear**): `id_tipo_vinculacion` obligatorio (1–15),
  `nombre` obligatorio (1–60), `es_interno` **obligatorio** (booleano,
  sin default), `exige_programa` opcional (por defecto `true`), `activo`
  opcional (por defecto `true`). Duplicado (`id` o `uq_tipo_vinculacion_nom`)
  → 500.
- **RF59 — Reemplazar** (`PUT .../{idTipoVinculacion}` con
  **TipoVinculacionReemplazo**): `nombre`, `es_interno`,
  `exige_programa`, `activo` obligatorios. Inexistente → 404.
- **RF60 — Actualizar parcialmente** (`PATCH .../{idTipoVinculacion}` con
  **TipoVinculacionActualizar**): todo opcional. Body vacío → 400;
  inexistente → 404.
- **RF61 — Eliminar** (`DELETE .../{idTipoVinculacion}`):
  `filasEliminadas`; inexistente → 404.

## 4. Requisitos no funcionales

- **RNF1 — Capas estrictas:** el controlador no toca SQL; el servicio no
  conoce HTTP ni el motor; el repositorio no conoce HTTP. Contratos con
  `interface` de C#.
- **RNF2 — SQL a la vista:** el SQL se escribe a mano y Dapper solo lo
  ejecuta y mapea (sin Entity Framework); paquetes: `Npgsql`, `Dapper`
  y `Swashbuckle.AspNetCore` (Artículo 2).
- **RNF3 — SQL SIEMPRE parametrizado** (`@parametro`); nada de concatenar
  valores.
- **RNF4 — Asíncrona:** todo el acceso a datos con `async/await`.
- **RNF5 — Errores uniformes:** `{estado, mensaje, detalle}` (y
  `errores:[…]` en el 422); `ArgumentException`→400 ·
  `NoEncontradoExcepcion`→404 · `NpgsqlException` y demás→500.
- **RNF6 — Sin anticipación:** ni fábrica multi-motor ni selección de motor
  en v1 (se introducen cuando exista un segundo motor real).

## 5. Criterios de aceptación

1. **`docker compose up -d --build` — un solo comando —** deja corriendo
   PostgreSQL (inicializado con el script provisto: esquema `catedras`
   con 37 tablas), y la API; `GET http://localhost:8142/` responde el
   JSON de diagnóstico. Guardar un `.cs` recompila y reinicia solo
   (`dotnet watch`).
2. `GET /api/Sede` devuelve las 3 sedes de ejemplo del script
   (`SAN_BENITO`, `BELLO`, `VIRTUAL`) con `{tabla:"sede", total:3,
   datos:[…]}`, y `GET /api/Sede?limite=2` devuelve exactamente 2.
3. `GET /api/Sede/SAN_BENITO` devuelve el Campus San Benito;
   `GET /api/Sede/NO_EXISTE` responde 404 con mensaje claro.
4. Ciclo completo con los 5 verbos: `POST` crea una sede nueva → `PUT` la
   reemplaza completo → `PATCH` le cambia solo `activo` → `GET` lo
   confirma → `DELETE` la elimina, y un segundo `DELETE` responde 404.
   Además, un `PUT` sin el campo `nombre` responde 422 (reemplazo
   completo) mientras el mismo body en `PATCH` responde 200 (parcial) —
   la diferencia entre ambos verbos.
5. La validación de la petición es la frontera: `POST` sin `nombre` o sin
   `id_sede` → 422 con `errores:[…]`; `POST` con `es_virtual: "texto"` →
   422 (**el tipo también es regla**: la petición declara `bool?`);
   `id_sede` o `nombre` duplicado → 500 con el error del motor en
   `detalle`.
6. **Prueba de capas:** `dotnet run --project pruebas` ejecuta el servicio
   con un repositorio FALSO en memoria — sin PostgreSQL — y todas las
   verificaciones pasan (incluye ahora también `Encuesta`, `EstadoProceso`
   y `Modalidad`).
7. `GET /api/Encuesta` devuelve la encuesta de ejemplo del script
   ("Calidad de la catedra abierta", versión 1) con `total:1`; un `POST`
   sin `nombre` → 422; un `POST` con `vigente_hasta` anterior a
   `vigente_desde` → 400 (regla de negocio, no llega a la BD); el `POST`
   exitoso devuelve el `id_encuesta` generado por la BD.
8. `GET /api/EstadoProceso` devuelve los 6 estados de ejemplo
   (`GENERADO`, `EN_COLA`, `EN_CURSO`, `ERROR`, `CORRECTO`, `INCORRECTO`);
   un `POST` con `es_exitoso: true` y `es_final: false` → 400 (viola la
   regla de coherencia antes de tocar la BD).
9. `GET /api/Modalidad` devuelve las 4 modalidades de ejemplo
   (`PRESENCIAL`, `VIRTUAL`, `TELEPRESENCIAL`, `HIBRIDA`); un `POST` con
   `canal_difusion: "FAX"` → 422 (valor fuera de la lista cerrada,
   detectado en la petición).
