# Contratos de integración — Versión 1: ApiCatedras + PostgreSQL

> **Documento 6 de 8** del spec kit: cómo se hablan las piezas entre sí
> (puertos, hosts, variables, healthchecks). Este documento cubre los
> contratos de **integración**; los endpoints HTTP de la API (verbos,
> formatos exactos de cada uno) están detallados en [2_spec.md](2_spec.md)
> (RF1-RF61) y se navegan en vivo desde `/swagger`.
>
> **Nota de alcance (v1):** un solo servicio `api` (C#/ASP.NET Core) hablando
> con una sola base de datos, PostgreSQL (`bdcatedras_postgres_local`).
> Los recursos que la API expone HOY son `Sede`, `Encuesta`,
> `EstadoProceso` y `Modalidad` (ver [5_data_model.md](5_data_model.md));
> 6 más están especificadas para el próximo commit. Sin segundo motor, sin
> devcontainer. Sí existe un frontend (`front_catedras/`, Flask) pero es
> un proyecto aparte, todavía no integrado a este `docker-compose.yml`.

---

## 1. Mapa de puertos

| Servicio | Host interno (red compose) | Puerto publicado al PC |
|---|---|---|
| api | `api:8042` | **8142** (`/`, `/swagger`) |
| postgres | `postgres:5432` | **15542** |

Regla: **entre contenedores** siempre el host interno con puerto estándar
(`postgres:5432`); **desde el PC** siempre `localhost` con el puerto
publicado (`15542`).

## 2. Variables de entorno (el contrato de configuración)

### api
| Variable | Valor por defecto (`appsettings.json`, sin Docker) | Valor en compose |
|---|---|---|
| `ConnectionStrings__Postgres` | `Host=localhost;Port=15542;Database=bdcatedras_postgres_local;Username=sa;Password=Construccion123!;Include Error Detail=true` | `Host=postgres;Port=5432;Database=bdcatedras_postgres_local;Username=sa;Password=Construccion123!;Include Error Detail=true` |
| `ASPNETCORE_URLS` | (no aplica fuera de Docker) | `http://+:8042` |
| `ASPNETCORE_ENVIRONMENT` | (no aplica fuera de Docker) | `Development` |

El separador `__` (doble guion bajo) es la convención de ASP.NET Core para
sobrescribir una clave anidada de `appsettings.json`
(`ConnectionStrings:Postgres`) con una variable de entorno. No hay
`DB_PROVIDER` ni motores alternativos en v1: PostgreSQL es el único motor
y no se conmuta en tiempo de ejecución.

### postgres
| Variable | Valor |
|---|---|
| `POSTGRES_DB` | `bdcatedras_postgres_local` |
| `POSTGRES_USER` | `sa` |
| `POSTGRES_PASSWORD` | `Construccion123!` |

## 3. Contrato de la API (resumen)

- Healthcheck de "en línea": `GET /` responde JSON de diagnóstico
  (`{mensaje, version, contratos}`).
- Documentación interactiva (contrato vivo): `GET /swagger` (Swagger UI de
  Swashbuckle).
- CRUD sobre `Sede`, `Encuesta`, `EstadoProceso` y `Modalidad`:
  `GET/POST /api/{Recurso}`, `GET/PUT/PATCH/DELETE /api/{Recurso}/{id}`,
  cuerpo y respuesta en JSON con la forma de cada modelo
  ([5_data_model.md](5_data_model.md)). `{id}` es texto para las tres
  primeras y numérico para `Encuesta` (su PK la genera la BD).
- Errores de validación de la PETICIÓN (antes de tocar la BD): `422` con
  `{estado, mensaje, errores:[{campo, mensaje}]}`.
- Errores de negocio: `400` (`ArgumentException`) y `404`
  (`NoEncontradoExcepcion`), ambos como `{estado, mensaje, detalle}`.
- Errores del motor (PK/UNIQUE/FK violados): `500` con el mensaje de
  `NpgsqlException` en `detalle`, mismo sobre `{estado, mensaje, detalle}`.

## 4. Contrato API ↔ BD

- Una conexión (`NpgsqlConnection`) por operación del repositorio, abierta
  con `await using` — no hay pool de conexiones propio ni engine
  compartido: lo maneja `Npgsql` internamente.
- La API **depende** de que Postgres esté sano para arrancar en compose
  (`depends_on: postgres: condition: service_healthy`): no hace falta
  reintentos de conexión en el arranque porque el propio compose ya
  garantiza el orden.
- No hay lógica de negocio en triggers/SP para `sede` en v1 (eso es de
  otras tablas/versiones): la API valida y hace el `INSERT`/`UPDATE`
  directo; la BD solo aplica sus restricciones (`pk_sede`,
  `uq_sede_nombre`, `NOT NULL`).

## 5. Healthchecks y arranque

| Servicio | Check | Parámetros |
|---|---|---|
| postgres | `pg_isready -U sa -d bdcatedras_postgres_local` | cada 5 s, timeout 5 s, 10 reintentos |
| api | (sin healthcheck propio) | arranca solo cuando `postgres` está `healthy` |

## 6. Contratos de volúmenes y montajes

| Montaje | Para qué |
|---|---|
| `pgdata` (nombrado) | Persistencia de datos de PostgreSQL; `down -v` = reset |
| `./db/postgres/script_bdcatedras.sql → /docker-entrypoint-initdb.d/script_bdcatedras.sql` (ro) | Init de PostgreSQL (solo corre con volumen vacío) |
| `./api_catedras → /app` | Código de la API montado en caliente (`dotnet watch`) |
| `/app/bin`, `/app/obj`, `/app/pruebas/bin`, `/app/pruebas/obj` (anónimos) | Compilados de Linux (contenedor) separados de los de Windows (IDE) |

## 7. Devcontainer

**No existe en v1.** El desarrollo se hace directamente sobre el código
montado (`dotnet watch` dentro del contenedor `api`); no hay
`.devcontainer/devcontainer.json` ni extensión de IDE específica requerida.

## 8. Credenciales (fijas en todo el proyecto)

| Motor | BD | Usuario | Clave |
|---|---|---|---|
| PostgreSQL | `bdcatedras_postgres_local` | `sa` | `Construccion123!` |

## 9. Reset completo

`docker compose down -v && docker compose up -d --build` — borra el
volumen `pgdata` y el inicializador (`script_bdcatedras.sql`) vuelve a
crear el esquema `catedras` completo (37 tablas) con sus datos de
ejemplo.
