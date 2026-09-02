# Proyecto Cátedras — Versión 1

API REST del sistema de **asistencia a cátedras universitarias**, construida
por **versiones incrementales**. Esta es la **v1**: la primera rebanada
vertical — el CRUD completo de **una sola entidad (`Sede`)** contra **un
solo motor (PostgreSQL)**, con la arquitectura en capas completa desde el
primer día.

## Qué incluye la v1

- CRUD de `Sede` (`/api/Sede`): listar, obtener por código, crear (POST),
  reemplazar (PUT), actualizar parcial (PATCH), eliminar (DELETE).
- Capas con interfaces de C#: `SedeController` → `IServicioSede` →
  `IRepositorioSede` → `RepositorioSedePostgres` (Dapper, SQL a mano
  parametrizado; sin Entity Framework).
- Validación en las **peticiones** (una por verbo) → `422` con lista de
  errores antes de tocar la base.
- Documentación interactiva en `/swagger` y diagnóstico en `/`.
- Un solo comando: `docker compose up -d --build`.

## Stack

| Pieza | Elección |
|---|---|
| Lenguaje / framework | C# · ASP.NET Core (.NET 10) |
| Acceso a datos | Dapper sobre Npgsql (SQL a mano, siempre `@parametro`) |
| Documentación | Swashbuckle.AspNetCore (Swagger) |
| Motor | PostgreSQL 16 (contenedor oficial) |
| Orquestación | Docker Compose |

Paquetes NuGet permitidos en v1: `Npgsql`, `Dapper`, `Swashbuckle.AspNetCore`
— y ninguno más (Artículo 2 de la constitución).

## Cómo levantarlo

```powershell
docker compose up -d --build
```

| Recurso | URL |
|---|---|
| Diagnóstico | http://localhost:8142/ |
| Swagger UI | http://localhost:8142/swagger |
| API `Sede` | http://localhost:8142/api/Sede |
| PostgreSQL (herramienta externa) | localhost,15542 · base `bdcatedras_postgres_local` · `sa` / `Construccion123!` |

> Los puertos se publican con **+100** respecto a los documentos para
> convivir con el proyecto del curso en la misma máquina; dentro de los
> contenedores todo queda en 8042 y 5432. El proyecto Compose se llama
> `mi_v1_catedras`.

## Estructura

```
docker-compose.yml              # un comando: postgres + api
db/postgres/
  script_bdcatedras.sql         # la BD completa, PROVISTA (se monta, no se genera)
api_catedras/                   # la API .NET (ver docs/spec_kit/3_plan.md)
docs/
  GUIA_ESTUDIANTE.md            # guía de arranque y primer recorrido
  spec_kit/1_constitution.md .. 8_tasks.md   # constitución + spec kit de la v1
```

## Documentación

En `docs/spec_kit/`:

- `1_constitution.md` — reglas permanentes de todas las versiones.
- `2_spec.md` — qué construir en v1 y sus criterios de aceptación.
- `3_plan.md` — cómo: stack, estructura y diseño de capas.
- `5_data_model.md` — la BD dada y la tabla `sede`.
- `6_contracts.md` / `7_quickstart.md` / `8_tasks.md` — contratos, arranque y orden de trabajo.

Y `docs/GUIA_ESTUDIANTE.md` — guía práctica paso a paso.
