# Decisiones del equipo para V8–V11 (ADR D55–D70)

> Tomadas el 2026-11-04, después de la reunión con el cliente, para poder
> arrancar el spec kit. Continúan la numeración de ADR de la v7.1 (D38–D54).
> Contexto: [historias_usuario_cliente.md](historias_usuario_cliente.md) y
> `docs/Cambios_proyecto_catedras_V8-V11.pdf` (secciones 4 y 5).
> Si el cliente responde distinto a una pregunta, se agrega un ADR nuevo que
> reemplace al anterior; estos no se editan.

## Registro de asistencia (V8)

| ADR | Tema | Decisión | Afecta |
|---|---|---|---|
| **D55** | Validación del estudiante (D-3) | El estudiante se registra **solo con su ID**. La seguridad la dan el **QR dinámico** (D62) y la **ubicación** en presenciales (D60–D61). **Se quita la clave por correo** del flujo de registro; `clave_acceso` queda sin uso para registros nuevos. | HU-04, HU-09 |
| **D56** | Docente sin programa (D-1) | Una migración pasa `tipo_vinculacion.DOCENTE` a `exige_programa = false` (como `ADMINISTRATIVO`). El registro del docente guarda la **facultad**. | HU-05 |
| **D57** | Cómo se guardan docentes y externos (D-2/D-4) | Se agrega el tipo de vinculación **`EXTERNO`** (`es_interno = false`, `exige_programa = false`) y una columna de **procedencia** (barrio o institución). Un procedimiento crea en **una sola transacción** el `asistente`, su `documento_asistente` (cédula vigente) y su `vinculacion_asistente`. Si la cédula ya existe, **reutiliza** la persona. | HU-05, HU-06 |
| **D58** | Lo de la v7.1 que ya no se usa (HU-02) | **Se apaga sin borrar**: se quitan las rutas y los menús de `/estudiante`, `/ponente` (con login), la inscripción y el primer ingreso; los usuarios `ESTUDIANTE`/`PONENTE` quedan inactivos. Las tablas, los procedimientos y las pruebas viejas se conservan. Los QR nuevos se emiten con `requiere_inscripcion = false`. | HU-02 |
| **D59** | Formato del ID (D-5) | Se valida **alfanumérico de 5 a 15 caracteres**, igual que `chk_asistente_idasis`. En el celular se abre el teclado numérico (`inputmode="numeric"`), pero no se rechazan las letras. | HU-04 |
| **D60** | Ubicación en híbridas (P7) | `PRESENCIAL`: obligatoria. `HIBRIDA`: **se pide y se guarda si la dan, pero no bloquea**. `VIRTUAL`/`TELEPRESENCIAL`: no se pide. | HU-10 |
| **D61** | Geocerca (P5) | En las presenciales se valida que la ubicación esté **dentro de un radio de la sede**. Cada `sede` lleva latitud y longitud (migración), y el radio es un parámetro (`GEOCERCA_RADIO_METROS`, 300 por defecto). Fuera del radio no se registra. | HU-10 |
| **D66** | Encuestas (P8) | Si la sesión tiene encuesta, al terminar el registro **se ofrece responderla** a los 3 tipos de usuario, **sin obligar**. Se reutiliza la V6. | V8 |
| **D67** | Tratamiento de datos (HU-08) | **Casilla obligatoria** para docente y externo, con un enlace a la política de datos de la USB. Se guarda en `consentimiento_datos`. | HU-08 |

## QR dinámico (V9)

| ADR | Tema | Decisión | Afecta |
|---|---|---|---|
| **D62** | Quién proyecta el QR (D-8) | Al aprobar la cátedra, el ponente recibe por correo un **enlace secreto** que solo abre la pantalla del QR de **sus** sesiones. El **admin** también puede abrirla desde `/admin`. No se crea un rol nuevo. | HU-09, HU-12, HU-17 |
| **D63** | Margen del QR | El código **vive 60 s** y se acepta también **el inmediatamente anterior**. Ambos valores son parámetros (`QR_ROTACION_SEGUNDOS = 60`, `QR_CODIGOS_TOLERADOS = 1`). El código se deriva del tiempo (estilo TOTP) a partir de un secreto del enlace; no se crea una fila por minuto. | HU-09 |

## Calendario y solicitudes (V10)

| ADR | Tema | Decisión | Afecta |
|---|---|---|---|
| **D64** | Franjas (P6) | El admin crea franjas de **1 hora por fecha y sede**. Por franja y sede cabe **una sola cátedra**, así que en sedes distintas sí pueden coincidir. Hay un atajo para **repetir una plantilla semanal**. | HU-13, HU-14 |

## Archivos y reportes (V11)

| ADR | Tema | Decisión | Afecta |
|---|---|---|---|
| **D65** | Columnas del archivo ASIS (D-6) | Las columnas y su orden son **configurables** con un parámetro (`ASIS_COLUMNAS`). Valor inicial: **programa, ID, cédula**, como pide la nota de la reunión. Si el archivo de ejemplo del cliente dice otra cosa, solo se cambia el parámetro. | HU-19, HU-22 |
| **D68** | Generación automática (D-7) | Un **4.º servicio en docker-compose** (un contenedor liviano con cron) llama a la API a la hora de `HORA_CIERRE_JORNADA` (21:00 por defecto). La lógica queda en un procedimiento. También hay un botón manual en `/admin`. | HU-20 |

## Organización

| ADR | Tema | Decisión |
|---|---|---|
| **D69** | Numeración | Se sigue el mapa actual: **V8, V9, V10 y V11**, en `docs/spec_kit/Versiones/version8/` a `version11/`, y migraciones desde la **054**. |
| **D70** | Reparto del equipo | **V8 entre todos**; después la **V9 y la V10 en paralelo**, cada una en su rama y con su PR a `main`; la **V11** al final (depende de la V8 y la V10). |

## ADR técnicos de los spec kits (D71–D84)

Salieron al escribir los spec kits y cruzarlos con la BD real. Son propuestas
del spec; se confirman en la fase de preparación de cada versión.

| ADR | Versión | Decisión | Dónde |
|---|---|---|---|
| D71 | V8 | Una **clave técnica** ya usada en `clave_acceso` por cada registro, porque `chk_registro_clave` exige `fk_clave` en los registros por QR | [version8/4_research.md](version8/4_research.md) |
| D72 | V8 | `asistente.fk_facultad`, `asistente.procedencia` y `registro_asistencia.fk_facultad_snapshot` | idem |
| D73 | V8 | Nombres y apellidos en dos campos (la tabla los exige por separado) | idem |
| D74 | V9 | Código = HMAC(secreto, minuto) en base32 de 8 caracteres (o hex de 10) | [version9/4_research.md](version9/4_research.md) |
| D75 | V9 | Pase de 10 min para llenar el formulario después de escanear | idem |
| D76 | V9 | El enlace de proyección tiene su propio token, guardado solo como hash | idem |
| D77 | V10 | Datos del ponente en la tabla nueva `perfil_ponente` | [version10/4_research.md](version10/4_research.md) |
| D78 | V10 | El admin digita el `id_evento_asis` (9 dígitos) al aprobar | idem |
| D79 | V10 | La rama que llegue segunda (V9 o V10) conecta el enlace de proyección en el correo de aprobación | idem |
| D80 | V10 | El periodo de la sesión se deduce de la fecha de la franja | idem |
| D81 | V11 | La BD arma `{columnas, filas}` y el front escribe el CSV o el XLSX | [version11/4_research.md](version11/4_research.md) |
| D82 | V11 | Hora de la jornada: crontab fijo o parámetro que manda (se decide en la Fase 81) | idem |
| D83 | V11 | El formato de importación es el mismo de la exportación | idem |
| D84 | V11 | Días hábiles sin festivos (mejora futura: tabla de festivos) | idem |

## Sigue pendiente con el cliente (no bloquea arrancar la V8)

- **P2:** un archivo plano real de ejemplo para ASIS, para confirmar la decisión D65 (hace falta antes de la V11).
- **P9:** qué cuenta de correo se usa para los avisos al ponente y el enlace secreto (hace falta antes de la V10).
- **P1 y P3:** confirmar que el ID puede traer letras y que al docente no se le pide el programa. Lo decidido (D59 y D56) funciona con cualquiera de las dos respuestas.
