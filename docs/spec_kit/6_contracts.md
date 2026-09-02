# Contratos de integración — Proyecto Construcción 1

> **Documento 6 de 8** del spec kit raíz: cómo se hablan las piezas entre sí
> (puertos, hosts, variables, healthchecks). Este kit cubre los contratos de
> **integración**; los contratos HTTP internos de la API pertenecen a la
> aplicación (caja negra para la infraestructura).
>
> **Nota de alcance (v1):** no hay `front`, ni `api-generica`/`api-facturas`
> separadas, ni MariaDB/SQL Server/phpMyAdmin — eso es plantilla de otro
> proyecto de referencia. La v1 real de Construcción 1 es **un solo
> servicio API** hablando con **una sola base de datos, PostgreSQL**
> (`bdcatedras`), y el único recurso que la API puede exponer es `sede`
> (ver Documento 5 — Modelo de datos).

---

## 1. Mapa de puertos

| Servicio | Host interno (red compose) | Puerto publicado al PC |
|---|---|---|
| api | `api:8000` | **8000** (`/docs`) |
| postgres | `postgres:5432` | **15432** |

Regla: **entre contenedores** siempre el host interno con puerto estándar;
**desde el PC** siempre localhost con el puerto publicado.

## 2. Variables de entorno (el contrato de configuración)

### api
| Variable | Valor en compose |
|---|---|
| `DB_POSTGRES` | `postgresql+asyncpg://sa:Construccion123!@postgres:5432/bdcatedras_postgres_local` |

No hay `DB_PROVIDER` ni motores alternativos en v1: PostgreSQL es la
única base de datos y no se conmuta en tiempo de ejecución.

### postgres
| Variable | Valor |
|---|---|
| `POSTGRES_DB` | `bdcatedras_postgres_local` |
| `POSTGRES_USER` | `sa` |
| `POSTGRES_PASSWORD` | `Construccion123!` |

## 3. Contrato de la API (resumen)

- Healthcheck de "en línea": `GET /` responde JSON (5 s de timeout).
- Documentación interactiva (contrato vivo): `GET /docs` (Swagger UI de
  FastAPI).
- CRUD sobre `sede`: verbos estándar (`GET/POST/PUT/DELETE /api/sede`,
  `GET/PUT/DELETE /api/sede/{id_sede}`), cuerpo y respuesta en JSON con
  la forma del modelo `Sede` (Documento 5).
- Errores de validación (API, antes de tocar la BD): `422` con lista de
  errores por campo.
- Errores de la BD (PK/UNIQUE/FK violados): `500` con el mensaje del
  motor, envuelto como `{"detail": {estado, mensaje, detalle}}`
  (convención de error de FastAPI).

## 4. Contrato API ↔ BD

- Un único engine, creado de forma **perezosa** (primera petición) → la
  API arranca aunque Postgres aún esté inicializando; no hace falta
  `depends_on: postgres: condition: service_healthy` para que el
  contenedor de la API viva, aunque las peticiones fallarán hasta que
  Postgres esté listo.
- No hay lógica de negocio en triggers/SP para `sede` en v1 (eso es de
  otras tablas/versiones): la API valida y hace el `INSERT`/`UPDATE`
  directo; la BD solo aplica sus restricciones (PK, `UNIQUE(nombre)`).

## 5. Healthchecks y arranque

| Servicio | Check | Parámetros |
|---|---|---|
| postgres | `pg_isready -U sa -d bdcatedras_postgres_local` | cada 5 s, 10 reintentos |

## 6. Contratos de volúmenes y montajes

| Montaje | Para qué |
|---|---|
| `pgdata` (nombrado) | Persistencia de datos; `down -v` = reset |
| `./db/postgres/script_bdcatedras.sql → /docker-entrypoint-initdb.d/` (ro) | Init de PostgreSQL (solo corre con volumen vacío) |
| `./api → /app` | Recarga en caliente |

## 7. Devcontainer (contrato con VS Code)

`.devcontainer/devcontainer.json`: `service: api`, `workspaceFolder: /workspace`,
`forwardPorts: [8000]`, extensiones Python + Pylance + SQLTools (driver
pg) con una conexión SQLTools al **host interno** (`postgres:5432`).

## 8. Credenciales (fijas en todo el proyecto)

| Motor | BD | Usuario | Clave |
|---|---|---|---|
| PostgreSQL | `bdcatedras_postgres_local` | `sa` | `Construccion123!` |

## 9. Reset completo

`docker compose down -v && docker compose up -d` — borra el volumen
`pgdata` y el inicializador (`script_bdcatedras.sql`) vuelve a crear el
esquema `catedras` completo con sus datos de ejemplo.