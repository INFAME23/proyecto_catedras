# Plan V8–V11 · Cambios por versión y decisiones tomadas

> **Para la IA que lea este archivo:** este es el plan aprobado por el equipo
> para las versiones V8 a V11 del Proyecto Cátedras. Úsalo como fuente de
> verdad al escribir el spec kit de cada versión (`2_spec.md` … `8_tasks.md`
> en `docs/spec_kit/Versiones/versionN/`) y al construirlo. Respeta la
> [constitución](../1_constitution.md) (la BD manda, capas con interfaces,
> todo en español y comentado, un solo comando) y no contradigas los ADR de la
> sección 2. Si algo del plan choca con la BD real
> (`db/postgres/script_bdcatedras.sql`), **gana la BD** y se registra un ADR nuevo.
>
> Documentos relacionados:
> - [historias_usuario_cliente.md](historias_usuario_cliente.md): las 25 historias con sus criterios de aceptación
> - [decisiones_v8_v11.md](decisiones_v8_v11.md): los ADR D55–D70
> - [../../reuniones/2026-11-04_reunion_cliente.md](../../reuniones/2026-11-04_reunion_cliente.md): la transcripción de la reunión
> - `docs/Cambios_proyecto_catedras_V8-V11.pdf`: este mismo plan en PDF

**Origen:** reunión con el cliente (Oficina de Cátedras Abiertas, USB Medellín)
del 2026-11-04 · **Punto de partida:** v7.1 construida (37 tablas, migraciones
001–053) · **Método:** SDD, un spec kit por versión · **Rama:** `feature/rama-Jhoan.v`

---

## 1. Resumen del plan

El cliente aclaró tres cosas:
- el aplicativo sirve para **registrar la asistencia** a las cátedras;
- también sirve para que los **ponentes propongan sus cátedras**;
- **solo la administración inicia sesión**.

| Versión | Qué se hace | Al terminar se puede… | Quién |
|---|---|---|---|
| **V8** Registro sin login | QR con 3 tipos de usuario (estudiante por ID; docente y externo por formulario), ubicación según la modalidad y geocerca. Se apaga lo que sobra de la v7.1 | Usar el sistema en una cátedra real | Todo el equipo |
| **V9** QR dinámico | Código que cambia cada 60 s, enlace secreto del ponente y pantalla de proyección | Proyectar un QR que no sirve si se comparte | Persona A (en paralelo) |
| **V10** Calendario y solicitudes | Franjas por sede, calendario público, solicitud del ponente, bandeja del admin y aprobación | Que el ponente proponga y el admin apruebe | Persona B (en paralelo) |
| **V11** Archivos y reportes | Archivo ASIS configurable, generación automática a las 21:00, archivo general, importación, ponentes y estadísticas | Subir a ASIS y SNIES sin digitar | Equipo, al final |

**Reglas que se mantienen (constitución):**
- no se edita `script_bdcatedras.sql`;
- cada cambio va en una **migración nueva (054 en adelante)**, montada en `docker-compose.yml` como `z0NN_…` y con su procedimiento almacenado;
- la arquitectura sigue en capas: Controller → Servicio → Repositorio (`CALL catedras.sp_…`) → BD;
- todo el sistema se levanta con `docker compose up -d --build`;
- si el volumen de PostgreSQL ya existe, las migraciones se aplican a mano, en orden.

### Orden de trabajo

| Paso | Qué | Depende de |
|---|---|---|
| 1 | Spec kit y construcción de la **V8** (todo el equipo) | Estas decisiones |
| 2a | Spec kit y construcción de la **V9**, en su rama | V8 fusionada en `main` |
| 2b | Spec kit y construcción de la **V10**, en su rama, *en paralelo con la V9* | V8 fusionada en `main` |
| 3 | Spec kit y construcción de la **V11** | V8 y V10 (datos del ponente) |
| 4 | Actualizar `0_mapa_versiones.md`, el README, la prueba de capas (`api_catedras/pruebas/Programa.cs`) y los tags (v6 a v11) | Cada versión |

---

## 2. Decisiones tomadas (ADR D55–D70)

Las tomó el equipo el 2026-11-04 y continúan la numeración de la v7.1 (D38–D54).
Si el cliente responde distinto a una pregunta pendiente, se agrega un ADR nuevo
que reemplaza al anterior; estos no se editan.

| ADR | Tema | Decisión | Versión |
|---|---|---|---|
| **D55** | Validación del estudiante | Se registra **solo con su ID**. La seguridad la dan el QR dinámico (D63) y la ubicación (D60–D61). **Se quita la clave por correo** del registro; `clave_acceso` queda sin uso para registros nuevos | V8 / V9 |
| **D56** | Docente sin programa | `tipo_vinculacion.DOCENTE` pasa a `exige_programa = false` (como `ADMINISTRATIVO`); si no, el disparador RN-07 rechazaría el registro. El registro guarda la **facultad** | V8 |
| **D57** | Docentes y externos | Nuevo tipo de vinculación **`EXTERNO`** (`es_interno = false`, `exige_programa = false`) y una columna de **procedencia** (barrio o institución). Un procedimiento crea en **una sola transacción** el `asistente`, su `documento_asistente` (cédula vigente) y su `vinculacion_asistente`. Si la cédula ya existe, **reutiliza** a la persona | V8 |
| **D58** | Lo de la v7.1 | **Se apaga sin borrar**: se quitan las rutas y los menús, los usuarios `ESTUDIANTE`/`PONENTE` quedan inactivos y los QR nuevos salen con `requiere_inscripcion = false`. Las tablas, los procedimientos y las pruebas viejas se conservan | V8 |
| **D59** | Formato del ID | **Alfanumérico de 5 a 15**, como `chk_asistente_idasis` (hay IDs reales como `1000513C`). En el celular se abre el teclado numérico (`inputmode="numeric"`), pero no se rechazan las letras | V8 |
| **D60** | Ubicación por modalidad | `PRESENCIAL`: obligatoria · `HIBRIDA`: se pide y se guarda si la dan, pero no bloquea · `VIRTUAL`/`TELEPRESENCIAL`: no se pide | V8 |
| **D61** | Geocerca | En las presenciales la ubicación debe estar **dentro de un radio de la sede** (`GEOCERCA_RADIO_METROS = 300`). Cada `sede` lleva latitud y longitud. Fuera del radio no se registra | V8 |
| **D62** | Quién proyecta el QR | Al aprobar la cátedra, el ponente recibe por correo un **enlace secreto** que abre el QR de **sus** sesiones. El admin también puede abrirlo desde `/admin`. No se crea un rol nuevo | V9 / V10 |
| **D63** | Margen del QR | El código **vive 60 s** y se acepta también **el inmediatamente anterior** (`QR_ROTACION_SEGUNDOS = 60`, `QR_CODIGOS_TOLERADOS = 1`). Se deriva del tiempo (estilo TOTP) a partir de un secreto del enlace; **no** se crea una fila por minuto | V9 |
| **D64** | Franjas | **1 hora por fecha y sede**; una sola cátedra por franja y sede (en sedes distintas sí pueden coincidir). Hay una **plantilla semanal** para repetirlas | V10 |
| **D65** | Archivo ASIS | Columnas **configurables** (`ASIS_COLUMNAS`). El valor inicial es **programa, ID, cédula** | V11 |
| **D66** | Encuestas | Si la sesión tiene encuesta, se **ofrece sin obligar** después del registro, a los 3 tipos de usuario (se reutiliza la V6) | V8 |
| **D67** | Tratamiento de datos | **Casilla obligatoria** para docente y externo (Ley 1581), con un enlace a la política de la USB. Se guarda en `consentimiento_datos` | V8 |
| **D68** | Generación automática | **4.º servicio en docker-compose** (un contenedor liviano con cron) que llama a la API a la `HORA_CIERRE_JORNADA` (21:00). También hay un botón manual en `/admin` | V11 |
| **D69** | Numeración | **V8, V9, V10 y V11** (`version8/` a `version11/`). Migraciones desde la **054** | Todas |
| **D70** | Reparto del equipo | V8 entre todos; después la **V9 y la V10 en paralelo** (cada una en su rama y con su PR a `main`); la V11 al final | Todas |

---

## 3. Lo que se apaga o cambia de la v7.1

Por la decisión D58 **nada se borra**: se quita del front y queda sin uso en la API y la BD.

| Lo que hay hoy (v7.1) | Qué pasa | Versión |
|---|---|---|
| Login con roles `ESTUDIANTE` y `PONENTE` (migración 046) | Solo `ADMIN` inicia sesión; los usuarios de esos roles quedan inactivos | V8 |
| Panel `/estudiante` e inscripción a cátedras (049) | Rutas y menús fuera; la API `InscripcionCatedra` queda sin uso | V8 |
| El QR exige inscripción (`requiere_inscripcion`, `trg_registro_inscripcion`, 051) | Los QR nuevos se emiten con `requiere_inscripcion = false` | V8 |
| Cupo que bloquea el registro (RN-38) | Las sesiones se crean sin cupo por defecto; no bloquea | V8 |
| Primer ingreso del estudiante y cambio obligatorio de clave (052) | No aplica; `_exigir_cambio_de_clave` de `app.py` solo queda para el admin | V8 |
| Registro con identificador + clave por correo (`ClaveAcceso/solicitar` y `/validar`) | Estudiante por ID; docente y externo por formulario; sin clave (D55) | V8 |
| Ubicación obligatoria en todo registro (045) | Según la modalidad (D60), con geocerca en las presenciales (D61) | V8 |
| `/ponente` y `/ponente/catedras` con login | Se apagan en la V8 y los reemplazan el calendario público y la solicitud en la V10 | V8 / V10 |
| QR fijo por sesión (`sp_emitir_qr_sesion`) | QR dinámico de 60 s | V9 |
| Archivo ASIS fijo de 3 columnas: reunión, ID, programa (`fn_exportar_asis`, V7) | Columnas configurables (D65) | V11 |

Archivos que toca el apagado (V8): `front_catedras/auth_helpers.py`, `app.py`,
`routes/auth.py`, `routes/estudiante.py`, `routes/ponente.py`,
`routes/ponente_catedras.py`, `routes/registro.py`, `routes/perfil.py`, las
plantillas `estudiante_*` y `ponente_*`, y `ServicioUsuario.LoginAsync`.

---

## 4. V8 · Registro de asistencia sin login, por tipo de usuario

**Objetivo:** que cualquier persona registre su asistencia escaneando el QR, sin
cuenta, según su tipo de usuario. Es la versión que el cliente usa todos los días.
**Depende de:** v7.1 (registro, enlace y ubicación).

### Historias de usuario

| HU | Qué hace |
|---|---|
| HU-01 | Catálogo público de cátedras, sin login y sin datos personales |
| HU-02 | Solo el administrador inicia sesión; lo demás de la v7.1 se apaga |
| HU-03 | Al abrir el QR se elige el tipo: Estudiante USB, Docente/Administrativo o Externo |
| HU-04 | Estudiante: solo su ID. El sistema toma su programa vigente (RN-22) |
| HU-05 | Docente/Administrativo: cédula, nombre, contacto y facultad |
| HU-06 | Externo: cédula, nombre, contacto y procedencia (barrio o institución) |
| HU-07 | Una sola asistencia por persona y sesión |
| HU-08 | Aceptación obligatoria del tratamiento de datos (docente y externo) |
| HU-10 | Ubicación según la modalidad, con geocerca en las presenciales (*se adelanta de la V9 porque cambia el registro*) |

**Decisiones que aplican:** D55, D56, D57, D58, D59, D60, D61, D66 y D67.

### Cambios por capa

**Base de datos** (definitiva en [version8/5_data_model.md](version8/5_data_model.md)):
- **054**:
  - tipo de vinculación `EXTERNO`;
  - `DOCENTE` con `exige_programa = false`;
  - `asistente.fk_facultad` y `asistente.procedencia`;
  - en el registro, `fk_facultad_snapshot` y `distancia_sede_metros`.
- **055**: latitud, longitud y radio en `sede`; parámetro `GEOCERCA_RADIO_METROS`; `fn_distancia_metros` (Haversine).
- **056**: `sp_registrar_estudiante` (por ID), `sp_registrar_docente` y `sp_registrar_externo`. Cada uno guarda en un solo `CALL` el asistente, su documento, su vinculación, el consentimiento y el registro, con la ubicación por modalidad, la geocerca y la clave técnica (D71).
- **057**: roles `ESTUDIANTE`/`PONENTE` cerrados y QR nuevos sin `requiere_inscripcion`.
- El índice único `(fk_sesion, fk_asistente)` **ya existe** en el script (`uq_registro_sesion_asis`): no hace falta migración.

**API (.NET):**
- `RegistroController` nuevo con `POST /api/Registro/estudiante`, `/docente` y `/externo`.
- Servicio y repositorio de registro con el patrón de capas de siempre, y peticiones con validación (422).
- Login: solo usuarios con rol `ADMIN`; los demás reciben 401.
- Prueba de capas y `pruebas/verificar_v8.ps1` contra la API real.

**Front (Flask):**
- `/registro/<token>`: pantalla con los 3 botones y un formulario por tipo.
- `geolocalizacion.js`: solo pide la ubicación según la modalidad.
- Al registrar: confirmación y, si la sesión tiene encuesta, «responder encuesta (opcional)».
- `auth_helpers.py` y `app.py`: solo `ADMIN`; se quitan los menús y las rutas de estudiante y ponente.
- `/admin/sedes`: campos de coordenadas.

**Se apaga / cambia:**
- Los blueprints `estudiante`, `ponente` y `ponente_catedras` dejan de registrarse en `app.py`.
- El flujo de clave por correo (`/registro/<token>/validar`) sale del registro.

### La V8 está terminada cuando…
- Un estudiante, un docente y un externo se registran desde el QR sin login.
- Un registro presencial fuera del radio de la sede es rechazado con un mensaje claro.
- Repetir el registro en la misma sesión dice «ya estás registrado».
- Un usuario `ESTUDIANTE` o `PONENTE` ya no puede iniciar sesión.
- Las pruebas de las versiones anteriores siguen pasando.

---

## 5. V9 · QR dinámico y control por modalidad

**Objetivo:** que el QR no sirva si se comparte por pantallazo: el código cambia
cada minuto y solo lo proyecta quien debe.
**Depende de:** la V8 fusionada en `main`.

### Historias de usuario

| HU | Qué hace |
|---|---|
| HU-09 | QR dinámico: el código cambia cada 60 s y se tolera el anterior |
| HU-11 | La ventana de registro al final de la sesión se mantiene (`QR_MINUTOS_ANTES_FIN` / `QR_MINUTOS_DESPUES_FIN`) |
| HU-12 | Pantalla de proyección a pantalla completa con un contador de registrados en vivo |
| — | Enlace secreto de proyección para el ponente (su envío al aprobar llega con la V10) |

**Decisiones que aplican:** D62, D63 y D55.

### Cambios por capa

**Base de datos:**
- **058**: columna `secreto` en `enlace_registro`; parámetros `QR_ROTACION_SEGUNDOS = 60`, `QR_CODIGOS_TOLERADOS = 1` y `QR_PASE_MINUTOS = 10`; `fn_codigo_qr` y `fn_codigo_qr_valido`.
- **059**: pase de registro de 10 min (`fn_emitir_pase`, `fn_validar_pase`); los procedimientos de registro de la V8 lo exigen.
- **060**: tabla `enlace_proyeccion` (token secreto por sesión, para el ponente).

**API (.NET):**
- `GET /api/QrSesion/{id}/codigo`: el código actual y los segundos que le quedan.
- `POST /api/QrSesion/{id}/enlace-proyeccion`: genera el enlace secreto.
- Los `POST /api/Registro/*` reciben el código y responden «código vencido» si no es válido.

**Front (Flask):**
- `/proyectar/<secreto>` (sin login) y `/admin/sesiones/<id>/qr`: el QR se redibuja solo cada 60 s, con cuenta regresiva y contador.
- Pantalla de «código vencido, escanea el que está en pantalla».

**Se apaga / cambia:** la pantalla de QR fija de la v7.1 (`ponente_sesion_qr.html`) se reemplaza por la dinámica.

### La V9 está terminada cuando…
- Un pantallazo del QR deja de servir unos 2 minutos después.
- El enlace secreto solo abre el QR de esa sesión; sin él, o sin login de admin, no se puede proyectar.
- El contador de la pantalla sube al registrarse alguien.

---

## 6. V10 · Calendario público y solicitudes de ponentes

**Objetivo:** que el ponente proponga su cátedra en una franja libre, sin cuenta,
y el administrador la apruebe desde una bandeja, quedando guardados sus datos
para ASIS.
**Depende de:** la V8 fusionada en `main` (en paralelo con la V9).

### Historias de usuario

| HU | Qué hace |
|---|---|
| HU-13 | El administrador programa franjas de 1 hora por fecha y sede, con una plantilla semanal |
| HU-14 | Calendario público mes a mes: rojo = ocupada, verde = libre, gris = en revisión |
| HU-15 | Solicitud del ponente: cátedra, resumen de la ponencia y datos ASIS (cédula, tipo de documento, perfil profesional, dirección, teléfono y correo institucional) |
| HU-16 | Bandeja de entrada en `/admin` con un contador de pendientes |
| HU-17 | Aprobar crea el ponente, la cátedra, la sesión y la ponencia en una transacción y envía el enlace secreto por correo. Rechazar libera la franja y avisa el motivo |
| HU-18 | Sesiones especiales del administrador (más de 1 hora o fuera de las franjas) |

**Decisiones que aplican:** D64, D62 y D58.

### Cambios por capa

**Base de datos:**
- **061**: tabla `franja_disponible` (sede, inicio, fin, estado, sin solapes por sede) y procedimiento para repetir la plantilla semanal.
- **062**: tablas `solicitud_catedra` (datos de la cátedra y del ponente, estado `PENDIENTE` / `APROBADA` / `RECHAZADA`, motivo) y `perfil_ponente` (D77).
- **063**: `sp_aprobar_solicitud` (reutiliza `sp_guardar_catedra` y `sp_guardar_asistente`; el admin digita el `id_evento_asis`, D78) y `sp_rechazar_solicitud`.

**API (.NET):**
- CRUD `/api/FranjaDisponible` y `GET /api/FranjaDisponible/calendario?mes=`.
- `POST /api/SolicitudCatedra` (pública), `GET` para la bandeja, `POST /{id}/aprobar` y `/{id}/rechazar`.

**Front (Flask):**
- `/calendario` público: vista mensual en JS; al pinchar una franja libre se abre el formulario. Reutiliza la consulta de disponibilidad de la migración 050.
- `/admin/franjas`: crear, bloquear o liberar y aplicar la plantilla semanal.
- `/admin/solicitudes`: bandeja con contador en el panel, detalle y botones aprobar o rechazar.
- Correos al ponente (`servicios/correo.py`): solicitud recibida, aprobada (con el enlace secreto) y rechazada.

### La V10 está terminada cuando…
- Un ponente sin cuenta pide una franja libre y esta pasa a «en revisión».
- Al aprobar aparecen la cátedra, la sesión y la ponencia, y el ponente recibe el correo con el enlace.
- Al rechazar, la franja vuelve a verse libre.
- No se pueden aprobar dos solicitudes para la misma franja y sede.

---

## 7. V11 · Archivos planos, importación y reportes

**Objetivo:** sacar del sistema lo que la oficina sube a ASIS, a SNIES y a los
certificados, sin volver a digitar, dentro de los 3 días hábiles.
**Depende de:** la V8 (registros) y la V10 (datos del ponente).

### Historias de usuario

| HU | Qué hace |
|---|---|
| HU-19 | Archivo plano ASIS por cátedra o sesión, con columnas configurables |
| HU-20 | Generación automática al cierre de la jornada (21:00) y botón manual |
| HU-21 | Archivo plano general con todos los asistentes (los 3 tipos y sus datos) |
| HU-22 | Importar asistencia desde CSV/Excel (posgrados que no se registran), con `origen = 'IMPORTADO'` |
| HU-23 | Exportar los ponentes por periodo para certificados y SNIES (los externos) |
| HU-24 | Ficha del evento y la reunión ASIS: cátedra, fecha y ponentes |
| HU-25 | Estadísticas de impacto social: asistentes por tipo, año y procedencia |

**Decisiones que aplican:** D65 y D68.

### Cambios por capa

**Base de datos:**
- **064**: parámetros `ASIS_COLUMNAS`, `ASIS_ENCABEZADO` y `HORA_CIERRE_JORNADA`; procedimientos de exportación (ASIS por sesión y por lote, archivo general y ficha ASIS). `fn_exportar_asis` no se toca.
- **065**: `sp_generar_archivos_jornada` (reutiliza `sp_generar_lote_migracion` de la V7), `sp_importar_asistencia` y `fn_dias_habiles`.
- **066**: reportes de ponentes por periodo y estadísticas de impacto.

**API (.NET):**
- `GET /api/Exportacion/asis/{sesion}`, `/asistencia/{sesion}` y `/ponentes?periodo=`.
- `POST /api/Exportacion/jornada` (la llama el cron o el botón).
- `POST /api/Importacion/asistencia` (CSV/Excel, con un informe de filas con error).
- `GET /api/Estadistica/impacto?anio=`.

**Front (Flask):**
- Descargas en el detalle de la sesión y la lista de archivos del día en `/admin`.
- `/admin/importar-asistencia` (reutiliza la carga de la V3).
- `/admin/reportes/ponentes` y `/admin/estadisticas`.

**docker-compose.yml:** un **4.º servicio** `cron` (imagen liviana) que llama a la API a las 21:00.

### La V11 está terminada cuando…
- A las 21:00 quedan generados los archivos ASIS de las sesiones terminadas del día.
- Cambiar `ASIS_COLUMNAS` cambia el archivo sin tocar código.
- Un CSV de posgrado importado aparece en los archivos y las estadísticas.
- El reporte de ponentes trae los datos para certificados y SNIES.

---

## 8. Pendientes con el cliente

Ninguno bloquea el arranque de la V8.

| # | Qué falta | Hace falta antes de | Mientras tanto |
|---|---|---|---|
| P2 | Un archivo plano real de ejemplo de ASIS (columnas, orden, encabezado; Excel o CSV) | V11 | D65: configurable, empieza con programa, ID, cédula |
| P9 | La cuenta de correo para los avisos al ponente y el enlace secreto | V10 | `CORREO_MODO_CONSOLA=1` para desarrollo |
| P1 | Confirmar si el ID puede traer letras | — (no bloquea) | D59: se aceptan letras |
| P3 | Confirmar que al docente no se le pide el programa | — (no bloquea) | D56: solo la facultad |
| — | Coordenadas de cada sede para la geocerca | V8 | Se cargan en `/admin/sedes` |

> **Spec kits escritos (2026-11-04):** [version8/](version8/2_spec.md),
> [version9/](version9/2_spec.md), [version10/](version10/2_spec.md) y
> [version11/](version11/2_spec.md), con 8 documentos cada uno. Migraciones
> definitivas: V8 054–057 · V9 058–060 · V10 061–063 · V11 064–066. Requisitos:
> RF198–RF263. Fases: 62–87. ADR nuevos: D71–D84. Ante cualquier diferencia
> entre este plan y un spec kit, **gana el spec kit**.
