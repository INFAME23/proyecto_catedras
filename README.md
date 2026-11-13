# Proyecto Cátedras — Versión 7

API REST del sistema de **asistencia a cátedras universitarias**, construida
por **versiones incrementales**. Esta es la **v7: migración a ASIS,
bitácora y parámetros** (`LoteMigracion`, `DetalleMigracion`, `Bitacora`,
`Parametro`), la última del mapa: con ella la API expone las **37 tablas**
de `bdcatedras`. Se apoya en las encuestas de la v6, el login y registro de
asistencia de la v5, las sesiones de la v4, las personas de la v3, la
estructura académica de la v2 y la arquitectura en capas de la v1.

## Subversión v7.2 (asistencia por QR con 3 tipos de asistente)

Mantenimiento sobre V5–V7, **no es una V8**: sin tablas nuevas (sigue en 37 recursos) y con requisitos `RF-S8`…`RF-S14`,
decisiones `D60`…`D72` y las migraciones `060`–`066`. Documento completo, con lo decidido, lo verificado y los riesgos:
[docs/spec_kit/Versiones/version7_2/v7_2_subversion.md](docs/spec_kit/Versiones/version7_2/v7_2_subversion.md).

- **Registro público sin clave por correo (D60).** Quien escanea el QR ve la cátedra y tres opciones: **estudiante** (con su
  carnet), **docente** (cédula, nombre, contacto y facultad) y **externo** (cédula, nombre, contacto y de dónde viene). La clave de
  6 dígitos de la v5 ya no se usa (sus rutinas siguen en la API y la BD, «sin efecto desde D60»).
- **Ubicación solo en sesiones presenciales y contra la sede (D61, D62).** Si el enlace es de canal QR se pide la ubicación y debe
  caer dentro del **radio de la sede** (`sede.latitud/longitud` + `radio_metros`, o el parámetro `RADIO_VALIDACION_METROS` = 200 m);
  fuera del radio la asistencia **se rechaza** y no queda registro. En sesiones virtuales no se pide y `latitud`/`longitud` quedan
  `NULL`. Se avisa **antes** de que la persona escriba nada (aviso temprano) y se vuelve a verificar dentro del procedimiento
  almacenado. La modalidad `HIBRIDA` se eliminó (migración 072).
- **El QR no se genera si la sede no tiene coordenadas (D72)**, con un mensaje que nombra la sede a completar en `/admin/sedes`.
- **Carga de estudiantes** (`/carga_datos`): archivo `CC, Carnet_estudiante, Programa` + periodo en un `<select>`; es **idempotente**
  por carnet (D68): volver a subirlo actualiza, no duplica.
- **Exportación de fin de periodo** (`/admin/exportar`, solo ADMIN): por cátedra, un CSV de estudiantes, otro de docentes y otro de
  externos, el catálogo del periodo y un ZIP con todo. Sirve para contar **a cuántas cátedras asistió cada estudiante** (cuántos
  archivos contienen su carnet); el sistema no calcula ese total.
- **IP real detrás de un proxy:** pon `PROXY_CONFIABLES=1` en Render (ver `front_catedras/env.ejemplo`). Sin eso se guarda la IP del proxy.

**Aplicarlo a una base que ya existe** (el compose solo ejecuta las migraciones con un volumen nuevo), en orden y **antes** de
desplegar la API y el front nuevos:

```bash
for f in 060_sede_coordenadas 061_registro_publico_columnas 062_qr_exige_sede_con_coordenadas 063_sp_registro_publico 064_sp_cargar_estudiante 065_programas_archivo 066_exportacion_catedras; do docker exec -i mi_v1_catedras_postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -q < db/postgres/migraciones/$f.sql; done
```

Son idempotentes (se pueden repetir). Los 9 programas de la `065` (`INDS`, `ADMO`…) llevan datos **ficticios** con el sufijo «(mock)».

**Verificarlo:**

```bash
dotnet run --project api_catedras/pruebas                      # prueba de capas (689 pasadas; 1 fallida que ya fallaba: Bitácora)
docker exec mi_v1_catedras_front python pruebas/prueba_v72.py  # front con la API simulada (104 pasadas)
powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\verificar_v72.ps1   # API y PostgreSQL reales (114 pasadas); ESCRIBE datos de prueba
```

Prueba SQL de las migraciones (no deja rastro): `db/postgres/pruebas/prueba_migraciones_v72.sql` (118 casos).

## Subversión v7.1 (mantenimiento sobre V5–V7)

Sin tablas de negocio nuevas en el mapa (sigue en 37 recursos) y sin romper el
orden de versiones; sus requisitos se numeran `RF-S1`…`RF-S7`. Documento
completo, con las decisiones (D38–D59), lo verificado y lo pendiente:
[docs/spec_kit/Versiones/version7_1/v7_1_subversion.md](docs/spec_kit/Versiones/version7_1/v7_1_subversion.md).

- **Solo el administrador inicia sesión (D55).** Estudiantes y ponentes **no tienen cuenta ni panel**: consultan
  el catálogo y registran su asistencia sin iniciar sesión, con el QR de la sesión. El administrador crea las
  cátedras y sus sesiones, asigna ponentes, genera y proyecta el QR de cada sesión y revisa la asistencia.
- **Sin inscripción a cátedras y sin cupo** (D55): se quitaron (la asistencia por QR es pública).
- **Geolocalización obligatoria** al autorregistrar la asistencia: el navegador
  entrega latitud y longitud; sin ellas no se registra (front, API y BD lo
  exigen). Se ve en `/admin` con un enlace al mapa.
- **QR de asistencia por sesión** (D53): se genera al crear la cátedra y sirve en los últimos minutos de la sesión.
- **Disponibilidad de horario** (D52): al crear una cátedra se ve la ocupación de una fecha y no se pueden
  cruzar dos sesiones en la misma sede y el mismo salón.
- **Cambiar la clave** (`/perfil/clave`, y obligatorio si al administrador se le entregó una clave temporal) y
  **recuperarla** por un código de un solo uso enviado por correo (`/recuperar`).
- **Panel de `/admin` con categorías desplegables** (cerradas por defecto, sin barra lateral).
- **Calendario público y «Soy ponente» (D56).** `/calendario` (en el navbar, sin iniciar sesión) muestra las sesiones de todas
  las cátedras por mes, semana o lista. Su botón **«Soy ponente»** abre un formulario (documento, perfil profesional,
  dirección, teléfono, correo institucional, ponencia y resumen) y **elige la fecha, la hora, la modalidad, la sede y el periodo de su cátedra nueva**; la solicitud llega
  a la **bandeja del administrador** (`/admin/solicitudes-ponente`, con el número de pendientes en `/admin`), que la **aprueba** (queda
  registrado como persona y **la cátedra se crea sola** con su sesión, su QR y él como ponente; el administrador puede ajustar modalidad, sede,
  periodo y salón en la misma pantalla) o la **rechaza** con un motivo. Necesita las migraciones `055`, `057` y `058` y reconstruir la API y el front.
- **Periodos y horarios de programación (D57).** En `/admin` → «Estructura académica y cátedras» → «Periodos y horarios», el administrador define
  las fechas, horas y días en que se pueden programar cátedras (por ejemplo, un periodo de agosto a diciembre, de 06:00 a 21:00, de lunes a sábado).
  Con alguna ventana activa, la base de datos rechaza toda sesión nueva (o movida) que no quepa. Sin ventanas activas no hay restricción. Necesita la migración `056`.
- **Una hora entre sesiones y enlace de ingreso (D59).** Todas las cátedras usan el mismo enlace, así que la base de datos **no deja crear dos sesiones a la misma hora
  (en ninguna modalidad ni sede) y exige al menos 60 minutos entre una y otra** (parámetro `MINUTOS_ENTRE_SESIONES`; solo cuentan las cátedras activas). Al crear la cátedra
  (y al aprobar a un ponente) hay un campo **«Enlace de ingreso»** (`http://` o `https://`). Necesita la migración `059`. Las cátedras activas del catálogo se redujeron a 500 (el resto quedó
  desactivado, no borrado: «Ver inactivas»).

> **Si ya tienes el volumen de PostgreSQL creado**, las migraciones 045-059 no
> corren solas (initdb solo corre con el volumen vacío). Si ya tenías hasta la 056, **bastan la 057, la 058 y la 059** (reconstruye la API y el front antes o a la vez: cambian los procedimientos del formulario «Soy ponente»)
> (primero reconstruye la API y el front con `docker compose up -d --build`; la 055 es repetible). Si
> tenías hasta la 053, la 054 borra la inscripción y la API anterior la usaba: reconstruye antes. Para aplicarlas en orden:
>
> ```bash
> for n in 045_geolocalizacion_registro 046_roles_estudiante_ponente 047_codigo_recuperacion 048_semilla_estudiante 049_inscripcion_catedra 050_disponibilidad_sesiones 051_qr_sesion 052_primer_ingreso_estudiante 053_qr_canal_por_modalidad 054_sin_inscripcion_ni_cupo_login_admin 055_solicitud_ponente 056_restriccion_programacion 057_solicitud_ponente_catedra_nueva 058_solicitud_ponente_modalidad_sede_periodo 059_separacion_entre_sesiones_enlace; do
>   docker compose cp db/postgres/migraciones/$n.sql postgres:/tmp/m.sql
>   docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -v ON_ERROR_STOP=1 -f /tmp/m.sql
> done
> ```
>
> Sin la 045, validar una clave responde `42883: function fn_validar_clave_geo_api
> does not exist`; sin la 047, la recuperación de clave falla; sin la 050, la consulta de disponibilidad falla;
> sin la 051 y la 053, el QR de asistencia falla; sin la 054, el cupo sigue limitando los registros y la tabla de
> inscripción sigue en la base (la aplicación ya no la usa); sin la 055, el formulario «Soy ponente» y la bandeja
> responden error (`relation "solicitud_ponente" does not exist`); sin la 056, «Periodos y horarios» responde error (`relation "restriccion_programacion" does not exist`); sin la 057 y la 058, el formulario «Soy ponente» y la aprobación fallan (`procedure ... does not exist`); sin la 059, no se aplica el margen de una hora entre sesiones y la aprobación del ponente falla (la API ya manda el enlace de ingreso).

**QR de asistencia (D53).** Cada sesión tiene un QR que el administrador proyecta o comparte al final (pantalla «QR de asistencia» de la sesión, en `/admin`). Para que sirva en los celulares de quienes asisten, el front debe abrirse con una dirección que ellos alcancen y **en https** (el navegador solo da la ubicación en https o localhost): pon esa dirección en `URL_PUBLICA` (ver `front_catedras/env.ejemplo`). Con el correo configurado, la clave de asistencia llega por correo y no se muestra en pantalla.

**Correo (RF-S6).** El código de recuperación se envía por SMTP configurado
con variables de entorno; copia `front_catedras/env.ejemplo` como
`front_catedras/.env` (o como `.env` junto al compose) y llena lo de tu
proveedor. Sin `SMTP_HOST` el correo queda apagado y la pantalla avisa. Para
desarrollo sin proveedor: `CORREO_MODO_CONSOLA=1` escribe el correo en el log
del front. Qué proveedor usar lo decide el equipo.

**Ubicación en el navegador.** Solo funciona en `https://` o `localhost`; abrir
el front por la IP de la red (`http://192.168.x.x:5000`) la deniega siempre.

Verificación contra la API real: `pruebas/verificar_v71.ps1` (ver el
documento de la subversión para los números y el resto).

## Qué incluye la v7

- Lo nuevo que enseña la v7:
  - **Reutilizar un procedimiento provisto en vez de reescribirlo** (ADR
    D34): `POST /api/LoteMigracion {"fk_sesion": 5712}` hace `CALL
    sp_crear_lote_migracion`, que llama a `sp_generar_lote_migracion` (del
    script de la BD). Ese procedimiento cuenta los registros migrables,
    arma el nombre del archivo, inserta el lote y sus filas y deja la
    sesión en `MIGRADA`, todo en el mismo `CALL`. La API no toca `Sesion`.
  - **Un efecto colateral real sobre otra versión** (RF184): generar un
    lote cambia `sesion.estado` (v4). Se documenta, no se "arregla".
  - **Un índice único parcial como regla de negocio**: `ux_detmig_aceptado`
    impide que un registro de asistencia quede `ACEPTADO` en dos lotes; el
    segundo intento responde 500 y la API no lo revalida (D9).
  - **Una entidad cuyo ciclo de vida sigue después de crearse** (D35): un
    lote nace `GENERADO` y avanza (`EN_COLA`, `EN_CURSO`, `CORRECTO`…) con
    `PATCH`, según responda ASIS; cada fila tiene su `PATCH` de resultado.
  - **Por qué una bitácora no se edita** (D36, D37-A): `Bitacora` es de
    solo lectura en la API; la escriben los triggers `trg_bitacora_registro`
    y `trg_bitacora_asistente`. Los verbos que no existen responden 405.
  - `Parametro`: CRUD con llave de texto; `modificado_en` lo pone el
    procedimiento, no un trigger ni el código C#.
- Front (Flask), todo bajo `/admin` (protegido por rol `ADMIN`): parámetros
  (CRUD), lotes de migración (lista, detalle con sus filas, estado del lote
  y resultado por fila), detalle de migración y bitácora (solo lectura). En
  el detalle de una sesión terminada con registros, un administrador ve el
  botón **"Generar migración"**.
- Decisiones del equipo al revisar el borrador contra el código (ADR D37):
  JSON en snake_case, bitácora de solo lectura, sin filtro por usuario en
  la bitácora, `clave` de parámetro en mayúsculas, front dentro de
  `routes/admin.py`.

## Maestro-detalle en el front (después de la v7)

En `/admin`, **al entrar a un maestro aparecen sus detalles**: cada uno
tiene una pantalla con su ficha y las tablas de sus detalles, y crear o
editar el maestro lleva directo a ella (Guía IA, Regla 4). Las 10 parejas:
cátedra → sesiones; sesión → enlaces de registro y registros de
asistencia; asistente → documentos y consentimientos; encuesta →
preguntas; pregunta → opciones; registro de asistencia → su respuesta de
encuesta; respuesta → sus ítems; programa → sus alias. Ejemplos:
http://localhost:5000/admin/catedras/3975 ·
http://localhost:5000/admin/encuestas/1 ·
http://localhost:5000/admin/programas/M0286.

Y en el **formulario** de cada maestro se agregan sus detalles en filas
("Agregar sesión", "Agregar pregunta", "Agregar alias"…). Al crear, maestro
y detalles se guardan en **una sola transacción** (Guía IA, Regla 1); para
eso hay 3 procedimientos nuevos que reutilizan los existentes:

| Migración | Procedimiento | Guarda junto |
|---|---|---|
| `042` | `sp_guardar_catedra` | cátedra + sus sesiones (`POST /api/Catedra` acepta `sesiones`) |
| `043` | `sp_guardar_encuesta` | encuesta + sus preguntas + las opciones de cada una (`POST /api/Encuesta` acepta `preguntas`) |
| `044` | `sp_guardar_sesion` | sesión + su primer enlace de registro (`POST /api/Sesion` acepta `enlace`) |

Al editar, cada fila nueva se guarda con su propio POST y las que fallan
vuelven al formulario con el motivo. Detalle completo en
`docs/spec_kit/guia_ia.md` (Reglas 1 y 4).

> **Volumen de PostgreSQL ya creado:** las migraciones 042-044 solo corren
> solas con una BD nueva. Si conservas el volumen, aplícalas a mano (son
> `CREATE OR REPLACE`):
> `docker compose cp db/postgres/migraciones/042_sp_guardar_catedra.sql postgres:/tmp/m.sql` y
> `docker compose exec -T postgres psql -U sa -d bdcatedras_postgres_local -f /tmp/m.sql`
> (igual con 043 y 044). Sin ellas, crear una cátedra, encuesta o sesión
> responde `42883: procedure ... does not exist`.

Pruebas: `db/postgres/pruebas/prueba_sp_maestro_detalle_formulario.sql`
(32 casos, incluido el todo-o-nada) y 16 verificaciones nuevas en la
prueba de capas (`pruebas/Programa.cs`, bloque "Maestro-detalle en el
formulario").

## Lo que enseñó la v6 (sigue vigente)

- Lo nuevo que enseña la v6:
  - **Maestro-detalle de nacimiento** (Guía IA, Regla 1 · ADR D30):
    `POST /api/Pregunta` acepta una lista opcional `opciones` y guarda la
    pregunta con sus opciones iniciales en una sola transacción
    (`sp_guardar_pregunta`), igual mecánica que `ProgramaAcademico`/`alias`
    en v2 pero sin ser retrofit. Si una opción falla (`orden` repetido), no
    se crea ni la pregunta.
  - **"Responder" y "cerrar" son una sola operación** (ADR D31): `POST
    /api/RespuestaEncuesta` llama a `fn_responder_encuesta_api`, que ya
    existía desde la v1, inserta el maestro y todos los `RespuestaItem` del
    detalle, y ella misma cierra la encuesta — todo en el mismo `CALL`. No
    hay un verbo "cerrar" aparte. Falta una pregunta obligatoria, lista
    vacía, registro sin encuesta asignada o ya respondido → `500`, y no
    queda nada guardado (ADR D25, mismo bucket que cualquier otro error de
    PostgreSQL).
  - **Hecho auditable** (ADR D32, igual criterio que `ConsentimientoDatos`
    en v3 y `RegistroAsistencia` en v5): `RespuestaEncuesta` no tiene `PUT`,
    `PATCH` ni `DELETE` — `uq_respuesta_registro` garantiza una sola
    respuesta por registro, para siempre.
  - **El tipo de pregunta es DATO, no código** (RN-25): `fk_tipo_pregunta`
    decide, vía `guarda_numero`/`guarda_texto`/`guarda_opcion` de
    `TipoPregunta` (v1), en cuál de las tres columnas de `RespuestaItem` se
    espera la respuesta; la API no lo revalida (D9), lo vigila el trigger
    `fn_trg_respuesta_item_tipo` al responder.
- Front (Flask): en el detalle de una sesión con encuesta asignada, un
  aviso que cambia según el caso (sin sesión invita a iniciar sesión; sin
  registro de asistencia en esa sesión avisa que solo responde quien
  asistió; ya respondida lo confirma) y, si corresponde, el enlace
  **"Responder encuesta"**. El formulario dibuja un campo por pregunta
  según su tipo real: numérico para `ESCALA_5`/`SI_NO`, `<textarea>` para
  `TEXTO_LIBRE`, `<select>` de `OpcionPregunta` para `OPCION_UNICA`. En
  `/admin`, los 4 bloques nuevos: `Pregunta` (con sus opciones iniciales al
  crear), `OpcionPregunta` (CRUD completo), `RespuestaEncuesta` y
  `RespuestaItem` (ambas solo lectura).
- Con la v6 la API llegó a **33 tablas** (hoy son 37 con la v7): las 29
  de las versiones 1-5 (listadas abajo) más las 4 de la v6: `Pregunta`, `OpcionPregunta`,
  `RespuestaEncuesta` y `RespuestaItem` (19 endpoints nuevos).

## Lo que enseñó la v5 (sigue vigente)

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
- Las **29 tablas** que trajo hasta la v5: las 24 de las versiones 1-4
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
  **calendario del ponente** (`/ponente/`; con sesión iniciada abre el
  calendario propio, y se puede buscar el de otra persona) y, en `/admin`,
  el CRUD de `Sesion` y la asignación de `Ponencia`.
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
| API (ejemplo v7) | http://localhost:8142/api/LoteMigracion?fkSesion=5712 |
| Front · inicio de sesión | http://localhost:5000/login |
| Front · registro de asistencia (público) | http://localhost:5000/registro |
| Front · calendario de una cátedra | http://localhost:5000/catedras/3975 |
| Front · detalle de sesión, ponentes y encuesta | http://localhost:5000/sesiones/1 |
| Front · calendario del ponente | http://localhost:5000/ponente/ |
| Front · administración | http://localhost:5000/admin/ |
| Front · lotes de migración a ASIS | http://localhost:5000/admin/lotes-migracion |
| Front · carga de datos | http://localhost:5000/carga_datos |
| PostgreSQL (herramienta externa) | localhost,15542 · base `bdcatedras_postgres_local` · `sa` / `Construccion123!` |

> **¿Vienes de un volumen de PostgreSQL ya creado?** Docker solo corre las
> migraciones (`docker-entrypoint-initdb.d`) la primera vez, con el volumen
> vacío. Si tu volumen es anterior a `041_sp_respuesta_item.sql`, la API
> responde `42883: procedure ... does not exist` porque a la BD le faltan
> procedimientos. Dos salidas: `docker compose down -v && docker compose up
> -d --build` (pierdes los datos de ejemplo que hayas modificado), o aplicar
> las migraciones faltantes sin perderlos con
> `db/postgres/aplicar_migraciones.ps1` (es idempotente: usa `CREATE OR
> REPLACE`, así que se puede correr aunque algunas ya estén aplicadas).

**Verificación completa de la v7 en un comando** (BD nueva, procedimientos,
prueba de capas, regresión v5 y retrofit, criterios de la v7 y una BD nueva
al final con la clave del administrador fijada):

```powershell
powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\correr_todo_v7.ps1
```

Solo los criterios 1-6 de la v7 (71 casos, sobre una BD recién creada):
`.\api_catedras\pruebas\verificar_v7.ps1`.

Prueba de capas (sin PostgreSQL, 37 entidades, 452 verificaciones esperadas). Se
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

## Documentación de la v7

La carpeta `docs/spec_kit/Versiones/version7/` describe la versión de
**migración a ASIS, bitácora y parámetros**: `2_spec.md` (RF179-RF197 y
criterios de aceptación), `3_plan.md`, `4_research.md` (decisiones D34-D37),
`5_data_model.md`, `6_contracts.md`, `7_quickstart.md`, `8_tasks.md` (fases
54-61, con la evidencia de cada casilla), **`guia_ia_v7.md`** (guía para
trabajar la v7 con una IA) y `prompts/` (el registro de prompts del §11 de
`VERSION_2.md`, por fase).

Las capas de las 4 entidades nacieron durante el retrofit a procedimientos
(migraciones `013`, `014`, `031` y `032`); la v7 las auditó contra el spec,
cerró la escritura de la bitácora, agregó la validación de `clave`, la
prueba de capas de las 37 entidades y el front.

**Estado (2026-10-15):** fases 54-61 hechas. Verificado contra PostgreSQL 16
real y la API real por HTTP: `verificar_v7.ps1` **71/0**, prueba de capas
**452/0** (37 entidades), regresión `verificar_retrofit_fase4.ps1` **80/0**,
`verificar_v5.ps1` **63/0**, SQL de procedimientos **160/160**, y el front
de punta a punta **49/0**. Falta correr `correr_todo_v7.ps1` contra Docker,
probar el criterio 8 a mano y, si todo pasa, quitar el banner BORRADOR y
crear el tag `v7` (Fase 61 de `8_tasks.md`).

### Cómo crear el tag

```powershell
git checkout main
git pull
git tag -a v7 -m "Versión 7: migración a ASIS, bitácora y parámetros"
git push origin v7
```

## Documentación de la v6 (versión anterior)

La carpeta `docs/spec_kit/Versiones/version6/` describe la versión de
**encuestas de calidad**: `Pregunta`, `OpcionPregunta`, `RespuestaEncuesta`
y `RespuestaItem`. Incluye `2_spec.md` (requisitos), `3_plan.md` (plan
técnico), `4_research.md` (decisiones), `5_data_model.md` (modelo de datos),
`6_contracts.md` (contratos), `7_quickstart.md` (smoke test), `8_tasks.md`
(fases 47–53), `ESTADO_V6_Y_CUMPLIMIENTO.md` (estado y discrepancias
detectadas) y **`guia_ia_v6.md`** (guía para trabajar fase por fase con una IA).

El alta de `Pregunta` (maestro-detalle con sus opciones) usa
`sp_guardar_pregunta`, de la migración `004_sp_guardar_pregunta.sql`. Los
otros 4 verbos de `Pregunta` y `OpcionPregunta`, y los de `RespuestaEncuesta`
y `RespuestaItem`, se agregaron después, junto con el retrofit a
procedimientos de `Usuario`, `RolPorUsuario`, `EnlaceRegistro`,
`ClaveAcceso` y `RegistroAsistencia` (v5): son las migraciones
`033`-`041` (ver
[`AJUSTES_RETROFIT_SP_LOTE_FASE5.md`](docs/spec_kit/Versiones/AJUSTES_RETROFIT_SP_LOTE_FASE5.md)).
Si se conserva un volumen PostgreSQL anterior a estas migraciones, hay que
aplicarlas a mano; ver la nota de "Cómo levantarlo" arriba
(`aplicar_migraciones.ps1`).

**Estado:** v6 completa y cerrada con su tag `v6`: prueba de capas
**385 pasadas, 0 fallidas**; los 33 recursos verificados contra Docker
real; el front comprobado a mano. Nota de la v7: la prueba de capas de la
v6 en realidad no cubría `Pregunta`, `OpcionPregunta`, `RespuestaEncuesta`
ni `RespuestaItem`; la Fase 58 de la v7 las agregó. Siguen abiertas, fuera
de cualquier versión, las discrepancias de `ESTADO_V6_Y_CUMPLIMIENTO.md`
(409 en vez de 500, carpeta de prompts de v1-v6, cierre formal de la v1).

## Estructura

```
docker-compose.yml              # un comando: postgres + api + front
db/postgres/
  script_bdcatedras.sql         # la BD completa, PROVISTA (se monta, no se genera)
  migraciones/                  # 001-041: maestro-detalle propios (Guía IA, Regla 1:
                                #  001 programa, 002 asistente, 003 lote de carga, 004 pregunta)
                                #  + el retrofit a SP de todas las tablas de la API (005-041;
                                #  033-041 = Usuario, RolPorUsuario, EnlaceRegistro, ClaveAcceso,
                                #  RegistroAsistencia, Pregunta, OpcionPregunta, RespuestaEncuesta,
                                #  RespuestaItem). Ver PLAN_RETROFIT_SP.md.
  aplicar_migraciones.ps1       # aplica 001-041 a una BD ya creada, sin perder datos (ver arriba)
                                #  042-044: maestro + detalles desde el formulario (sp_guardar_catedra,
                                #  sp_guardar_encuesta, sp_guardar_sesion)
api_catedras/                   # la API .NET (Controllers, Modelos, Peticiones,
                                #  Servicios, Repositorios, Excepciones, pruebas;
                                #  pruebas/verificar_v5.ps1, verificar_v7.ps1 y
                                #  correr_todo_v7.ps1 = verificación contra Docker)
front_catedras/                 # el front Flask (routes/carga.py + servicios/carga_asistentes.py: carga;
                                #  routes/auth.py + auth_helpers.py: login; routes/registro.py: autorregistro)
docs/
  GUIA_ESTUDIANTE.md            # guía de arranque (v1)
  spec_kit/1_constitution.md    # reglas permanentes de todas las versiones
  spec_kit/guia_ia.md           # reglas técnicas transversales (SP, <select> de FK, BCrypt,
                                #  maestro -> sus detalles en el front)
  spec_kit/Versiones/
    0_mapa_versiones.md         # qué tablas entran en cada versión
    version1/  2_spec .. 8_tasks
    version2/  2_spec .. 8_tasks
    version3/  2_spec .. 8_tasks
    version4/  2_spec .. 8_tasks + guia_ia_v4.md
    version5/  2_spec .. 8_tasks + guia_ia_v5.md
    version6/  2_spec .. 8_tasks + guia_ia_v6.md + estado/cumplimiento
    version7/  2_spec .. 8_tasks + guia_ia_v7.md + prompts/ (última versión)
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