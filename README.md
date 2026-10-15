# Proyecto Cátedras — Versión 6 (en desarrollo)

API REST del sistema de **asistencia a cátedras universitarias**, construida
por **versiones incrementales**. Este repositorio avanza a la **v6: encuestas
de calidad**, sobre el login y registro de asistencia de la v5, las sesiones
de la v4, las personas de la v3, la estructura académica de la v2 y la
arquitectura en capas de la v1.

> **Estado de la v6:** la implementación de backend está presente, pero no
> debe considerarse cerrada. Según
> [`ESTADO_V6_Y_CUMPLIMIENTO.md`](docs/spec_kit/Versiones/version6/ESTADO_V6_Y_CUMPLIMIENTO.md),
> faltan la prueba de capas (fase 50), la verificación con Docker (fase 51),
> el frontend (fase 52) y el cierre (fase 53). Verifica todo en tu entorno
> antes de marcar tareas como completadas.

## Qué incluye la v5

- Lo nuevo que enseña la v5:
  - **Contraseñas con BCrypt** (ADR D23, Guía IA Regla 3): la clave llega en
    texto plano en `POST/PUT/PATCH /api/Usuario`, el **servicio** la convierte
    en hash y `clave_hash` **nunca** sale en ninguna respuesta.
  - **Login** (`POST /api/Usuario/login`): `200` con el usuario y sus roles
    vigentes; usuario inexistente, cuenta inactiva o clave incorrecta dan el
    **mismo** `401` (ADR D27, no se revela la causa).
  - **Lógica de negocio en funciones de la BD** (ADR D24): `EnlaceRegistro` y
    `ClaveAcceso` no son CRUD; la API llama `fn_emitir_enlace_api`,
    `fn_solicitar_clave_api` y `fn_validar_clave_api`. Los rechazos de negocio
    de la BD (clave incorrecta, usada, expirada, enlace vencido…) salen como
    `500` con el motivo en `detalle` (ADR D25).
  - **Secreto de un solo uso** (RNF12): la clave de 6 dígitos viaja en texto
    plano **una sola vez**, en la respuesta de `solicitar` (en producción
    llegaría por correo).
  - **`origen` validado en el servicio** (ADR D26): el alta manual de
    `RegistroAsistencia` solo admite `MANUAL`/`IMPORTADO` (`400`); `QR` y
    `ENLACE` solo nacen de validar una clave.
  - **Autenticar sí, autorizar no, en la API** (ADR D28-D29): la sesión es una
    cookie de **Flask** firmada con `SECRET_KEY`; `/admin` exige el rol
    `ADMIN` en el front y la API sigue abierta.
  - **Integridad desde tablas ya sembradas**: `DELETE /api/Asistente/1` → `500`
    por `registro_asistencia`.
- Front (Flask): **`/login`** y **`/logout`**; `/admin` redirige al login sin
  sesión y responde `403` sin el rol `ADMIN`; **autorregistro público**
  (`/registro` y `/registro/<token>`, sin login) y, en `/admin`, los 5
  bloques nuevos (Usuario, Rol por usuario, Enlace de registro, Clave de
  acceso y Registro de asistencia).
- Las **29 tablas** que hoy expone la API: las 24 de las versiones 1-4
  (listadas abajo) más las 5 de la v5: `Usuario`, `RolPorUsuario`,
  `EnlaceRegistro`, `ClaveAcceso` y `RegistroAsistencia` (21 endpoints
  nuevos).

## Lo que enseñó la v4 (sigue vigente)

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
- Las 24 tablas de las versiones 1-4:
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
| Front | Flask + Bootstrap 5 (identidad USB) · `openpyxl` para leer Excel · sesión de Flask para el login |
| Orquestación | Docker Compose |

Paquetes NuGet: `Npgsql`, `Dapper`, `Swashbuckle.AspNetCore` y, desde la v5,
`BCrypt.Net-Next` — y ninguno más (Artículo 2 de la constitución; el de
BCrypt es la única excepción, y solo se usa en `ServicioUsuario`). El front
no agrega paquetes.

## Cómo levantarlo

```powershell
docker compose up -d --build
```

| Recurso | URL |
|---|---|
| Diagnóstico | http://localhost:8142/ |
| Swagger UI | http://localhost:8142/swagger |
| API (ejemplo v5) | http://localhost:8142/api/RolPorUsuario?fkUsuario=1 |
| Front · inicio de sesión | http://localhost:5000/login |
| Front · registro de asistencia (público) | http://localhost:5000/registro |
| Front · calendario de una cátedra | http://localhost:5000/catedras/3975 |
| Front · detalle de sesión y ponentes | http://localhost:5000/sesiones/1 |
| Front · calendario del ponente | http://localhost:5000/ponente/ |
| Front · administración | http://localhost:5000/admin/ |
| Front · carga de datos | http://localhost:5000/carga_datos |
| PostgreSQL (herramienta externa) | localhost,15542 · base `bdcatedras_postgres_local` · `sa` / `Construccion123!` |

> **¿Vienes de la v3 con el volumen de PostgreSQL ya creado?** La v4 y la v5
> no agregan ninguna migración: no hay nada que reaplicar. (Si vienes de la v2
> o antes, mira `docs/spec_kit/Versiones/version3/7_quickstart.md` §1.)

Prueba de capas (sin PostgreSQL, 29 entidades, 345 verificaciones esperadas). Se
corre dentro del contenedor de la API (en Windows, el Control de
aplicaciones puede bloquear la DLL recién compilada):

```powershell
docker compose exec api dotnet run --project pruebas
```

Verificación de la v5 contra Docker real (62 casos; ESCRIBE datos de
ejemplo, así que se corre sobre un entorno recién reseteado):

```powershell
powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\verificar_v5.ps1
```

> La cuenta de ejemplo `admin.catedras` trae una clave que nadie conoce. Para
> iniciar sesión en el front, primero se fija una (se repite tras cada
> `down -v`):
> `Invoke-RestMethod -Method Patch -Uri http://127.0.0.1:8142/api/Usuario/1 -ContentType 'application/json' -Body '{"clave":"Admin12345"}'`
>
> Las pruebas del quickstart agregan y modifican datos de ejemplo. Para
> volver al estado original: `docker compose down -v && docker compose up -d --build`.

## Documentación de la v6

La carpeta `docs/spec_kit/Versiones/version6/` describe la versión de
**encuestas de calidad**: `Pregunta`, `OpcionPregunta`, `RespuestaEncuesta`
y `RespuestaItem`. Incluye `2_spec.md` (requisitos), `3_plan.md` (plan
técnico), `4_research.md` (decisiones), `5_data_model.md` (modelo de datos),
`6_contracts.md` (contratos), `7_quickstart.md` (smoke test), `8_tasks.md`
(fases 47–53), `ESTADO_V6_Y_CUMPLIMIENTO.md` (estado y discrepancias
detectadas) y **`guia_ia_v6.md`** (guía para trabajar fase por fase con una IA).

La v6 agrega la migración
`db/postgres/migraciones/004_sp_guardar_pregunta.sql`. Si se conserva un
volumen PostgreSQL anterior, la migración nueva puede necesitar aplicación
manual; consulta `version6/7_quickstart.md` antes de reiniciar o borrar datos.

**Estado:** en desarrollo; no declarar completada hasta que las fases 50–53
tengan evidencia verificable y se resuelvan explícitamente las discrepancias
documentadas contra `VERSION_2.md`.

## Estructura

```
docker-compose.yml              # un comando: postgres + api + front
db/postgres/
  script_bdcatedras.sql         # la BD completa, PROVISTA (se monta, no se genera)
  migraciones/                  # SP maestro-detalle PROPIOS (Guía IA, Regla 1):
                                #  001 programa (v2) · 002 asistente · 003 lote de carga (v3)
                                #  (v4 y v5: sin migraciones nuevas)
api_catedras/                   # la API .NET (Controllers, Modelos, Peticiones,
                                #  Servicios, Repositorios, Excepciones, pruebas;
                                #  pruebas/verificar_v5.ps1 = verificación contra Docker)
front_catedras/                 # el front Flask (routes/carga.py + servicios/carga_asistentes.py: carga;
                                #  routes/auth.py + auth_helpers.py: login; routes/registro.py: autorregistro)
docs/
  GUIA_ESTUDIANTE.md            # guía de arranque (v1)
  spec_kit/1_constitution.md    # reglas permanentes de todas las versiones
  spec_kit/guia_ia.md           # reglas técnicas transversales (SP, <select> de FK, BCrypt)
  spec_kit/Versiones/
    0_mapa_versiones.md         # qué tablas entran en cada versión
    version1/  2_spec .. 8_tasks
    version2/  2_spec .. 8_tasks
    version3/  2_spec .. 8_tasks
    version4/  2_spec .. 8_tasks + guia_ia_v4.md
    version5/  2_spec .. 8_tasks + guia_ia_v5.md
    version6/  2_spec .. 8_tasks + guia_ia_v6.md + estado/cumplimiento
```

## Documentación de la v5 (versión anterior)

En `docs/spec_kit/Versiones/version5/`: `2_spec.md` (qué y criterios de
aceptación, RF132-RF158), `3_plan.md` (cómo), `4_research.md` (decisiones
D22-D29), `5_data_model.md` (las 5 tablas, sus FK y los datos de ejemplo
útiles para probar), `6_contracts.md`, `7_quickstart.md` (smoke test),
`8_tasks.md` (fases 38-46) y **`guia_ia_v5.md`** (cómo trabajar esta
versión con un asistente de IA: orden de lectura, prompts por fase y
trampas conocidas). La documentación de la v4 sigue en `version4/`
(RF119-RF131, D19-D21, fases 32-37).

**Estado:** v5 completa y verificada en Docker (2026-10-11): `verificar_v5.ps1`
con **62 pasadas, 0 fallidas**, la prueba de capas con **345 pasadas, 0
fallidas** y las pantallas del front (login, 403 sin rol, autorregistro
público y los 5 bloques de administración) comprobadas a mano. Solo queda el
commit con el tag `v5` (Fase 46 de `8_tasks.md`).
