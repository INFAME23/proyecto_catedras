# Proyecto Cátedras — Versión 2

API REST del sistema de **asistencia a cátedras universitarias**, construida
por **versiones incrementales**. Esta es la **v2**: la estructura académica
y el **catálogo de cátedras**, sobre la arquitectura en capas que validó la
v1.

## Qué incluye la v2

- Las **15 tablas** que hoy expone la API:
  - las 10 sin foreign key de la v1 (`Sede`, `Encuesta`, `EstadoProceso`,
    `Modalidad`, `PeriodoAcademico`, `Rol`, `TipoDocumento`, `TipoEvento`,
    `TipoPregunta`, `TipoVinculacion`);
  - las 5 de la v2: `Facultad`, `Dependencia`, `ProgramaAcademico`,
    `AliasPrograma` y `Catedra`.
- Lo nuevo que enseña la v2:
  - **Autorreferencia** (`Facultad`, `Dependencia`): el servicio rechaza
    con 400 una fila que se nombra a sí misma como padre.
  - **Foreign keys reales**: un padre inexistente lo rechaza el motor
    (500); los `DELETE` disparan `SET NULL`, `CASCADE` o `RESTRICT`
    según el esquema.
  - **Columna generada** (`Catedra.nombre_asis`): la calcula la BD y se
    devuelve con un solo `INSERT ... RETURNING`.
  - **Tabla de solo-anexo** (`AliasPrograma`): sin `PUT` ni `PATCH`.
- Front (Flask): catálogo público de cátedras (`/catedras`, con buscador,
  filtro por tipo y paginación), detalle (`/catedras/<id>`) y panel de
  administración con el CRUD de las 15 tablas.

## Stack

| Pieza | Elección |
|---|---|
| Lenguaje / framework | C# · ASP.NET Core (.NET 10) |
| Acceso a datos | Dapper sobre Npgsql (SQL a mano, siempre `@parametro`) |
| Documentación | Swashbuckle.AspNetCore (Swagger) |
| Motor | PostgreSQL 16 (contenedor oficial) |
| Front | Flask + Bootstrap 5 (identidad USB) |
| Orquestación | Docker Compose |

Paquetes NuGet: `Npgsql`, `Dapper`, `Swashbuckle.AspNetCore` — y ninguno más
(Artículo 2 de la constitución). La v2 no agrega paquetes.

## Cómo levantarlo

```powershell
docker compose up -d --build
```

| Recurso | URL |
|---|---|
| Diagnóstico | http://localhost:8142/ |
| Swagger UI | http://localhost:8142/swagger |
| API (ejemplo) | http://localhost:8142/api/Catedra?limite=10 |
| Front · catálogo | http://localhost:5000/catedras |
| Front · administración | http://localhost:5000/admin/ |
| PostgreSQL (herramienta externa) | localhost,15542 · base `bdcatedras_postgres_local` · `sa` / `Construccion123!` |

Prueba de capas (sin PostgreSQL, 15 entidades):

```powershell
cd api_catedras
dotnet run --project pruebas
```

> Las pruebas de `DELETE` del quickstart modifican datos de ejemplo. Para
> volver al estado original: `docker compose down -v && docker compose up -d --build`.

## Estructura

```
docker-compose.yml              # un comando: postgres + api + front
db/postgres/
  script_bdcatedras.sql         # la BD completa, PROVISTA (se monta, no se genera)
  migraciones/                  # objetos PROPIOS del proyecto (p. ej. SP maestro-detalle,
                                #  docs/spec_kit/guia_ia.md); se montan después del script
api_catedras/                   # la API .NET (Controllers, Modelos, Peticiones,
                                #  Servicios, Repositorios, Excepciones, pruebas)
front_catedras/                 # el front Flask
docs/
  GUIA_ESTUDIANTE.md            # guía de arranque (v1)
  spec_kit/1_constitution.md    # reglas permanentes de todas las versiones
  spec_kit/guia_ia.md           # reglas técnicas transversales (SP, <select> de FK, BCrypt)
  spec_kit/Versiones/
    0_mapa_versiones.md         # qué tablas entran en cada versión
    version1/  2_spec .. 8_tasks
    version2/  2_spec .. 8_tasks
```

## Documentación de la v2

En `docs/spec_kit/Versiones/version2/`: `2_spec.md` (qué y criterios de
aceptación), `3_plan.md` (cómo), `4_research.md` (decisiones D9-D13),
`5_data_model.md` (las 5 tablas y sus FK reales), `6_contracts.md`,
`7_quickstart.md` (smoke test) y `8_tasks.md` (fases 14-21, todas cerradas).
