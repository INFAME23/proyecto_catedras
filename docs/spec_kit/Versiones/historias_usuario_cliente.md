# Historias de usuario del cliente · Reunión 2026-11-04

> **Fuente:** reunión con el cliente (Cátedras USB) del 2026-11-04, dos audios
> transcritos automáticamente y las notas del compañero. Transcripción y notas:
> [docs/reuniones/2026-11-04_reunion_cliente.md](../../reuniones/2026-11-04_reunion_cliente.md).
> Cada historia cita el minuto del audio de donde sale (`R mm:ss` = reunión,
> `W mm:ss` = audio de WhatsApp, `N` = notas del compañero).
>
> **Cómo se usa en SDD:** cada versión de abajo es un futuro spec kit
> (`2_spec.md` … `8_tasks.md`) en `docs/spec_kit/Versiones/versionN/`. Las
> historias son la entrada del `2_spec.md`; los criterios de aceptación se
> vuelven requisitos `RF-xx` y pruebas. Se numeran **V8 a V11** para seguir el
> [mapa de versiones](0_mapa_versiones.md); la regla de siempre se mantiene:
> **la BD manda**, nada se edita en `script_bdcatedras.sql` y todo cambio va en
> una migración nueva (054 en adelante).

---

## 0. Lo que cambia frente a lo construido (v7.1)

El cliente simplificó el modelo de acceso. Varias cosas de la v7.1 **se
retiran o se vuelven opcionales**. Hay que decidirlo antes de la V8:

| Hoy (v7.1) | Lo que pide el cliente | Origen |
|---|---|---|
| Cuentas y roles `ESTUDIANTE` y `PONENTE` con login | **Solo el administrador tiene login.** Nadie más inicia sesión | R 04:21–04:54 |
| `/ponente` y `/estudiante` protegidos | El catálogo de cátedras y el calendario son **públicos** | R 04:03–04:18, R 08:07–08:22 |
| Inscripción previa obligatoria a la cátedra (QR con `requiere_inscripcion`) | **No hay inscripción** ni reserva de cupo | R 10:15–10:45 |
| Cupo por sesión (RN-38) | No se usa para bloquear. Si la cátedra es muy concurrida, se transmite a otras salas | R 10:28–11:32 |
| El estudiante se identifica con su correo institucional o su código | Se identifica **solo con su ID** (numérico, obligatorio) | R 12:18–12:26, W 00:30 |
| Geolocalización obligatoria siempre | Obligatoria **solo en presenciales**; en virtuales no importa | R 17:02–17:24 |
| QR fijo por sesión | **QR dinámico** que cambia cada minuto | R 16:30–16:52, N |
| Primer ingreso del estudiante con clave = código | Ya no aplica (el estudiante no tiene cuenta) | — |

---

## 1. Actores

| Actor | ¿Inicia sesión? | Qué hace |
|---|---|---|
| **Administrador** (Cátedras USB) | Sí, el único | Programa las franjas disponibles, aprueba o rechaza las solicitudes de ponentes, crea sesiones especiales, saca los archivos planos y las estadísticas |
| **Ponente** | No | Mira el calendario y solicita dictar una cátedra con sus datos |
| **Estudiante USB** | No | Registra su asistencia con su ID |
| **Docente / administrativo USB** | No | Registra su asistencia con un formulario corto |
| **Externo** (comunidad, otra institución) | No | Registra su asistencia con un formulario |
| **Público** | No | Consulta el catálogo de cátedras |

---

## V8 · Registro de asistencia sin login, por tipo de usuario

**Objetivo:** que cualquier persona registre su asistencia escaneando el QR,
sin cuenta, diciendo qué tipo de usuario es. Al terminar la V8 el sistema ya
sirve en una cátedra real.

**HU-01 · Catálogo público de cátedras** (R 04:03–04:18)
Como **persona interesada**, quiero ver todas las cátedras de la universidad
sin iniciar sesión, para saber cuáles hay y cuándo son.
- Dado que no tengo sesión, cuando entro a `/catedras`, entonces veo el
  listado con fecha, hora, modalidad y ponente, sin que me pida login.
- No se muestran datos personales de ponentes ni asistentes.

**HU-02 · Solo el administrador inicia sesión** (R 04:21–04:54)
Como **administrador**, quiero ser el único rol con login, para que el
aplicativo sea solo de registro y no haya que administrar cuentas de
estudiantes ni de ponentes.
- El login sigue existiendo solo para `ADMIN`.
- Las rutas de estudiante y ponente con login se retiran o quedan
  deshabilitadas (decisión ADR: ¿se borran o se apagan?).
- Los usuarios `ESTUDIANTE`/`PONENTE` ya creados no pueden entrar.

**HU-03 · Elegir el tipo de usuario al registrarse** (R 11:46–12:07, R 01:35–01:52, N)
Como **asistente**, quiero que al abrir el QR me pregunte si soy *Estudiante
USB*, *Docente/Administrativo* o *Externo*, para llenar solo lo que me toca.
- Al abrir el enlace del QR aparecen tres botones.
- Cada botón lleva a su propio formulario (HU-04, HU-05 y HU-06).
- No hay inscripción previa ni cupo que bloquee el registro (R 10:15–10:45).

**HU-04 · Registro del estudiante con su ID** (R 12:07–12:26, W 00:30–01:08, N)
Como **estudiante USB**, quiero registrarme escribiendo solo mi **ID**, para
que el sistema traiga el resto de mis datos (programa, cédula, nombre).
- El ID es **obligatorio**. No se usa la cédula porque los documentos
  extranjeros pueden llevar letras (pasaportes).
- Si el ID existe en el maestro de asistentes, se registra la asistencia y se
  toma una "foto" del programa vigente (la regla RN-22 ya existe).
- Si el ID no existe, el mensaje lo dice claramente y no se crea nada.
- ⚠️ **Por validar:** el cliente dice que el ID "sí o sí es numérico", pero en
  la BD `id_asis` es alfanumérico y hay valores reales como `1000513C`
  (`chk_asistente_idasis`). Ver pregunta P1.

**HU-05 · Registro del docente o administrativo** (R 12:29–12:52, N)
Como **docente o administrativo**, quiero llenar un formulario corto, porque
casi nadie se sabe su ID.
- Campos: **cédula, nombre, un dato de contacto (correo) y facultad**.
- La facultad se escoge de la lista existente (`facultad`).
- Con esos datos la oficina lo sube después al sistema institucional (sirve
  para el escalafón docente, R 02:31–02:43).
- La cédula admite letras (documentos extranjeros).

**HU-06 · Registro del externo** (R 12:56–13:24, N)
Como **persona externa**, quiero registrar mi asistencia con mis datos
básicos, para que la universidad mida su impacto social.
- Campos: **cédula, nombre, correo de contacto y de dónde viene** (barrio,
  zona o institución, en texto libre).
- No se pide dirección.
- Se marca como externo (`es_externo = true`, sin `id_asis`; la BD ya lo
  permite).

**HU-07 · Una sola asistencia por persona y sesión**
Como **administrador**, quiero que nadie quede registrado dos veces en la
misma sesión, para que los conteos y los archivos salgan limpios.
- Un segundo intento muestra "ya estás registrado" y no crea otro registro.

**HU-08 · Tratamiento de datos** (ya existe `consentimiento_datos`)
Como **docente o externo**, quiero aceptar el tratamiento de mis datos antes
de enviar el formulario, para cumplir la ley de habeas data.
- No se puede enviar el formulario sin marcar la casilla; se guarda el
  consentimiento.
- *(No se habló en la reunión: es una propuesta del equipo, porque se piden
  datos personales. Confirmar con el cliente.)*

---

## V9 · QR dinámico y control por modalidad

**Objetivo:** que la asistencia sea difícil de falsificar. El QR cambia cada
minuto y la ubicación se exige solo donde tiene sentido.

**HU-09 · QR dinámico que cambia cada minuto** (R 16:30–16:52, N)
Como **administrador**, quiero que el QR de la sesión cambie cada minuto,
para que un pantallazo enviado a otra persona deje de servir.
- La pantalla del QR se renueva sola cada 60 s (configurable con un
  parámetro, p. ej. `QR_ROTACION_SEGUNDOS`).
- Un QR vencido muestra "este código ya expiró, escanea el que está en
  pantalla".
- Se da un pequeño margen (p. ej. el minuto anterior) para quien escaneó justo
  en el cambio.
- Diseño sugerido: el token cambia por tiempo y se firma (estilo TOTP) en vez
  de crear una fila de `enlace_registro` por minuto (R 16:48: "sería muy
  complicado crear tantos").

**HU-10 · Ubicación obligatoria solo en presenciales** (R 17:02–17:24)
> **Se construye en la V8**, no en la V9: cambia los procedimientos de
> registro de la V8 (ver [plan_v8_v11.md](plan_v8_v11.md)). Decisiones D60 y D61.

Como **administrador**, quiero exigir la ubicación solo en las sesiones
presenciales, porque en las virtuales no importa desde dónde se conectan.
- `PRESENCIAL`: sin ubicación no hay registro (como hoy).
- `HIBRIDA`: se pide y se guarda si la dan, pero no bloquea (D60).
- `VIRTUAL`/`TELEPRESENCIAL`: no se pide la ubicación.
- Geocerca (D61): en presenciales, fuera del radio de la sede
  (`GEOCERCA_RADIO_METROS`, 300 m) no se registra.

**HU-11 · Ventana de registro al final de la sesión** (R 15:58–16:03, R 18:12–18:50)
Como **administrador**, quiero que el registro solo se abra al final de la
cátedra, para que no cuente a quien se conecta 10 minutos antes de terminar y
se va.
- El QR se proyecta al final; la ventana se mantiene (parámetros
  `QR_MINUTOS_ANTES_FIN`/`QR_MINUTOS_DESPUES_FIN`, que ya existen).
- En virtuales, el control de ingreso tardío lo hace la sala de Zoom; el
  sistema solo respeta la ventana.

**HU-12 · Pantalla de proyección del QR**
Como **administrador** (o quien proyecta en la sala), quiero una pantalla de
QR a pantalla completa con el nombre de la cátedra y un contador, para
proyectarla en el auditorio o compartirla en Zoom.
- Se abre desde el detalle de la sesión con la sesión de admin iniciada.
- Muestra cuántos se han registrado en vivo (opcional).

---

## V10 · Calendario público y solicitudes de ponentes

**Objetivo:** que los ponentes propongan sus cátedras en las franjas libres
sin tener cuenta, y que el administrador las apruebe desde una bandeja.

**HU-13 · El administrador programa las franjas disponibles** (R 07:07–07:17)
Como **administrador**, quiero definir qué fechas y horas están disponibles
para cátedras, para que los ponentes solo puedan pedir en esas franjas.
- Se crean franjas por fecha y hora, con sede y modalidad.
- La duración por defecto es **1 hora** (R 06:50–06:56).
- Se puede bloquear o liberar una franja.

**HU-14 · Calendario público de disponibilidad** (R 05:01–05:20, R 07:45–08:33, N)
Como **ponente**, quiero ver un calendario mes a mes con las franjas libres y
ocupadas, para escoger cuándo dictar mi cátedra.
- Vista por mes; las franjas ocupadas se ven **en rojo** y las libres en
  verde o blanco.
- Al pinchar una franja libre se abre el formulario de solicitud (popup)
  (R 06:29–06:32).
- Cualquiera puede verlo sin login (R 08:12–08:22).
- Ya existe la consulta de conflictos (`/api/Disponibilidad`, migración 050)
  y se puede reutilizar.

**HU-15 · Solicitar dictar una cátedra con los datos del ponente** (R 03:19–03:46, R 05:18–05:22, N)
Como **ponente**, quiero llenar un formulario con la cátedra que quiero dictar
y mis datos, para que el administrador la apruebe sin pedirme nada más
después.
- Datos de la cátedra: nombre o tema, la franja elegida, la modalidad y el
  **resumen de la ponencia**.
- Datos del ponente (los que pide ASIS): **cédula, tipo de documento, perfil
  profesional, dirección, teléfono y correo institucional**.
- Antes de guardar se comprueba que la franja siga libre (R 05:46–06:02).
- La solicitud queda **pendiente**; la franja se ve como "en revisión".
- El ponente recibe la confirmación de que su solicitud llegó.

**HU-16 · Bandeja de entrada de solicitudes para el administrador** (R 08:40–09:13, N)
Como **administrador**, quiero una bandeja dentro del aplicativo con las
solicitudes nuevas de ponentes, para revisarlas sin depender del correo, que
"se pierde entre miles".
- En `/admin` aparece un aviso con cuántas solicitudes hay pendientes.
- La bandeja lista cada solicitud con el ponente, la cátedra, la fecha y su
  estado (pendiente, aprobada, rechazada).
- Desde el detalle se puede **aprobar** o **rechazar** (con motivo).

**HU-17 · Aprobar o rechazar una solicitud** (R 05:24–05:32, R 08:23–08:49, R 14:51–15:11)
Como **administrador**, quiero que al aprobar una solicitud se cree la
cátedra, su sesión y el ponente, para no volver a digitar nada.
- Al aprobar, en una sola transacción, se crean o actualizan `asistente` (el
  ponente), `catedra`, `sesion` y `ponencia`, y la franja queda ocupada.
- Al rechazar, la franja vuelve a quedar libre.
- Al ponente le llega un correo: "su cátedra fue aprobada" o "fue rechazada"
  con el motivo (R 05:28–05:32).
- Los datos del ponente quedan guardados para ASIS, los certificados y SNIES
  (V11).

**HU-18 · Sesiones especiales creadas por el administrador** (R 06:56–07:02, R 15:52–15:54)
Como **administrador**, quiero crear cátedras o sesiones especiales que
duren más de una hora o que estén fuera de las franjas, para los casos que no
siguen la regla.
- Solo el administrador puede saltarse la duración de 1 hora y las franjas.

---

## V11 · Archivos planos, importación y reportes

**Objetivo:** sacar del sistema lo que la oficina sube a ASIS, a SNIES y a los
certificados, sin digitar dos veces.

**HU-19 · Archivo plano de estudiantes para ASIS** (W 01:12–02:15, R 14:21–14:45, N)
Como **administrador**, quiero un archivo plano por cada cátedra con los
estudiantes que asistieron, en el formato de ASIS, para subirlo dentro de los
**3 días hábiles** que tengo.
- Columnas: **programa, ID y cédula** (N). El código de programa es texto: una
  letra seguida de números (W 00:32).
- Un archivo por cátedra o sesión ("un Excel por cada tabla", W 02:04).
- Se puede descargar a mano desde el detalle de la sesión.
- Reutiliza `fn_exportar_asis` y el lote de migración de la V7, adaptados al
  nuevo formato.

**HU-20 · Generación automática al final de la jornada** (W 01:36–02:15)
Como **administrador**, quiero que los archivos de las cátedras del día se
generen solos al terminar la jornada, para tenerlos listos sin acordarme.
- La jornada va de **6:00 a 21:00**; después de la última cátedra del día
  (por defecto a las 21:00, configurable) se generan los archivos de las
  sesiones terminadas.
- Quedan en `/admin` para descargar, con fecha y estado.

**HU-21 · Archivo plano general de asistencia** (N: "que arroje otro archivo plano con la asistencia")
Como **administrador**, quiero otro archivo con **todos** los asistentes de
una sesión (estudiantes, docentes y externos) y sus datos, para mis controles
y para subir a los docentes al sistema institucional.
- Incluye el tipo de usuario y los campos de cada formulario (HU-04, HU-05 y
  HU-06).
- Se descarga en CSV o Excel.

**HU-22 · Importar asistencia desde CSV o Excel** (W 02:17–02:57)
Como **administrador**, quiero cargar un CSV o Excel con la asistencia de
cátedras cuyos asistentes no se registran en el sistema (p. ej. posgrados
nocturnos), para que también salgan en los archivos y las estadísticas.
- El formato de carga es el mismo del archivo de salida (HU-19).
- Los registros entran con `origen = 'IMPORTADO'` (ya existe en la BD).
- Las filas con error se reportan sin tumbar la carga (como la carga de
  asistentes de la V3).

**HU-23 · Datos de ponentes para certificados y SNIES** (R 13:37–13:49, R 15:13–15:34)
Como **administrador**, quiero exportar los ponentes de un periodo con sus
datos y su ponencia, para enviarles el certificado y reportar a SNIES (MEN) a
los ponentes externos.
- El archivo trae los datos del ponente (HU-15), la cátedra, la fecha y si es
  externo.
- Se filtra por periodo o por año.

**HU-24 · Datos para crear el evento y la reunión en ASIS** (R 14:21–14:45)
Como **administrador**, quiero ver juntos los datos del evento y de la
reunión (cátedra, fecha y nombre de los ponentes), para crearlos en ASIS antes
de subir el archivo de estudiantes.
- Una ficha o archivo por sesión con lo que ASIS pide para el evento y la
  reunión.

**HU-25 · Estadísticas de impacto social** (R 13:31–14:09)
Como **administrador**, quiero ver cuántas personas externas asistieron por
año (y por cátedra), para medir el impacto social de la Cátedra Abierta.
- Conteo de asistentes por tipo (estudiante, docente o externo) y por año o
  periodo; también por procedencia (barrio o institución) de los externos.
- Se puede exportar.

---

## Resumen por versión

| Versión | Historias | Depende de | Al terminar se puede… |
|---|---|---|---|
| **V8** Registro sin login | HU-01 … HU-08 y HU-10 | v7.1 (registro, enlace, geo) | Usar el sistema en una cátedra real: cada persona se registra según su tipo, con ubicación por modalidad |
| **V9** QR dinámico | HU-09, HU-11 y HU-12 | V8 | Proyectar un QR que no se puede compartir |
| **V10** Calendario y solicitudes | HU-13 … HU-18 | V8 (catálogo público) | Que los ponentes propongan cátedras y el admin las apruebe desde la bandeja |
| **V11** Archivos y reportes | HU-19 … HU-25 | V8 (registros), V10 (datos del ponente) | Subir a ASIS y SNIES y emitir certificados sin digitar dos veces |

V9 y V10 son independientes entre sí: se pueden hacer en paralelo (una por
persona del equipo) una vez esté la V8.

---

## Preguntas abiertas para el cliente (resolver antes del spec de cada versión)

| # | Pregunta | Afecta |
|---|---|---|
| P1 | El ID del estudiante, ¿es siempre numérico? El maestro de ASIS que cargamos trae IDs con letra (p. ej. `1000513C`). | HU-04, HU-19 |
| P2 | Formato exacto del archivo plano para ASIS: ¿Excel o CSV?, ¿qué columnas y en qué orden?, ¿con encabezado? ¿Nos comparten un archivo de ejemplo? | HU-19, HU-22 |
| P3 | ¿El docente/administrativo debe dar también el programa, o solo la facultad? | HU-05 |
| P4 | ¿Quién proyecta el QR en cada sesión si el ponente ya no tiene cuenta: el admin, un monitor, o un enlace secreto que se le entrega al ponente? | HU-09, HU-12 |
| P5 | ¿Se valida que la ubicación esté cerca de la sede (radio en metros) o basta con tenerla? | HU-10 |
| P6 | ¿Cómo se definen las franjas? ¿Por semana o semestre, con qué horas y en qué salas? ¿Puede haber dos cátedras a la misma hora en salas distintas? | HU-13, HU-14 |
| P7 | ¿Qué se hace con los usuarios `ESTUDIANTE`/`PONENTE` y las inscripciones ya creadas en la v7.1: se borran o se dejan inactivos? | HU-02 |
| P8 | ¿Las encuestas de satisfacción siguen? ¿Se responden después del registro, para todos los tipos? (se mencionó de pasada, R 06:12) | V8 |
| P9 | ¿Qué correo o proveedor se usará para avisar al ponente de la aprobación? | HU-17 |
