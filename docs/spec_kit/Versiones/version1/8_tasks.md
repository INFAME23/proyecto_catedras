# Tareas — Versión 1: orden de construcción

> **Documento 8 de 8** del spec kit: orden de construcción del proyecto v1.
> Cada fase termina en algo **verificable**.
> Requisitos: [2_spec.md](2_spec.md) · decisiones: [3_plan.md](3_plan.md) ·
> principios: [1_constitution.md](1_constitution.md) · BD:
> [5_data_model.md](5_data_model.md) · integración:
> [6_contracts.md](6_contracts.md) · verificación:
> [7_quickstart.md](7_quickstart.md).
>
> **Nota de alcance (v1):** sin segundo motor, sin cambio de proveedor —
> un solo servicio `api` (C#/ASP.NET Core) + PostgreSQL. El código hoy
> cubre las **10 tablas sin FK** (Fases 9-12, cerradas al iniciar la v2). Ya existe un frontend
> (`front_catedras/`, Flask) — Fase 13.

---

## Fase 0 — Repositorio
- [x] `git init`, `.gitignore` (`bin/`, `obj/`, `.vs/`), `README.md` inicial.
- [x] Crear carpetas `db/postgres`, `docs/spec_kit`.

## Fase 1 — La base de datos (dada, no se construye)
- [x] Copiar el script **provisto** `script_bdcatedras.sql` a
      `db/postgres/script_bdcatedras.sql` **sin modificarlo** (§4 de
      [5_data_model.md](5_data_model.md): la BD no se toca — ni columnas,
      ni índices, ni datos semilla distintos).
- [x] Servicio `postgres` en `docker-compose.yml` (imagen
      `postgres:16-alpine`, volumen `pgdata`, script montado en
      `/docker-entrypoint-initdb.d/`, puerto **15542→5432**, healthcheck
      `pg_isready -U sa -d bdcatedras_postgres_local`).

**Verificar:** `docker compose up -d postgres` → conectar con
pgAdmin/psql a `localhost:15542` (usuario `sa` / clave
`Construccion123!`) y comprobar: esquema `catedras` con sus 37 tablas,
tabla `sede` con 3 filas (`SAN_BENITO`, `BELLO`, `VIRTUAL`).

## Fase 2 — Otros motores
**N/A v1.** Un solo motor (PostgreSQL); no hay selección de motor en
tiempo de ejecución. Queda para versiones posteriores si la
especificación lo pide.

## Fase 3 — La aplicación (capas de `Sede`)
- [x] `ApiCatedras.csproj` (SDK Web, .NET 10; paquetes `Npgsql`, `Dapper`,
      `Swashbuckle.AspNetCore`).
- [x] `Modelos/Sede.cs` — la entidad (5 propiedades).
- [x] `Peticiones/SedeCrear.cs`, `SedeReemplazo.cs`, `SedeActualizar.cs` —
      una petición por verbo, con anotaciones de validación (§3 de
      [5_data_model.md](5_data_model.md)): forma, tipos y rangos →
      `422` con lista de errores, **antes** de tocar la BD.
- [x] `Repositorios/IRepositorioSede.cs` + `RepositorioSedePostgres.cs` —
      Dapper, SQL a mano parametrizado.
- [x] `Servicios/IServicioSede.cs` + `ServicioSede.cs` — reglas de negocio
      (límite > 0, PATCH sin campos → 400, no existe → 404). No duplica en
      la API reglas que ya cubren `pk_sede` y `uq_sede_nombre`: un choque
      ahí se propaga como `500` con el mensaje del motor.
- [x] `Controllers/SedeController.cs` — los 6 endpoints, try/catch →
      código HTTP.
- [x] `Excepciones/NoEncontradoExcepcion.cs`.
- [x] `Program.cs` — ensamblador (DI), 422 personalizado, ruta `/`, Swagger.

## Fase 4 — Integración en compose
- [x] Agregar el servicio `api` (`8142→8042`) con: `Dockerfile` propio
      (SDK + `dotnet watch`), volumen de código (`./api_catedras:/app`) +
      volúmenes anónimos para `bin/`/`obj/`, `restart: unless-stopped`,
      `depends_on: postgres: condition: service_healthy`, y la variable
      `ConnectionStrings__Postgres` (§2 de [6_contracts.md](6_contracts.md)).

**Verificar:** `docker compose up -d --build` → `http://localhost:8142/swagger`
muestra el recurso `Sede`; crear/editar/eliminar una sede desde `/swagger`
se refleja consultando la BD por psql/pgAdmin; editar un `.cs` recarga la
app sin reconstruir la imagen.

## Fase 5 — Cambio de motor
**N/A v1.** Sin motores alternativos, no hay nada que conmutar.

## Fase 6 — Devcontainer
**N/A v1.** No se usa devcontainer; el desarrollo es directo sobre el
código montado en el contenedor `api` (`dotnet watch`).

## Fase 7 — Persistencia y reset
- [x] Validar el ciclo completo de datos.

**Verificar:** crear una sede → `docker compose down` → `up -d` → sigue
ahí; `down -v` → `up -d --build` → volvieron solo las 3 sedes originales
del script.

## Fase 8 — Documentación
- [x] `docs/GUIA_ESTUDIANTE.md` (instalación → primer recorrido con
      `/swagger` → comandos del día a día).
- [x] Comentarios didácticos en `docker-compose.yml` explicando por qué
      v1 solo tiene `api` + `postgres`.
- [x] `docs/RESUMEN_SOLID_ACID.md` — dónde vive cada principio SOLID y
      cada letra de ACID en el código de v1.

## Fase 9 — Prueba de capas (criterio de aceptación 6)
- [x] `pruebas/PruebaCapas.csproj` + `pruebas/Programa.cs`: `ServicioSede`
      construido con un repositorio FALSO en memoria (`RepositorioSedeFalso`),
      sin PostgreSQL. `dotnet run --project pruebas` corre 10 verificaciones
      y sale con código 0 si todas pasan.

## Fase 10 — La aplicación (capas de `Encuesta`, `EstadoProceso`, `Modalidad`)
- [x] `Modelos/Encuesta.cs`, `EstadoProceso.cs`, `Modalidad.cs` (§2.2-2.4
      de [5_data_model.md](5_data_model.md)).
- [x] `Peticiones/Encuesta{Crear,Reemplazo,Actualizar}.cs`,
      `EstadoProceso{Crear,Reemplazo,Actualizar}.cs`,
      `Modalidad{Crear,Reemplazo,Actualizar}.cs` — anotaciones de forma;
      `ModalidadCrear`/`Actualizar` validan `canal_difusion` con
      `[RegularExpression]` (§4.9 de [3_plan.md](3_plan.md)).
- [x] `Repositorios/IRepositorioEncuesta.cs` + `RepositorioEncuestaPostgres.cs`
      — `CrearAsync` devuelve `Task<long>` (`INSERT ... RETURNING
      id_encuesta`, §4.7 de [3_plan.md](3_plan.md)).
- [x] `Repositorios/IRepositorioEstadoProceso.cs` +
      `RepositorioEstadoProcesoPostgres.cs`.
- [x] `Repositorios/IRepositorioModalidad.cs` + `RepositorioModalidadPostgres.cs`.
- [x] `Repositorios/ManejadorTipoDateOnly.cs` — hallazgo no anticipado:
      Dapper no mapea `DateOnly` (columnas `DATE` de `Encuesta`) sin un
      `SqlMapper.TypeHandler` propio; registrado en `Program.cs` (§4.7 de
      [3_plan.md](3_plan.md)).
- [x] `Servicios/IServicioEncuesta.cs` + `ServicioEncuesta.cs` — valida
      `vigente_hasta >= vigente_desde` cuando tiene las dos fechas
      (§4.8 de [3_plan.md](3_plan.md)).
- [x] `Servicios/IServicioEstadoProceso.cs` + `ServicioEstadoProceso.cs` —
      valida `es_exitoso → es_final` cuando tiene ambos booleanos.
- [x] `Servicios/IServicioModalidad.cs` + `ServicioModalidad.cs`.
- [x] `Controllers/EncuestaController.cs`,
      `EstadoProcesoController.cs`, `ModalidadController.cs` — 6
      endpoints cada uno, mismo try/catch → código HTTP que `SedeController`.
- [x] `Program.cs` — registrar las 3 parejas interfaz/implementación
      nuevas en el ensamblador (`AddScoped`).

**Verificado** (contra Docker + PostgreSQL reales, no solo compilación):
`GET/POST/PUT/PATCH/DELETE` de las 3 entidades responden y persisten
correctamente; `POST /api/Encuesta` sin body extra crea con los defaults
(`version=1`, `vigente_desde`=hoy, `activa=true`) y devuelve `id_encuesta`
generado por la BD; fechas invertidas → 400; `POST /api/EstadoProceso`
con `es_exitoso:true, es_final:false` → 400; `POST /api/Modalidad` con
`canal_difusion:"FAX"` → 422; duplicados → 500; las 3 tablas quedaron con
solo sus filas de siembra tras las pruebas.

## Fase 11 — Prueba de capas extendida
- [x] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para `Encuesta`, `EstadoProceso` y `Modalidad`
      (mismo patrón que `RepositorioSedeFalso`): CRUD completo, cada regla
      de negocio (fechas, coherencia, PATCH vacío) y el caso 404.
      `RepositorioEncuestaFalso` simula además la IDENTITY con un contador
      propio (`_siguienteId`).

**Verificado:** `dotnet run --project pruebas` → **45 pasadas, 0
fallidas**, código de salida 0.

## Fase 12 — La aplicación (capas de las 6 tablas sin FK restantes) ✅
> Especificadas en RF26-RF61 de [2_spec.md](2_spec.md) y §2.5-2.10 de
> [5_data_model.md](5_data_model.md). **Cerrada al iniciar la v2:** el
> código de las 6 entidades ya existía, pero `Program.cs` no registraba
> `TipoPregunta` ni `TipoVinculacion` (sus endpoints respondían 500) y
> `pruebas/` no las cubría. Ambas cosas se corrigieron en el primer
> commit de la v2.

- [x] `Modelos/PeriodoAcademico.cs`, `Rol.cs`, `TipoDocumento.cs`,
      `TipoEvento.cs`, `TipoPregunta.cs`, `TipoVinculacion.cs`.
- [x] Las 3 peticiones por entidad (`...Crear`, `...Reemplazo`,
      `...Actualizar`) — `PeriodoAcademicoCrear` valida el formato de
      `id_periodo` con `[RegularExpression("^[0-9]{4}-[1-3]$")]`;
      `TipoEventoCrear` usa `[StringLength(4, MinimumLength = 4)]`
      (longitud fija, no "hasta 4").
- [x] Repositorios (`IRepositorioXxx` + `RepositorioXxxPostgres`) para
      las 6. `RepositorioPeriodoAcademicoPostgres` reutiliza
      `ManejadorTipoDateOnly.cs` (ya existe) para `fecha_ini`/`fecha_fin`.
- [x] Servicios: `ServicioPeriodoAcademico` valida `fecha_fin > fecha_ini`
      (mismo criterio que `Encuesta`, §4.8 de [3_plan.md](3_plan.md));
      `ServicioTipoPregunta` valida que exactamente una de
      `guarda_numero`/`guarda_texto`/`guarda_opcion` sea `true`
      (extensión a TRES campos de la misma regla).
- [x] Controllers (6 endpoints cada uno, mismo patrón que
      `SedeController`).
- [x] `Program.cs` — registrar las 6 parejas interfaz/implementación
      nuevas en el ensamblador.
- [x] Extender `pruebas/Programa.cs` con un repositorio FALSO y sus
      verificaciones por cada una de las 6 (mismo patrón que
      `RepositorioModalidadFalso`).

**Verificado** (contra PostgreSQL real): los 6 recursos aparecen en
`/swagger`; `POST /api/PeriodoAcademico` con `fecha_fin <= fecha_ini` →
400; `POST /api/TipoPregunta` con las tres columnas en `false` (o dos en
`true`) → 400; `POST /api/TipoVinculacion` sin `es_interno` → 422 (es
obligatorio, no tiene `DEFAULT`); `dotnet run --project pruebas` sigue en
0 fallidas con las 10 entidades.

## Fase 13 — Frontend (`front_catedras/`)
- [x] Proyecto Flask aparte (`front_catedras/`, junto a `api_catedras/`
      en la raíz del repo): `app.py`, `config.py`, `run.py`,
      `requirements.txt`, `.env` (`API_BASE_URL` → `http://localhost:8142/api`).
- [x] `servicios/api_catedras.py` — cliente HTTP hacia `ApiCatedras`
      (`listar/obtener/crear/reemplazar/eliminar`), traduce el sobre de
      error de la API (`{estado, mensaje, detalle, errores}`) a mensajes
      de plantilla.
- [x] Blueprint `admin` — CRUD completo (listar/crear/editar/eliminar)
      de las 4 entidades construidas (`Sede`, `Encuesta`, `EstadoProceso`,
      `Modalidad`), con formularios HTML que traducen a
      PUT/DELETE (los navegadores solo mandan GET/POST).
- [x] Blueprints `catedras` (inicio), `auth`, `ponente`, `estudiante`,
      `registro` como placeholders — dependen de tablas que la API aún
      no expone.
- [x] Identidad visual de la Universidad San Buenaventura Medellín:
      Bootstrap 5 + Font Awesome 6 + Google Font Inter + CSS
      institucional (naranja `#EF7D00`/negro `#1D1D1B`) + escudo
      (`static/img/escudo-usb.jpeg`), tomados de un proyecto de
      referencia del estudiante (`Desktop/Front`).
- [x] Integración a `docker-compose.yml`: el servicio `front` ya está en
      el compose (puerto 5000, `API_BASE_URL=http://api:8042/api`).
- [x] Pantallas para las 6 tablas de la Fase 12 (las 2 últimas,
      `TipoPregunta` y `TipoVinculacion`, se cerraron en la v2).

**Verificado:** CRUD de las 4 entidades probado de punta a punta contra
Docker/PostgreSQL reales desde el front (`http://localhost:5000/admin/`);
errores de negocio de la API (fechas, coherencia, canal, duplicados) se
ven como mensajes claros en pantalla.
