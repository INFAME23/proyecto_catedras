# Proyecto Cátedras — Versión 4

API REST del sistema de **asistencia a cátedras universitarias**, construida
por **versiones incrementales**. Esta es la **v4**: las **sesiones** (cada
reunión concreta de una cátedra, con fecha y hora) y las **ponencias**
(quién la dictó y con qué rol), sobre las personas de la v3, la estructura
académica de la v2 y la arquitectura en capas de la v1.

## Qué incluye la v4

- Lo nuevo que enseña la v4:
  - **Una regla de negocio que vive en la BD** (ADR D20, RNF10): el número
    de reunión consecutivo lo pone el trigger `fn_trg_sesion_consecutivo`.
    `POST /api/Sesion` sin `numero_reunion` → la BD responde `1`, `2`, …
    El C# no hace ningún `SELECT MAX(...)`: lo leería con una condición de
    carrera que el trigger evita con `FOR UPDATE`.
  - **Fechas con hora** (ADR D21): `inicio`/`fin` entran con zona
    (`-05:00`), se guardan y se devuelven en UTC (`...Z`); el front las
    muestra en hora de Colombia.
  - **Llave compuesta SIN vigencia** (ADR D19): `Ponencia` no tiene `id`
    y su llave va en 3 segmentos de ruta
    (`/api/Ponencia/{sesion}/{asistente}/{rol}`); a diferencia de las de la
    v3, **sí** se borra con `DELETE` y no tiene `PUT`/`PATCH`.
  - **Integridad que se siente desde tablas que aún no existen en la API**:
    `DELETE /api/Sesion/5712` → 500 por `registro_asistencia` (v5) y
    `lote_migracion` (v7), ya sembradas.
  - Sin SP ni migración nueva: ninguna operación de la v4 es maestro-detalle.
- Front (Flask): calendario de sesiones en el detalle de cada cátedra,
  detalle de sesión con sus ponentes (buscar → elegir → asignar / quitar),
  **calendario del ponente** (`/ponente/`) y, en `/admin`, el CRUD de
  `Sesion` y la asignación de `Ponencia`.
- Las **24 tablas** que hoy expone la API:
  - las 10 sin foreign key de la v1 (`Sede`, `Encuesta`, `EstadoProceso`,
    `Modalidad`, `PeriodoAcademico`, `Rol`, `TipoDocumento`, `TipoEvento`,
    `TipoPregunta`, `TipoVinculacion`);
  - las 5 de la v2: `Facultad`, `Dependencia`, `ProgramaAcademico`,
    `AliasPrograma` y `Catedra`;
  - las 7 de la v3: `Asistente`, `DocumentoAsistente`,
    `VinculacionAsistente`, `ProgramaAsistente`, `ConsentimientoDatos`,
    `LoteCargaAsistente` y `NovedadCarga`;
  - las 2 de la v4: `Sesion` y `Ponencia`.
- Lo que enseñó la v3 (sigue vigente):
  - **Maestro-detalle con 4 detalles** (Guía IA, Regla 1): `POST
    /api/Asistente` guarda la persona con sus documentos, vinculaciones,
    programas y consentimiento en **una sola transacción**
    (`sp_guardar_asistente`).
  - **Búsqueda y paginación en SQL** (`GET /api/Asistente?q=&pagina=`): con
    14.811 personas, el front nunca descarga la tabla completa (ADR D18).
  - **Llave compuesta** (`VinculacionAsistente`, `ProgramaAsistente`): sin
    `/{id}`; se abren y se **cierran** / **cambian de estado** (ADR D16).
  - **Índice único parcial** (`ux_documento_vigente`): un solo documento
    vigente por persona.
  - **Hecho legal inmutable** (`ConsentimientoDatos`, Ley 1581): solo se
    registra.
  - **Carga parcial con evidencia** (ADR D15): cada fila del archivo es su
    propia transacción; el lote se cierra con sus contadores y una novedad
    por fila rechazada (`sp_guardar_lote_carga`).
- Front (Flask): listado buscable de asistentes y su ficha (con sus 4
  detalles), las 7 tablas en el panel de administración y la pantalla de
  **carga de datos** (`/carga_datos`, archivos `.xlsx` o `.csv`
  interpretados en Python — ADR D17).

## Stack

| Pieza | Elección |
|---|---|
| Lenguaje / framework | C# · ASP.NET Core (.NET 10) |
| Acceso a datos | Dapper sobre Npgsql (SQL a mano, siempre `@parametro`) |
| Documentación | Swashbuckle.AspNetCore (Swagger) |
| Motor | PostgreSQL 16 (contenedor oficial) |
| Front | Flask + Bootstrap 5 (identidad USB) · `openpyxl` para leer Excel |
| Orquestación | Docker Compose |

Paquetes NuGet: `Npgsql`, `Dapper`, `Swashbuckle.AspNetCore` — y ninguno más
(Artículo 2 de la constitución). La v4 no agrega paquetes ni a la API ni
al front.

## Cómo levantarlo

```powershell
docker compose up -d --build
```

| Recurso | URL |
|---|---|
| Diagnóstico | http://localhost:8142/ |
| Swagger UI | http://localhost:8142/swagger |
| API (ejemplo v4) | http://localhost:8142/api/Sesion?fkCatedra=3975 |
| Front · calendario de una cátedra | http://localhost:5000/catedras/3975 |
| Front · detalle de sesión y ponentes | http://localhost:5000/sesiones/1 |
| Front · calendario del ponente | http://localhost:5000/ponente/ |
| Front · administración | http://localhost:5000/admin/ |
| Front · carga de datos | http://localhost:5000/carga_datos |
| PostgreSQL (herramienta externa) | localhost,15542 · base `bdcatedras_postgres_local` · `sa` / `Construccion123!` |

> **¿Vienes de la v3 con el volumen de PostgreSQL ya creado?** La v4 no
> agrega ninguna migración: no hay nada que reaplicar. (Si vienes de la v2
> o antes, mira `docs/spec_kit/Versiones/version3/7_quickstart.md` §1.)

Prueba de capas (sin PostgreSQL, 24 entidades, 256 verificaciones esperadas). Se
corre dentro del contenedor de la API (en Windows, el Control de
aplicaciones puede bloquear la DLL recién compilada):

```powershell
docker compose exec api dotnet run --project pruebas
```

> Las pruebas del quickstart agregan y modifican datos de ejemplo. Para
> volver al estado original: `docker compose down -v && docker compose up -d --build`.

## Estructura

```
docker-compose.yml              # un comando: postgres + api + front
db/postgres/
  script_bdcatedras.sql         # la BD completa, PROVISTA (se monta, no se genera)
  migraciones/                  # SP maestro-detalle PROPIOS (Guía IA, Regla 1):
                                #  001 programa (v2) · 002 asistente · 003 lote de carga (v3)
api_catedras/                   # la API .NET (Controllers, Modelos, Peticiones,
                                #  Servicios, Repositorios, Excepciones, pruebas)
front_catedras/                 # el front Flask (routes/carga.py + servicios/carga_asistentes.py: carga)
docs/
  GUIA_ESTUDIANTE.md            # guía de arranque (v1)
  spec_kit/1_constitution.md    # reglas permanentes de todas las versiones
  spec_kit/guia_ia.md           # reglas técnicas transversales (SP, <select> de FK, BCrypt)
  spec_kit/Versiones/
    0_mapa_versiones.md         # qué tablas entran en cada versión
    version1/  2_spec .. 8_tasks
    version2/  2_spec .. 8_tasks
    version3/  2_spec .. 8_tasks
    version4/  2_spec .. 8_tasks + guia_ia_v4.md (guía para construir/verificar la v4 con IA)
```

## Documentación de la v4

En `docs/spec_kit/Versiones/version4/`: `2_spec.md` (qué y criterios de
aceptación, RF119-RF131), `3_plan.md` (cómo), `4_research.md` (decisiones
D19-D21), `5_data_model.md` (las 2 tablas, sus FK y los datos de ejemplo
útiles para probar), `6_contracts.md`, `7_quickstart.md` (smoke test),
`8_tasks.md` (fases 32-37) y **`guia_ia_v4.md`** (cómo trabajar esta
versión con un asistente de IA: orden de lectura, prompts por fase y
trampas conocidas).

**Estado:** código de la v4 completo; el SQL y el front se verificaron
contra la BD real. Falta correr en Docker la prueba de capas y el smoke
test (`7_quickstart.md`) y poner el tag `v4` (Fase 37 de `8_tasks.md`).
