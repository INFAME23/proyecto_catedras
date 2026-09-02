# Tareas — Infraestructura e integración

> **Documento 8 de 8** del spec kit raíz: orden de construcción del
> proyecto v1. Cada fase termina en algo **verificable**.
> Requisitos: `2_spec.md` · decisiones: `3_plan.md` · principios:
> `1_constitution.md` · BD: [5_data_model.md](5_data_model.md) ·
> integración: [6_integration_contracts.md](6_integration_contracts.md) ·
> verificación: [7_quickstart.md](7_quickstart.md).
>
> **Nota de alcance (v1):** sin front, sin API doble, sin MariaDB/SQL
> Server/phpMyAdmin, sin cambio de motor — un solo servicio `api` +
> PostgreSQL, y el único recurso que la API puede tocar es `sede`. Las
> fases del proyecto de referencia que no aplican quedan marcadas
> **N/A v1** en vez de borradas, para que quede claro qué se dejó fuera
> a propósito y no por omisión.

---

## Fase 0 — Repositorio
- [ ] `git init`, `.gitignore` (venv, `__pycache__`, `.env*`), `README.md` inicial.
- [ ] Crear carpetas `db/postgres`, `docs/`.

## Fase 1 — La base de datos (dada, no se construye)
- [ ] Copiar el script **provisto** `script_bdcatedras.sql` a
      `db/postgres/init.sql` **sin modificarlo** (§4 de
      [5_data_model.md](5_data_model.md): la BD no se toca — ni columnas,
      ni índices, ni datos semilla distintos).
- [ ] Servicio `postgres` en un `docker-compose.yml` mínimo (imagen
      `postgres:16-alpine`, volumen `pgdata`, `init.sql` montado en
      `/docker-entrypoint-initdb.d/`, puerto **15432**, healthcheck
      `pg_isready -U sa -d bdcatedras_postgres_local`).

**Verificar:** `docker compose up -d postgres` → conectar con
pgAdmin/psql a `localhost:15432` (usuario `sa` / clave
`Construccion123!`) y comprobar: esquema `catedras` con sus 37 tablas,
tabla `sede` con 3 filas (`SAN_BENITO`, `BELLO`, `VIRTUAL`).

## Fase 2 — Los otros dos motores
**N/A v1.** Un solo motor (PostgreSQL); no hay `DB_PROVIDER` ni
traducción a MariaDB/SQL Server. Queda para versiones posteriores si la
especificación lo pide.

## Fase 3 — La aplicación
- [ ] Colocar en `api/` una API CRUD para `sede` (Documento 5) que
      escuche en el puerto 8000 y lea `DB_POSTGRES` del entorno.
- [ ] Implementar la primera muralla de validación en la API: forma,
      tipos y rangos de `Sede` → `422` con lista de errores por campo,
      **antes** de tocar la BD (§3 de
      [5_data_model.md](5_data_model.md)).
- [ ] Dejar que la BD sea la segunda muralla: no duplicar en la API
      reglas que ya cubren `pk_sede` y `uq_sede_nombre`; un choque ahí
      debe propagarse como `500` con el mensaje del motor.

> La aplicación es un proyecto independiente con su propia
> especificación; para esta infraestructura es una caja negra que
> cumple los contratos de
> [6_integration_contracts.md](6_integration_contracts.md).

## Fase 4 — Integración en compose
- [ ] Agregar el servicio `api` (8000) con: build propio, volumen de
      código + comando con reload, `restart: unless-stopped`, y la
      variable `DB_POSTGRES` (§2 de
      [6_integration_contracts.md](6_integration_contracts.md)).

**Verificar:** `docker compose up -d --build` → `http://localhost:8000/docs`
muestra el recurso `sede`; crear/editar/eliminar una sede desde `/docs`
se refleja consultando la BD por psql/pgAdmin; editar un archivo Python
recarga la app sin reconstruir.

## Fase 5 — Cambio de motor
**N/A v1.** Sin motores alternativos, no hay nada que conmutar.

## Fase 6 — Devcontainer (opcional)
- [ ] `.devcontainer/devcontainer.json`: `service: api`,
      `workspaceFolder: /workspace`, `forwardPorts: [8000]`,
      extensiones Python + Pylance + SQLTools, una conexión interna a
      `postgres:5432`.

**Verificar:** "Dev Containers: Reopen in Container" abre el repo;
SQLTools conecta a `postgres`; `Ctrl+E Ctrl+E` ejecuta una consulta
sobre `catedras.sede`.

## Fase 7 — Persistencia y reset
- [ ] Validar el ciclo completo de datos.

**Verificar:** crear una sede → `docker compose down` → `up -d` → sigue
ahí; `down -v` → `up -d` → volvieron solo las 3 sedes originales del
script.

## Fase 8 — Documentación
- [ ] `docs/GUIA_ESTUDIANTE.md` (instalación → primer recorrido con
      `/docs` → comandos del día a día).
- [ ] Comentarios didácticos en `docker-compose.yml` explicando por qué
      v1 solo tiene `api` + `postgres` (referencia a
      [6_integration_contracts.md](6_integration_contracts.md) §1 nota
      de alcance).