# Proyecto Cátedras — Versión 3

API REST del sistema de **asistencia a cátedras universitarias**, construida
por **versiones incrementales**. Esta es la **v3**: las **personas** (el
asistente y sus datos) y la **carga masiva de asistentes**, sobre la
estructura académica de la v2 y la arquitectura en capas de la v1.

## Qué incluye la v3

- Las **22 tablas** que hoy expone la API:
  - las 10 sin foreign key de la v1 (`Sede`, `Encuesta`, `EstadoProceso`,
    `Modalidad`, `PeriodoAcademico`, `Rol`, `TipoDocumento`, `TipoEvento`,
    `TipoPregunta`, `TipoVinculacion`);
  - las 5 de la v2: `Facultad`, `Dependencia`, `ProgramaAcademico`,
    `AliasPrograma` y `Catedra`;
  - las 7 de la v3: `Asistente`, `DocumentoAsistente`,
    `VinculacionAsistente`, `ProgramaAsistente`, `ConsentimientoDatos`,
    `LoteCargaAsistente` y `NovedadCarga`.
- Lo nuevo que enseña la v3:
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
(Artículo 2 de la constitución). La v3 no agrega paquetes a la API; el
Excel se lee en el front (`openpyxl`, en `front_catedras/requirements.txt`).

## Cómo levantarlo

```powershell
docker compose up -d --build
```

| Recurso | URL |
|---|---|
| Diagnóstico | http://localhost:8142/ |
| Swagger UI | http://localhost:8142/swagger |
| API (ejemplo) | http://localhost:8142/api/Asistente?q=1000445 |
| Front · administración | http://localhost:5000/admin/ |
| Front · asistentes | http://localhost:5000/admin/asistentes |
| Front · carga de datos | http://localhost:5000/carga_datos |
| PostgreSQL (herramienta externa) | localhost,15542 · base `bdcatedras_postgres_local` · `sa` / `Construccion123!` |

> **¿Tu volumen de PostgreSQL ya existía antes de la v3?** Los SP nuevos
> solo se crean solos con una BD vacía. Aplícalos a mano (o haz
> `docker compose down -v`); ver `docs/spec_kit/Versiones/version3/7_quickstart.md` §1.

Prueba de capas (sin PostgreSQL, 22 entidades, 220 verificaciones). Se
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
```

## Documentación de la v3

En `docs/spec_kit/Versiones/version3/`: `2_spec.md` (qué y criterios de
aceptación), `3_plan.md` (cómo), `4_research.md` (decisiones D15-D19),
`5_data_model.md` (las 7 tablas y sus FK reales), `6_contracts.md`,
`7_quickstart.md` (smoke test) y `8_tasks.md` (fases 22-31).
