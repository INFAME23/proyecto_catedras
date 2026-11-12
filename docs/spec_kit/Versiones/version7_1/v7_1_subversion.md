# Subversión v7.1 — geolocalización, panel de estudiante, cuentas, correo y admin lateral

> **No es una versión nueva del mapa** (no rompe el orden V1–V7 ni agrega
> recursos a la API): es una subversión de mantenimiento sobre V5–V7. Sus
> requisitos se numeran `RF-S1`…`RF-S7` hasta que el equipo les asigne
> número real. Este documento **no reemplaza** los `2_spec.md`…`8_tasks.md` de
> una versión numerada; si el equipo decide convertirla en una V8 formal,
> hay que pasarla primero a ese formato.
>
> Nace del prompt «Subversión Cátedras: geolocalización, panel estudiante,
> cuentas y admin» y de revisar el código real. Donde el prompt y el repo no
> coincidían, mandó el repo (sección 1).

## 1. Lo que se verificó antes de construir (y lo que no coincidía)

| El prompt decía | Lo que se encontró |
|---|---|
| El placeholder de `estudiante.py` ya no es cierto | Confirmado: `asistente`, `usuario`, `rol_por_usuario`, `registro_asistencia`, `programa_asistente` y `vinculacion_asistente` están en la API. Comentario corregido y panel construido. |
| El autorregistro es público, sin cuenta, 2 pasos | Confirmado. Un detalle: `ip` y `user_agent` **no** son campos del formulario; los toma el servidor Flask de la petición. La ubicación sí viene del navegador (campos ocultos). |
| Solo existe el rol `ADMIN` en el front | Confirmado en el front. **En la BD** el catálogo `rol` solo traía `ADMIN`, `COORDINADOR` y `CONSULTA`: `ESTUDIANTE` y `PONENTE` **no existían** (migración `046`). |
| `ponente.py`: «revisa si ya hay un blueprint o es placeholder» | **No es placeholder**: el calendario del ponente (v4) funciona, pero era **público** (cualquiera lo abría sin sesión). Ahora exige rol `PONENTE` o `ADMIN`. |
| `registro_asistencia` no tiene columnas de geolocalización | Confirmado (`045`). |
| Estudiante semilla `30000124724` «cargado a mano» | ⚠️ **Ese `id_asis` ya existía** en los datos del script provisto: una persona de relleno («Asistente ASIS 30000124724», correo institucional `asis.30000124724@usbmed.edu.co`, programa M0501, documento CC 1011390488). Ver D42. |
| El programa «Ingeniería en Datos y Software» hay que buscarlo | Existe: **`M0286`** («Ingenieria de Datos y Software»). |

## 2. Requisitos y dónde quedó cada uno

| # | Requisito | Capas tocadas |
|---|---|---|
| RF-S1 | Geolocalización obligatoria al autorregistrar | BD `045` · API (`ClaveAccesoValidar`, `RegistroAsistencia`, repo/servicio de `ClaveAcceso`) · front (`geolocalizacion.js`, `registro_validar.html`, `routes/registro.py`) · admin (macro `macros_geo.html`) |
| RF-S2 | Panel de estudiante funcional | front (`routes/estudiante.py`, `estudiante_panel.html`) |
| RF-S3 | Cuentas reales de Estudiante y Ponente (**sin efecto desde D55: solo el administrador inicia sesión**) | BD `046` |
| RF-S4 | Estudiante semilla | BD `048` |
| RF-S5 | Cambio de contraseña para cualquier usuario autenticado | API (`POST /api/Usuario/{id}/cambiar-clave`, recuperación) · front (`routes/perfil.py`) · BD `047` |
| RF-S6 | Correo real para el código | front (`servicios/correo.py`, `config.py`, `env.ejemplo`, compose) |
| RF-S7 | `/admin` con barra lateral colapsable (reemplazada por categorías desplegables, D51) | front (`admin/base_admin.html`, `admin/panel.html`, `estilo.css`) |

Migraciones nuevas (en orden; se montan en el compose como `z045`…`z058`):
`045_geolocalizacion_registro` · `046_roles_estudiante_ponente` ·
`047_codigo_recuperacion` · `048_semilla_estudiante` · `049_inscripcion_catedra` · `050_disponibilidad_sesiones` · `051_qr_sesion` · `052_primer_ingreso_estudiante` · `053_qr_canal_por_modalidad` · `054_sin_inscripcion_ni_cupo_login_admin` · `055_solicitud_ponente` · `056_restriccion_programacion` · `057_solicitud_ponente_catedra_nueva` · `058_solicitud_ponente_modalidad_sede_periodo` · `059_separacion_entre_sesiones_enlace`.

## 3. Decisiones (ADR de la v7.1, continúan después de D37)

**D38 — La ubicación es obligatoria, sin excepción (RF-S1).** Si la persona
niega el permiso, el navegador no lo soporta, o falla, el botón de enviar se
queda **bloqueado**, se explica por qué y cómo permitirlo y hay un botón
«Intentar de nuevo». **No hay** «continuar sin ubicación». La regla se exige
cuatro veces, de afuera hacia adentro, porque ninguna capa debe confiar en la
anterior: el JS bloquea el botón → Flask rechaza el POST sin coordenadas
válidas **antes** de llamar a la API (no gasta un intento de la clave) → la API
responde 422 si faltan o están fuera de rango → la BD lanza excepción si
llegan nulas. Las columnas siguen siendo **nulables**: los registros `MANUAL` e
`IMPORTADO` (los crea el administrador, sin navegador) y los anteriores a la
v7.1 no tienen ubicación; un `CHECK` exige que latitud y longitud vayan juntas
o ninguna. Nota práctica: los navegadores solo entregan la ubicación en
`https://` o `localhost`; abrir el front por la IP de la red (`http://192.168…`)
la deniega siempre (el script lo dice).

**D39 — Rutinas nuevas, no se edita el script provisto (RF-S1).** El script
`script_bdcatedras.sql` se monta, no se modifica. En vez de cambiar
`sp_validar_clave_y_registrar`/`fn_validar_clave_api`, la `045` agrega
`sp_validar_clave_y_registrar_geo`/`fn_validar_clave_geo_api` (mismo cuerpo +
ubicación) y la API llama a las nuevas. Las originales siguen en la BD sin
usarse. Se duplica la lógica (~40 líneas) a propósito: la alternativa
(llamar a la original y hacer un `UPDATE` después) dejaría el `INSERT` de la
bitácora sin coordenadas. `listar/consultar/crear` de la `037` se reemplazan
(misma firma) para que el JSON traiga `latitud` y `longitud`.

**D40 — Cambiar la clave y recuperarla son dos flujos (RF-S5).**
*Cambiar* (`/perfil/clave`, con sesión): clave actual + nueva + confirmación;
la API verifica la actual con `BCrypt.Verify`. **No** exige código por correo:
quien conoce su clave ya demostró quién es. *Recuperar* (`/recuperar`, público):
usuario o correo → código de 6 dígitos **enviado por correo** → código + clave
nueva. Cuenta inexistente e inactiva responden igual que una existente (D27);
al canjear, **cualquier** fallo muestra el mismo mensaje genérico.

**D41 — Tabla `codigo_recuperacion` y estado en vez de excepción (RF-S5/S6).**
`clave_acceso` no sirve para recuperar: su `fk_enlace` es `NOT NULL` y su SP
inserta un `registro_asistencia`. Se copia el **patrón** (hash con sal,
vigencia, intentos, `usado_en`, los parámetros `CLAVE_*`), no la tabla. Es una
tabla de apoyo que la API no expone como CRUD (el mapa sigue en 37 recursos).
Además, `sp_restablecer_clave` **devuelve un estado** (`OK`, `NO_EXISTE`,
`USADO`, `EXPIRADO`, `INCORRECTO`, `AGOTADO`) en vez de `RAISE EXCEPTION`:
una excepción deshace el `intentos + 1` del mismo `CALL`, así que el tope de
intentos nunca se cumple (es el límite RF153 de la v5). Con un código de 6
dígitos eso permitiría adivinarlo; en la recuperación de clave no se acepta.
(Se detectó al verificar: tres intentos fallidos no anulaban el código.) La API
responde **400**, no el 500 de D25, porque aquí no hay excepción de la BD.
El código sale de `gen_random_bytes`, no de `random()`.

**D42 — El estudiante semilla ajusta a la persona que ya existía (RF-S4).**
`id_asis` es único, así que no se podía «crear» otro. La `048` **actualiza** la
fila 12808 con los datos pedidos (nombre, apellidos, `correo_personal` =
gmail, `correo_institucional` = `NULL`, programa `M0286` en `2026-2`) y le crea
la cuenta. Importa lo del correo: el destino de las claves es el
institucional si existe (RN-19); con el de relleno los códigos habrían ido a un
buzón inexistente. Los valores originales están en el comentario de la
migración. Si el `id_asis` no existe, la migración crea la persona completa con
`sp_guardar_asistente`.

**D43 — `/ponente` y `/estudiante` ahora exigen rol (RF-S3).** Mismo patrón de
`admin.py` (`before_request` + `exigir_rol`, que ahora acepta varios roles).
`/estudiante` solo `ESTUDIANTE`; `/ponente` `PONENTE` o `ADMIN` (el admin lo
enlazaba desde su panel). El login sigue siendo uno solo; el destino depende
del rol (ADMIN › PONENTE › ESTUDIANTE). El navbar muestra cada panel solo a
quien lo puede abrir. La API sigue abierta (D28): es protección del front.

**D44 — Correo por SMTP configurado por entorno (RF-S6).** Sin credenciales en
el código; `SMTP_*` en `.env`/entorno (ver `front_catedras/env.ejemplo`). Sin
`SMTP_HOST` el correo está apagado y la recuperación **avisa** que no pudo
enviarlo (nunca falla en silencio ni muestra el código en pantalla).
`CORREO_MODO_CONSOLA=1` escribe el correo en el log del front, solo para
desarrollo. **El proveedor es decisión del equipo** (ver §5).

**D45 — Barra lateral sobre `base.html`, sin tocar rutas (RF-S7).**
`base.html` gana un bloque `principal`; `admin/base_admin.html` lo reemplaza
con la barra y las 70 plantillas de `admin/` extienden ese layout (el cambio
fue solo la línea `extends`). El menú sale de `_menu_datos.html`, generado
desde `panel.html` (misma taxonomía: 7 grupos, 37 secciones + «Carga de datos»
y «Calendario del ponente»). Bootstrap/CSS propio, sin librería nueva.
Escritorio: el estado se recuerda en `localStorage`; móvil: nace cerrada, no
se recuerda.

**D46 — Asignar roles y cargar datos son de ADMIN; el estudiante se registra desde la sesión.**
Corrección posterior a la entrega. (1) `POST /sesiones/<id>/ponentes` y `/ponentes/quitar`
(asignar o quitar a cualquier persona con cualquier rol: PONENTE, MODERADOR, INVITADO,
ORGANIZADOR) y `/carga_datos` (cargar personas masivamente) **no pedían sesión**: venían de la v3/v4,
cuando no había login, y la v7.1 abrió cuentas de estudiante sin cerrarlas. Ahora exigen `ADMIN`
(`rol_requerido`); el detalle público de la sesión solo muestra el formulario de asignar/quitar a un
ADMIN, y la búsqueda de personas (`?q=`) tampoco lista a nadie para los demás. (2) El detalle de la
sesión daba solo información. Si quien mira es un `ESTUDIANTE` y la sesión tiene un enlace de registro
vigente (no revocado y dentro de su ventana), aparece **«Registrar mi asistencia»**, que lleva a
`/registro/<token>` con su código ASIS ya escrito; sin enlace vigente se le avisa. Sigue valiendo todo
lo del registro: clave por correo y ubicación obligatoria. Compromiso asumido: el estudiante con
sesión ve el token del enlace (antes solo quien tenía el QR); la presencia en el lugar la respalda la
ubicación, que se guarda pero no se contrasta contra una sede. La API sigue abierta (D28): estas
protecciones son del front.

**D47 — Un ponente solo ve su propio calendario.** `/ponente` dejaba a cualquier PONENTE buscar a
cualquier persona y abrir su agenda con `?asistente=<id>`. Ahora el buscador y `?asistente=` de otra
persona son solo de ADMIN: a un PONENTE se le oculta el buscador, `?q=` se ignora y pedir la agenda de
otro responde 403 (la suya, con su propio id, sí abre). Las cuentas de ponente las crea el
administrador y les entrega las credenciales (`/admin/usuarios` + `/admin/roles-usuario`); no hay
autorregistro.

**D48 — El administrador da de alta a un ponente en un solo formulario.** `/admin/ponentes/nuevo`
(también en el menú lateral, grupo «Sesiones y ponentes», y como botón en `/admin/usuarios`): se
escriben nombres, apellidos, al menos un correo, usuario y (opcional) clave, y el front crea en orden la
persona (`Asistente`), la cuenta (`Usuario`) y el rol `PONENTE`, sin buscar antes a la persona. La API no
tiene una operación «todo o nada» para esto (D9), así que si un paso falla se **borran los anteriores**
desde el front (cuenta, luego persona) para no dejar datos a medias. Reglas heredadas de la API: una
persona externa (casilla marcada por defecto) no lleva código ASIS; una no externa lo exige. Se pide
al menos un correo porque ahí llega el código de recuperación. Sin clave escrita se genera una de 12
caracteres y, al terminar, la pantalla muestra **una sola vez** usuario y clave para entregárselos (no
se guardan). El camino anterior (buscar a una persona existente y crearle cuenta en `/admin/usuarios`)
sigue disponible para quien ya está en la base de datos.
Prueba: `front_catedras/pruebas/prueba_roles_v71.py` (45/0, con la API simulada).

**D49 — El ponente crea cátedras y se asigna a las existentes.** En el modelo, la participación
(`ponencia`) es **por sesión**, no por cátedra; por eso «asignarse a una cátedra» significa quedar como
`PONENTE` en sus sesiones (todas o las que marque). Rutas nuevas bajo `/ponente/catedras`
(`routes/ponente_catedras.py`, mismo guardia: rol `PONENTE` o `ADMIN`):
`/nueva` crea la cátedra **con sus sesiones** (el mismo maestro-detalle de `/admin`, en una sola
operación todo-o-nada, sin la encuesta al cierre) y deja al creador como ponente de todas; exige al
menos una sesión, porque sin ellas no habría a qué asignarse. `/` busca cátedras **activas** ya creadas
(por otro ponente o por el administrador) y marca en cuáles ya participa. `/<id>` lista sus sesiones con
casillas: «Asignarme a las marcadas» y «Quitarme de las marcadas». Así, si un ponente crea la cátedra,
el segundo no la duplica: la abre y se asigna a ella. Garantías: siempre actúa sobre la persona de la
**sesión** (nunca sobre un `fk_asistente` o `rol` que llegue en el formulario); los ids de sesión se
comparan con las sesiones reales de esa cátedra (los ajenos se ignoran); no se repite una asignación
(evita el 500 de llave duplicada); quitarse borra solo la llave propia (sesión + yo + `PONENTE`); una
cátedra inactiva responde 404. Decisiones para confirmar: el **código ASIS de 9 dígitos** lo escribe el
ponente (es obligatorio por la API y se usa en la migración a ASIS; no se inventa uno); y, como la cátedra
no guarda quién la creó, **cualquier ponente puede asignarse a cualquier cátedra activa** y no puede
editarla ni borrarla después (eso sigue siendo del administrador). Si asignar al creador falla a medias,
la cátedra no se borra: se avisa y se termina con «Asignarme».
Prueba: `front_catedras/pruebas/prueba_ponente_catedras_v71.py` (32/0, API simulada con almacén en memoria).

**D50 — El estudiante se inscribe a una cátedra (recurso nuevo `InscripcionCatedra`).** Hasta aquí el
estudiante solo veía el catálogo y la información; «inscribirse» no existía en el modelo (el pendiente 4 de
la sección 5 preguntaba si se refería al autorregistro de asistencia: no, es esto). Se agrega la tabla
`inscripcion_catedra` (migración `049`) con una fila por (cátedra, persona): **cancelar no borra**, pone
`CANCELADA` y fecha, y volver a inscribirse la reactiva. Tres procedimientos (inscribir, cancelar, listar) que
devuelven un **estado** y no lanzan para los casos de negocio (cátedra inactiva, ya inscrito...). API (único
recurso nuevo del mapa: 37 -> 38): `GET /api/InscripcionCatedra` (filtros `fkAsistente`, `fkCatedra`,
`soloActivas`), `POST /api/InscripcionCatedra` (idempotente: 200 `inscrita` o `ya_inscrita`; cátedra o persona
inactiva/inexistente -> 404) y `POST /api/InscripcionCatedra/cancelar` (404 si no hay inscripción activa).
Front: en la ficha de la cátedra hay una fila fija **«Inscripción»** (no un aviso): el ESTUDIANTE ve **«Inscribirme»** / **«Cancelar inscripción»**, un anónimo ve «Inicia sesión como estudiante» y un ponente o administrador, que solo los estudiantes se inscriben. Corrección posterior: `static/main.js` quitaba a los 5 s **todos** los `.alert`, así que la opción de inscribirse (y el aviso «Registrar mi asistencia», entre otros avisos de la app) desaparecía sola; ahora solo se quitan los mensajes flash (`.alert-dismissible`, los de `partials/mensajes.html`). Rutas: el ESTUDIANTE ve **«Inscribirme»** / **«Cancelar inscripción»**
(`/estudiante/catedras/<id>/inscribirse` y `/cancelar-inscripcion`, solo rol ESTUDIANTE), el panel gana
**«Mis cátedras»**, el administrador las consulta en `/admin/inscripciones` (solo lectura) y el ponente ve
cuántos estudiantes se inscribieron en una cátedra. La persona sale **siempre de la sesión**, nunca del
formulario. Decisiones para confirmar: (1) la inscripción es a la **cátedra**, no a una sesión; (2) **no es
requisito** para registrar la asistencia (no se rompe el flujo de la v5); (3) no hay cupo por cátedra (el
cupo sigue siendo de la sesión); (4) el ponente ve solo el **conteo**, no los nombres.
Pruebas: capas 502/1 (+10; la fallida es la de siempre), `verificar_v71.ps1` 60/0 (+11 de inscripción, contra la
API y PostgreSQL reales), `front_catedras/pruebas/prueba_inscripcion_v71.py` 29/0 (API simulada) y un recorrido
del front contra la API real (login, cancelar, inscribirse, panel, `/admin/inscripciones`).

**D51 — La barra lateral de `/admin` se quita; las categorías del panel son desplegables (reemplaza a D45).**
Pedido posterior: la barra lateral (RF-S7, D45) se retira y el panel `/admin` muestra cada categoría
(«Catálogos», «Estructura académica y cátedras», «Personas y carga de asistentes», «Sesiones y ponentes»,
«Usuarios y registro de asistencia», «Encuestas de calidad», «Migración a ASIS, auditoría y parámetros») como un
`<details>` **cerrado por defecto**, con el número de secciones al lado, para que no se llene la pantalla; sin
JavaScript. Además: **los títulos ya no llevan la versión** (se quitaron los «v2»…«v7») y se quitó la línea
«Las 37 tablas que expone ApiCatedras...» del encabezado. Las dos pantallas que solo estaban en la barra
(«Nuevo ponente» e «Inscripción a cátedra») ahora tienen su tarjeta en el panel (41 tarjetas en total).
`admin/base_admin.html` se conserva como layout mínimo (las 70 plantillas siguen extendiéndolo, sin
cambios); se borraron `_sidebar.html`, `_menu_datos.html`, `static/admin_sidebar.js` y los estilos de la barra.
Consecuencia: dentro de una pantalla de `/admin` la navegación es la miga de pan «Administración» (vuelve al
panel) y el navbar, no un menú lateral permanente.
Prueba: `front_catedras/pruebas/prueba_panel_admin_v71.py` (14/0) y las 105 plantillas compilan.

**D52 — El ponente ve la disponibilidad de un horario y no puede cruzar dos sesiones.** Al crear una cátedra
(`/ponente/catedras/nueva`) el ponente tiene **«Ver ocupación de una fecha»** (elige día y, si quiere, sede: se listan
las sesiones ya programadas, con hora, cátedra, sede y salón) y cada sesión que escribe muestra sola, mientras la
llena, **«✔ Horario disponible»** o **«✖ NO disponible: se cruza con …»**. Al guardar, el servidor vuelve a comprobarlo y,
si algo se cruza, **no crea nada** y deja los choques **fijos en la página** (no como flash, que se borra a los 5 s);
lo escrito vuelve al formulario. Lo mismo al **asignarse a una cátedra existente**: la sesión que se cruza con otra
suya no se asigna (las libres sí) y el aviso queda fijo. Qué es un choque (lo calcula la BD, migración `050`,
`sp_sesiones_en_conflicto`): otra sesión que se **traslapa** en el tiempo (terminar a las 16:00 y empezar a las 16:00
NO cruza) y además **LUGAR** (misma sede **física** y mismo salón; si falta el salón en alguna de las dos se cuenta como
choque, por prudencia; una sede virtual no tiene salón y no se revisa) o **PONENTE** (esa persona ya participa en la otra
sesión, en cualquier sede: nadie está en dos a la vez). Entre las sesiones del mismo formulario también se revisa. API: un
controlador de **solo consulta** (`Disponibilidad`, sin tabla propia; el conteo pasa a 39): `GET
/api/Disponibilidad/conflictos` y `GET /api/Disponibilidad/agenda`; `sp_agenda_sesiones` es la segunda consulta. Si la
disponibilidad no se puede consultar, no se crea nada (falla cerrado). Decisiones para confirmar: (1) la regla de
choque es por **salón + ponente**, no «una sola cátedra a la vez en toda la universidad» (que bloquearía casi todo
con 5.700 sesiones); si se quiere la otra, es solo cambiar `sp_sesiones_en_conflicto`; (2) solo aplica al **ponente**: el
administrador sigue creando sin esta revisión; (3) no hay un candado en la BD (dos ponentes guardando a la vez el mismo
hueco podrían cruzarse): es una verificación de aplicación, no una restricción de la base de datos.
Pruebas: capas 513/1 (+11; la fallida es la de siempre), `verificar_v71.ps1` 72/0 (+12 de disponibilidad, contra la
API y PostgreSQL reales, repetible), `front_catedras/pruebas/prueba_ponente_catedras_v71.py` 57/0 y un recorrido del
front contra la API real (14 casos) más la pantalla en el navegador (consulta por fecha y aviso por sesión).

**Limpiezas pedidas junto a D52.** Se quitó la lista «Mis sesiones» del panel del estudiante (también ya no se
cargan sus registros para el panel) y el texto «Este front consume ApiCatedras...» de la pantalla de inicio.

**D53 — QR de cierre de sesión: se genera con la cátedra, el ponente lo comparte al final y solo lo usa quien está inscrito.**
**Qué es.** Es el enlace de registro de la v5 (`enlace_registro`) con tres reglas: (1) **ventana** que se abre
`QR_MINUTOS_ANTES_FIN` (15) minutos **antes del fin de la sesión** y se cierra al terminar (`QR_MINUTOS_DESPUES_FIN` = 0; si se
quiere margen después del fin, se sube ese parámetro en `/admin`); fuera de ahí el enlace no sirve (RN-15, ya existente);
(2) **inscripción obligatoria** (`enlace_registro.requiere_inscripcion`): solo registra quien tiene inscripción ACTIVA en la cátedra
de la sesión, se comprueba al **pedir la clave** (`fn_solicitar_clave_insc_api`, antes de mandar el correo) y otra vez al
**guardar el registro** (disparador `trg_registro_inscripcion`, por si cancela entre un paso y otro); (3) **ubicación** obligatoria
(RF-S1) y vinculación vigente, como siempre. Los enlaces anteriores (`requiere_inscripcion = false`) no cambian.
**Cuándo se genera.** Al **crear una cátedra** —por el ponente o por el administrador— se emite el QR de **cada sesión**; si algo falla, la
cátedra queda creada y el QR se genera desde «Ver QR». Un solo enlace vigente por sesión (RN-14): emitir uno revoca el anterior.
**Qué ve el ponente.** En su calendario y en el detalle de la cátedra, «Ver QR» en cada sesión suya (`/ponente/catedras/sesion/<id>/qr`; solo
PONENTE u ORGANIZADOR de esa sesión, o un administrador): el QR dibujado en SVG (lleva la URL `<dirección pública>/registro/<token>`),
el estado de la ventana (aún no abre / abierto / cerrado), copiar el enlace, **proyectar en pantalla completa**, **regenerar** (el anterior deja de servir)
y la **asistencia en vivo**: quién de los inscritos asistió, con hora y enlace al mapa de su ubicación, cuántos faltan y quién asistió sin
inscripción (se actualiza sola cada 15 s). Cualquier otro rol recibe 403.
**Qué ve el estudiante.** En la sesión: cómo registrarse (QR del ponente, horario en que sirve, si está inscrito o debe inscribirse y,
si ya registró, «Tu asistencia quedó registrada»). **Se quitó el botón «Registrar mi asistencia» y el token ya no se muestra a los
estudiantes** (D46 lo exponía): si un estudiante pudiera abrir el enlace desde la página, el QR no probaría que estuvo presente.
**Clave por correo.** Con el correo configurado (RF-S6), la clave de un solo uso de la asistencia **se envía al correo y ya no se
muestra en pantalla** (antes se mostraba como «simulación»: quien tuviera el QR y el código de otro lo registraba); sin correo
configurado sigue mostrándose (desarrollo). Los errores del flujo («no estás inscrito», «el enlace está fuera de su ventana»…) ya **no
desaparecen solos** (solo los mensajes de éxito se quitan a los 5 s).
**API.** Controlador `QrSesion` (40.º; sin tabla propia): `POST /api/QrSesion` (idempotente, `regenerar`), `GET /api/QrSesion?fkSesion=`
(204 sin QR) y `GET /api/QrSesion/asistencia?fkSesion=`. La BD pone la ventana (`sp_emitir_qr_sesion`, `sp_consultar_qr_sesion`, `sp_asistencia_sesion`).
**Decisiones para confirmar.** (1) Ventana: 15 min antes del fin y cierre en el fin (parámetros editables). (2) «Asistió» = registró con el QR
dentro de la ventana, con su ubicación; la ubicación se guarda y se muestra, pero **no se compara** con la sede (no hay coordenadas de sedes).
(3) El ponente ve **nombres** de los inscritos de su sesión (necesario para verificar quién asistió); en la cátedra solo veía el conteo.
(4) Para que el QR sirva en celulares hay que publicar el front **en https** y configurar `URL_PUBLICA`; con `http://IP` la ubicación se deniega.
**No verificado:** escanear el QR con la cámara de un celular real ni el envío por un SMTP real; el dibujo del QR se comprobó en el
navegador y que codifica la URL correcta, y todo el flujo con la API, la BD y el front reales.
Pruebas: capas 525/1 (+12; la fallida es la de siempre), `verificar_v71.ps1` 89/0 (+17 de QR, contra la API y PostgreSQL reales; repetible),
`front_catedras/pruebas/prueba_qr_v71.py` 42/0 (API simulada), un recorrido del front contra la API real (29 casos: ponente crea → QR → estudiante
sin inscripción rechazado → se inscribe → registra con ubicación → el ponente lo ve) y la pantalla del QR en el navegador. `verificar_v5.ps1` sigue en 63/0.

**Corrección de D53 (migración 053): el canal del QR sale de la modalidad (RN-13).** La `051` emitía todos los QR con canal `QR`. El script provisto
(RN-13) exige que el canal del enlace concuerde con la modalidad de la sesión (presencial → `QR`; virtual y telepresencial → `ENLACE`; híbrida → `QR`), y con
una sesión virtual la BD respondía `23514: RN-13: la sesion N es de modalidad que se difunde por ENLACE, no por QR` (se reprodujo con la misma sesión). Ahora
`sp_emitir_qr_sesion` deriva el canal igual que `sp_emitir_enlace`. **Todas las sesiones tienen su código QR en pantalla** (es la imagen del enlace); lo que
cambia es el canal que se registra y el `origen` de la asistencia (`QR` o `ENLACE`), y la pantalla del ponente avisa en las sesiones virtuales que pegue el
enlace en el chat de la reunión (o proyecte el QR). El JSON del QR trae `canal`. **Decisión:** no se tocó RN-13 (es una regla del profesor, en el script
provisto); si se quisiera que las sesiones virtuales cuenten como `QR`, habría que cambiar esa regla a propósito. Hay que aplicar la `053` a mano en los
volúmenes que ya tenían la `051` (solo reemplaza dos procedimientos, no toca datos).
Pruebas de la corrección: `verificar_v71.ps1` 119/0 (+7: una sesión de cada modalidad, y el registro de asistencia de una virtual con origen `ENLACE`),
`prueba_qr_v71.py` 45/0 y el recorrido del front contra la API real (30/30, ahora con un servidor SMTP local que recibe el correo de la clave de asistencia
y comprueba destinatario, asunto y acentos; no sale nada a internet).

**D54 — Usuario = correo institucional, clave inicial = código de estudiante, cambio obligatorio en el primer ingreso; portada genérica.**
**Login.** `POST /api/Usuario/login` acepta como `usuario` el nombre de usuario de siempre **o el correo institucional** de la persona (sin importar
mayúsculas ni espacios). La columna `usuario.usuario` no cambia (su CHECK no admite «@»): sigue siendo un nombre corto y el correo es otra forma de
entrar. **Clave inicial = código de estudiante** (`asistente.id_asis`). **No se crean todas las cuentas de antemano** (serían miles de hashes BCrypt):
la cuenta se crea **en el primer ingreso**: si el correo institucional es de un estudiante vigente (vinculación ESTUDIANTE sin fecha de fin, activo,
con código) que aún no tiene cuenta, y la clave escrita es exactamente su código (comparación en tiempo constante), `ServicioUsuario` calcula el hash
BCrypt (Regla 3) y `sp_provisionar_cuenta_estudiante` crea la cuenta con el rol `ESTUDIANTE`, el nombre corto sacado del correo (ajustado al CHECK y sin
repetirse: `maria.lopez`, `maria.lopez2`…) y `debe_cambiar_clave = true`. Cualquier otro intento (clave que no es el código, correo desconocido, docente,
ya tiene cuenta) da el mismo 401 de siempre (D27) y no crea nada.
**Primer ingreso.** `usuario.debe_cambiar_clave` viaja en la respuesta del login. El front guarda `session['debe_cambiar']`, manda a `/perfil/clave` y un
`before_request` global **no deja usar nada más** (ni la portada, ni el catálogo, ni acciones) hasta cambiarla: solo la pantalla de cambio, «Cerrar sesión»
y los archivos estáticos. La pantalla avisa (aviso fijo) que es el primer ingreso y que la clave actual es su código; no vale dejar el mismo código ni
menos de 8 caracteres. Al cambiarla —por «cambiar clave», por recuperación o por un PUT— el disparador `trg_usuario_clave_cambiada` apaga la exigencia solo.
**Para otros.** El administrador puede **exigir el cambio** a cualquier cuenta (`POST /api/Usuario/{id}/exigir-cambio-clave`; botón en `/admin/usuarios`,
que además marca «Cambio pendiente»), y «Nuevo ponente» crea la cuenta con la clave temporal ya marcada para cambiar (`UsuarioCrear.debe_cambiar_clave`).
La cuenta semilla (`alejandro.zuleta`) pasa a esta regla mientras no haya ingresado nunca: su clave inicial es su código (`30000124724`), ya no `Cambiar-123`.
Como esa persona no tiene correo institucional registrado (solo el personal), entra por su nombre de usuario.
**Portada y barra.** La barra superior ya no trae **«Registro de asistencia»** (el estudiante entra por el QR o desde su panel; el enlace del pie de página se
conservó). La portada es **genérica para todos**: qué son las cátedras, información para estudiantes y ponentes, la asistencia con QR y los pasos para
participar; el botón de entrada depende de quién mira (visitante: catálogo e iniciar sesión; estudiante, ponente o administrador: su panel). **Se quitaron las
tarjetas con enlaces a pantallas de `/admin` que veía cualquiera**, y el pie de página muestra los enlaces de administración solo a un ADMIN.
**Correo.** La contraseña de aplicación de `catedracambioc@gmail.com` quedó en un `.env` **local** (no va en el zip ni en el repositorio) y se comprobó que
Gmail la acepta (login SMTP; no se envió ningún correo). Como la contraseña se pegó en una conversación, conviene revocarla y crear otra.
**Riesgos para decidir.** (1) El código de estudiante no es un secreto fuerte: quien conozca el correo institucional y el código de un compañero que **aún
no ha entrado** puede activar su cuenta antes que él; el cambio obligatorio solo protege si el dueño entra primero. Mitigación posible: entregar el código
por un canal propio o usar una clave inicial aleatoria enviada al correo. (2) No hay límite de intentos de login (como antes). (3) La persona sin correo
institucional registrado no puede usar este camino (entra por usuario con cuenta creada a mano).
Pruebas: capas 545/1 (+20; la fallida es la de siempre), `verificar_v71.ps1` 112/0 (+23 de D54, contra la API y PostgreSQL reales; repetible, y la migración se
volvió a aplicar encima sin errores), `front_catedras/pruebas/prueba_primer_ingreso_v71.py` 42/0, recorrido del front contra la API real (19 casos) y
`verificar_v5.ps1` sigue en 63/0. Un error propio se cazó con la BD real: `rpad` **recorta** los textos largos y dejaba el nombre de usuario en `est.`; corregido.

**D55 — Solo el administrador inicia sesión; se quitan la inscripción a cátedras y el cupo.** Cambio de alcance pedido después de D43–D54. **Lo que queda sin efecto
de las decisiones anteriores:** D43 (paneles `/estudiante` y `/ponente`), D46 (el estudiante ve «Registrar mi asistencia» y el token), D47 (calendario del ponente
solo propio), D48 («Nuevo ponente» con cuenta), D49 (el ponente crea cátedras y se asigna), D50 (inscripción a cátedras), D54 en lo de estudiantes (correo institucional como
usuario, clave inicial = código, primer ingreso que crea la cuenta) y la regla «con el QR solo registra quien está inscrito» de D53. Esos textos se conservan arriba como
historia; esto es lo que vale ahora.
**Login.** `ServicioUsuario.LoginAsync` exige el rol **ADMIN vigente** (además de usuario activo y clave correcta); cualquier otra cuenta recibe el mismo 401 de siempre (D27) y
no se le anota acceso. El front también lo comprueba. No hay cuentas de estudiante ni de ponente que entren: quedan como **personas** (`asistente`) y como `Ponencia` de las sesiones
(eso lo asigna el administrador en `/admin`). El login vuelve a ser solo por **nombre de usuario** (se borraron la búsqueda por correo y la creación de cuentas en el primer ingreso:
`sp_buscar_estudiante_sin_cuenta`, `sp_provisionar_cuenta_estudiante`). Se conserva `debe_cambiar_clave` (cambio obligatorio para una cuenta con clave temporal, el botón «Exigir
cambio de clave» y el disparador que lo apaga) porque sirve a cualquier administrador nuevo. Las cuentas ya existentes de estudiante o ponente (p. ej. `alejandro.zuleta`) quedan sin
poder entrar; no se borraron.
**Qué pasó con lo que hacía el ponente.** Se movió al administrador: **crear cátedras con sus sesiones**, la **ocupación de una fecha** y el bloqueo de cruces (misma sede física y
mismo salón, o entre las sesiones del mismo formulario) en `/admin/catedras/nueva`, y la pantalla del **QR de la sesión** con la asistencia en vivo, regenerar y proyectar en
`/admin/sesiones/<id>/qr` (enlazada desde el detalle de la cátedra y de la sesión). El QR se sigue generando al crear la cátedra. Dejó de existir el motivo «ya participa el mismo
ponente» en la pantalla (no hay ponente que cree), aunque `sp_sesiones_en_conflicto` lo conserva.
**Asistencia pública.** `/registro/<token>` ya era público: quien escanea el QR se identifica con su documento o correo, recibe la clave por correo y registra con su ubicación; ya no
hace falta estar inscrito ni tener cuenta. La página pública de la sesión explica cómo (sin mostrar el token). **La encuesta** (v6) dependía de iniciar sesión: ahora se responde
**justo después de registrar la asistencia**, con el registro recién creado (queda en la cookie firmada; si se cierra esa página ya no se puede). La lista de asistencia del administrador
muestra solo a quien registró (no hay inscritos).
**Inscripción.** Se quitaron la tabla `inscripcion_catedra` (la 054 la borra **solo si está vacía**; si tiene filas la conserva y avisa), sus procedimientos, el recurso `InscripcionCatedra`
de la API (el mapa vuelve a 39 controladores), las pantallas del estudiante y del administrador y la regla de QR. El QR de cierre se marca ahora con `enlace_registro.qr_cierre` (antes
`requiere_inscripcion`, que queda en `false`); `fn_solicitar_clave_insc_api` se conserva como un envoltorio sin comprobación para una API anterior.
**Cupo.** Se quitó de todo: formularios y vistas del front, modelos y peticiones de la API (el campo se ignora si llega) y la **regla RN-38** del disparador
`fn_trg_registro_validar` (`054` lo recrea sin ella). La columna `sesion.cupo` **no se borra** porque la usan los procedimientos del script provisto; queda dormida (se escribe `NULL`).
**Decisiones para confirmar.** (1) Si algún día el ponente debe iniciar sesión otra vez, hay que volver a la rama anterior (queda en el historial de decisiones D43–D49). (2) Ya nadie
(salvo el administrador) puede consultar «mis cátedras» ni «mis sesiones». (3) La encuesta solo se responde en la misma página/navegador donde se registró la asistencia. (4) Las cuentas de
estudiante o ponente que ya existieran siguen en la base sin poder entrar. (5) Hay que reconstruir la API y el front **antes** de aplicar la 054 en una base con la versión anterior.
Pruebas: capas 528/1 (la fallida es la de siempre), `verificar_v71.ps1` 100/0 (repetible; incluye que un estudiante o un ponente con la clave correcta recibe 401, que la inscripción ya no existe
y los cambios de clave de un administrador), `verificar_v5.ps1` sin fallos (sus dos pruebas de «login de una cuenta sin rol ADMIN» pasaron a esperar 401), `front_catedras/pruebas/prueba_d55_v71.py`
78/0 y `prueba_panel_admin_v71.py` 14/0 (API simulada), un recorrido del front contra la API y PostgreSQL reales (28/28: login solo ADMIN, crear cátedra con QR, bloqueo de cruces, asistencia
pública con la clave por correo a un servidor SMTP local, encuesta justo después y asistencia en vivo) y la migración 054 aplicada sobre una base nueva y sobre una con filas de inscripción.

**D56 — Calendario público con «Soy ponente» y bandeja de entrada del administrador (la elección de sesión y lo que pasa al aprobar cambiaron en la D58: el ponente propone una cátedra nueva con fecha y hora y al aprobar se crea sola).** Pedido posterior a D55. Como los ponentes no tienen cuenta, quien quiere dictar una
ponencia **se postula desde un formulario público** y el administrador decide.
**Calendario (`/calendario`, en el navbar, sin sesión).** Cuadrícula mensual, semana y lista (FullCalendar 6 por CDN; en celular abre en lista) con las sesiones de todas las cátedras, un
color por modalidad y, al tocar una, su detalle (fecha, hora, sede, salón, modalidad, reunión y enlace al detalle público). Las sesiones salen de `GET /api/Disponibilidad/agenda` (D52, ya traía el
nombre de la cátedra y de la sede): el front pide el rango visible en **tramos de 30 días** (la agenda no acepta más de 31) y los junta sin repetir. Las horas se entregan como hora de pared de Colombia
(UTC-5, sin zona) y el calendario va en `UTC`, así se ven igual en cualquier navegador. Si la API falla se muestra el error: nunca se inventan sesiones (RF93).
**Botón «Soy ponente».** Abre un formulario con: nombres, apellidos, **tipo de documento** (cédula `CC`, pasaporte `PA`, licencia de conducción `LC`, código institucional `CI`; los dos últimos no
estaban en el catálogo y los agrega la `055`), número de documento, **perfil profesional**, **dirección**, **teléfono**, **correo institucional**, **ponencia** (título) y **resumen de la ponencia**
(30–2000 caracteres), más una **sesión opcional** (las de los próximos 60 días; «Propongo una cátedra nueva» si no elige ninguna; también se puede abrir desde el detalle de una sesión futura) y la
autorización de tratamiento de datos (Ley 1581). Las reglas de cada campo las aplica la API (422 con la lista `{campo, mensaje}` que el front pinta junto a cada campo; lo escrito no se pierde).
Campo trampa antispam (`sitio_web`). Sin captcha ni límite de envíos.
**Bandeja (`/admin/solicitudes-ponente`, solo ADMIN, tarjeta destacada en `/admin` con el número de pendientes).** Pestañas Pendientes / Aprobadas / Rechazadas / Todas; cada solicitud abre su
detalle con todos los datos y la decisión. **Aprobar** (observación opcional) llama a `PATCH /api/SolicitudPonente/{id}/resolver` y la **base de datos hace todo en una transacción**
(`sp_resolver_solicitud_ponente`): busca a la persona por documento y, si no está, por **correo institucional** (es único: `uq_asistente_corinst`); si no existe la crea como **externa**
(`asistente` + `documento_asistente`); y, si eligió sesión, la asigna como **PONENTE** (`ponencia`). Sin sesión, la persona queda registrada y la ponencia aprobada; la cátedra se crea a mano desde
`/admin/catedras/nueva`. **Rechazar** exige el motivo. Una solicitud se resuelve **una sola vez** (otra resolución → 409) y guarda quién y cuándo (`resuelto_por`, `resuelto_en`).
**API (controlador 40).** `SolicitudPonente`: `GET` (lista con `?estado=` y `?limite=`; 204 si no hay), `GET /{id}`, `POST` y `PATCH /{id}/resolver`; sin PUT ni DELETE. **Desviación de D25:** las reglas de
negocio que la base de datos levanta con `RAISE` (código `P0001`: «ya hay una pendiente igual», «ya fue resuelta») responden **409**, no 500; los demás rechazos del motor (FK, unicidad) siguen en 500.
BD: migración `055` (tabla `solicitud_ponente`, vista `v_solicitud_ponente`, 4 procedimientos), montada en el compose como `z055`.
**Decisiones para confirmar.** (1) El correo **no** se obliga a ser del dominio institucional (hay ponentes externos): la bandeja avisa «dominio no institucional» si no está en `DOMINIOS_INSTITUCIONALES`.
(2) La persona nueva se crea siempre como **externa** (`es_externo = true`), aunque use código institucional: ese código no es el `id_asis`. (3) Dirección y perfil profesional quedan **solo en la
solicitud** (`asistente` no tiene esas columnas). (4) **No se envía correo** al administrador cuando llega una solicitud ni a la persona cuando se decide: el aviso es el contador del panel; el motivo del rechazo
queda guardado pero la persona no lo recibe. (5) Se aprueba también si la sesión elegida ya empezó o se cruza con otra de esa persona (no se revisa disponibilidad al aprobar).
Pruebas: capas 566/1 (+38; la fallida era la de las peticiones de Bitácora, ya resuelta), `front_catedras/pruebas/prueba_calendario_ponente_v71.py` 70/0 (API simulada), `prueba_panel_admin_v71.py` 14/0 y `prueba_d55_v71.py` 78/0, y un
recorrido contra la API y PostgreSQL reales en un contenedor aparte (422/409/404/400 de la API, aprobar creando persona + documento + ponencia, reutilizar a la persona por documento y por correo,
rechazar sin y con motivo, no resolver dos veces, y el recorrido en el navegador: enviar el formulario desde el calendario y aprobarlo en la bandeja).

**D57 — Periodos y horarios en que se pueden programar cátedras; formulario «Soy ponente» con scroll; botón «Administración».** Pedido posterior a D56.
**Periodos y horarios (`/admin/restricciones-programacion`, grupo «Estructura académica y cátedras», tarjeta «Periodos y horarios»).** El administrador define **ventanas**: nombre, fechas
(desde/hasta, inclusive), horas opcionales (desde/hasta, van juntas) y días de la semana opcionales (sin marcar = todos), y puede **activarlas o pausarlas** sin borrarlas. **Sin ventanas activas no hay
restricción** (el comportamiento de siempre); con alguna, una sesión solo se puede **crear o mover** a un horario que quepa en una: sus fechas de inicio y fin dentro del periodo, empezar a partir de la
primera hora y terminar a más tardar a la segunda **el mismo día**, y caer en un día permitido; todo en hora de Colombia. **La regla vive en la base de datos** (migración `056`: disparador `trg_sesion_restriccion`
sobre `sesion`), así vale para el formulario de cátedra (que guarda cátedra + sesiones en un solo procedimiento: si una sesión no cabe, **no se crea nada**), el de sesión y la API directa. El disparador solo
actúa en sesiones nuevas o cuando **cambia** inicio o fin: corregir el salón de una sesión vieja no se bloquea. Un rechazo sale como `P0001` y la API lo devuelve **409** con el mensaje y las ventanas donde sí se
puede (`Sesion` y `Catedra`; otra desviación de D25, igual que D56). Ayudas del front: aviso fijo con las ventanas activas en «Nueva cátedra» y «Nueva/Editar sesión», y el aviso en vivo de disponibilidad
(`/admin/catedras/disponibilidad`) ahora dice «✖ fuera de los periodos…» además de los cruces. API (controlador 41): `RestriccionProgramacion` con `GET` (`?soloActivas=`; 204 si no hay), `GET /{id}`, `POST`,
`PUT /{id}` (también activa o pausa), `DELETE /{id}` y `GET /validar?inicio=&fin=` (solo lectura).
**«Soy ponente» con scroll.** El `<form>` quedaba entre `.modal-content` y `.modal-body`, así Bootstrap recortaba el modal en lugar de desplazar el cuerpo: se veía hasta «Tu ponencia» y nada más. `estilo.css`
vuelve al form una columna flexible: el cuerpo hace scroll con barra y el encabezado y los botones quedan siempre a la vista.
**Botón «Administración».** Sin sesión, el botón de la barra dice solo «Administración» (lleva a iniciar sesión; el administrador cae en su panel). Con sesión no cambia.
**Decisiones para confirmar.** (1) Los **cruces de medianoche** no caben en una ventana con horas (si hay horas, la sesión debe empezar y terminar el mismo día). (2) Varias ventanas activas se suman:
basta con caber en **una**. (3) Una ventana **no está ligada** a `periodo_academico` (son independientes: el nombre es libre). (4) Borrar o pausar una ventana **no toca** las sesiones ya creadas.
(5) Que una cátedra esté «fuera de periodo» solo se impide al **crear o mover** sesiones; no se revisa al leer ni al registrar asistencia. (6) El ADMIN no tiene «saltarse la restricción»: para programar fuera hay
que pausar o ampliar la ventana.
Pruebas: capas 598/1 (+32; la fallida era la de las peticiones de Bitácora). Después se borraron `BitacoraCrear/Reemplazo/Actualizar.cs` (clases muertas que D36/D37-A ya había mandado quitar): capas **599/0**, `front_catedras/pruebas/prueba_restricciones_v71.py` 39/0 (API simulada), `prueba_calendario_ponente_v71.py` 70/0, `prueba_panel_admin_v71.py` 14/0 y
`prueba_d55_v71.py` 78/0, y un recorrido contra la API y PostgreSQL reales en un contenedor aparte (crear la ventana, validar dentro/fuera de fechas, antes y después de las horas, domingo y cruce de medianoche,
409 al crear o mover una sesión fuera, mover solo el salón de una sesión vieja sin bloqueo, cátedra con una sesión fuera = no se crea nada, pausar y borrar) y en el navegador (lista y formulario de ventanas,
aviso en «Nueva cátedra», scroll del formulario «Soy ponente», botón «Administración»).

**D58 — «Soy ponente» propone una cátedra nueva con fecha y hora; al aprobar, la cátedra se crea sola (reemplaza en parte a D56).** Pedido posterior a D57.
**El formulario.** «Sesión a la que te postulas» se quitó. En su lugar hay **«Cátedra nueva»**: el ponente elige la **fecha y la hora de inicio y de fin** en que quiere dictarla (hora de Colombia; no se puede
elegir el pasado y, al poner el inicio, el fin se propone dos horas después). Si el administrador definió **periodos y horarios** (D57), se le muestran. Al enviar, la API revisa que el fin sea posterior al inicio y que
no dure más de 24 horas (400) y la base de datos que la fecha sea futura y quepa en las ventanas activas (409, con las ventanas donde sí se puede). Los errores se muestran en el formulario, que hace scroll hasta el primer aviso.
También se quitó «Soy ponente de esta sesión» del detalle de una sesión del calendario.
**Modalidad, sede y periodo los elige el ponente (migración `058`).** El formulario trae tres `<select>` del catálogo, obligatorios: **modalidad**, **sede** (solo las activas) y **periodo académico** (solo los activos que no han
terminado; al poner la fecha, `calendario.js` elige solo el periodo que la cubre). Reglas (409): el periodo debe estar activo y **la fecha debe caer dentro de él** (las cátedras se dictan en periodos específicos), y la sede debe
estar activa; se revisan al proponer y otra vez al aprobar (`fn_validar_periodo_y_sede`).
**La decisión (`/admin/solicitudes-ponente/<id>`).** Una solicitud con fecha propuesta muestra la tarjeta **«Cátedra que se creará al aprobar»** con valores sugeridos y editables: nombre (el título de la ponencia),
inicio y fin (los propuestos), **modalidad, sede y periodo (ya elegidos por el ponente; en una solicitud anterior a la 058 se sugiere presencial y el periodo que cubre la fecha, y la sede solo si hay una activa)**, salón,
tipo de evento (cátedra abierta) y código ASIS (vacío = el siguiente libre). Lo que el administrador no escribe se toma de lo que eligió el ponente. Antes de aprobar se revisa que el horario no se cruce con otra sesión en esa sede y salón (D52; si no se puede consultar, no se aprueba;
los choques se muestran y la pantalla conserva lo escrito).
**Aprobar = crear (`sp_resolver_solicitud_ponente`, migración `057`, una sola transacción).** Persona (por documento, luego por correo; si no, nueva como externa) + **cátedra con su sesión** (reutiliza `sp_guardar_catedra`;
el disparador de la 056 revisa periodos y horarios) + **ponencia** (PONENTE de esa sesión); la solicitud guarda `fk_catedra` y `fk_sesion`. Después el front genera el **QR de la sesión** (D53). Si algo falla (sede
inexistente, código ASIS repetido, fecha ya pasada, fuera de periodo...) **no se crea nada** y la solicitud sigue pendiente. **Rechazar** sigue pidiendo el motivo y no crea nada. Las solicitudes anteriores a la 057
(que elegían una sesión) se aprueban como antes. API: `POST /api/SolicitudPonente` ahora lleva `inicio` y `fin` (sin `fk_sesion`) y `PATCH .../resolver` acepta los datos de la cátedra (`nombre_catedra`, `fk_tipo_evento`,
`id_evento_asis`, `fk_modalidad`, `fk_sede`, `fk_periodo`, `lugar`, `inicio`, `fin`); aprobar una solicitud con fecha sin modalidad, sede y periodo responde 400.
**Decisiones para confirmar.** (1) Lo que el ponente **no** elige (salón, tipo de evento, nombre ASIS) lo decide el administrador al aprobar; modalidad, sede y periodo los puede cambiar.
(2) El código ASIS automático es `máximo + 1` de los códigos existentes: es un número interno, **no** el del ASIS real; si ya existe en el ASIS, el administrador lo escribe. (3) La cátedra nace con **una** sesión (las demás se
agregan desde la cátedra). (4) El resumen y los datos personales (dirección, perfil) quedan solo en la solicitud. (5) La cátedra se crea activa y sin dependencia ni encuesta. (6) **La fecha debe caer dentro del periodo
elegido** (regla de la `058`): si el administrador cambia la fecha o el periodo al aprobar, también se revisa; una fecha que ningún periodo cubre no se puede proponer.
Pruebas: capas 619/0, `front_catedras/pruebas/prueba_calendario_ponente_v71.py` 108/0, `prueba_restricciones_v71.py` 39/0, `prueba_panel_admin_v71.py` 14/0 y `prueba_d55_v71.py` 78/0 (API simulada), un recorrido
contra la API y PostgreSQL reales (proponer con fecha pasada, fin antes del inicio, de más de 24 horas, fuera de periodo y repetida; aprobar sin datos, con sede inexistente —no queda nada—, con código ASIS repetido
—no queda nada—, con código automático, con nombre, tipo, modalidad y fecha ajustados, con una ventana activa que no cabe; rechazar; aprobar una solicitud anterior) y en el navegador: enviar el formulario desde el
calendario con fecha y hora, y aprobarlo creando la cátedra, su sesión, su ponente y su QR.

**D59 — Una hora de margen entre sesiones, enlace de ingreso al crear la cátedra y depuración del catálogo (migración `059`).** Pedido posterior a D58.
**Margen entre sesiones.** Todas las cátedras entran por el mismo enlace, así que **no pueden haber dos sesiones a la misma hora, en ninguna modalidad ni sede, y entre una y otra debe haber al menos 60 minutos**
(terminar a las 10:00 y empezar a las 11:00 vale). Los minutos viven en el parámetro `MINUTOS_ENTRE_SESIONES` (60). La regla la aplica la base de datos con el disparador `trg_sesion_separacion` sobre `sesion`
(INSERT, o UPDATE que cambie inicio o fin; toma un candado para que dos transacciones simultáneas no se la salten): cubre el formulario de cátedra, la sesión suelta, el calendario y la aprobación de un ponente; el
rechazo es un `P0001` (409 en la API) con el nombre de la cátedra con la que choca. `fn_validar_separacion` hace la misma revisión al proponer en «Soy ponente» y al aprobar, antes de crear a la persona. `sp_sesiones_en_conflicto`
devuelve ahora también el motivo `HORARIO` (más `solapa`: si además se traslapan) y los formularios del administrador lo muestran antes de guardar («Horario NO disponible… choca con…», y el aviso de dos sesiones del mismo formulario).
**Solo cuentan las sesiones de cátedras activas**: una cátedra desactivada deja de ocupar el enlace (si no, el histórico desactivado bloquearía horarios que ya nadie usa). Las sesiones que ya existían **no se tocan**: solo
cuenta lo que se cree o se mueva desde ahora.
**Enlace de ingreso.** `sesion.enlace_virtual` ya existía; ahora se pide al **crear la cátedra** (campo «Enlace de ingreso» en cada sesión del formulario) y en la tarjeta **«Cátedra que se creará al aprobar»** de la bandeja
(`sp_resolver_solicitud_ponente` recibe `p_enlace_virtual` y lo guarda en la sesión que crea). Es opcional pero recomendado en virtual o híbrida, debe empezar por `http://` o `https://` (hasta 300 caracteres) y se valida en
las cuatro peticiones de sesión de la API y en la base de datos. El calendario público **no** muestra el enlace.
**Depuración del catálogo.** Las 5.502 cátedras activas se redujeron a **500**: las demás se **desactivaron** (borrado lógico, no se borró ninguna fila: se ven con «Ver inactivas» y se reactivan al editar), con respaldo previo.
Criterio: primero las que tienen historia real (asistencias, ponentes, enlaces, lotes de migración o una solicitud de ponente), luego las que tienen una sesión programada futura y luego las más cercanas a la fecha de hoy
(quedaron las de octubre y noviembre de 2026 casi todas). **Decisiones para confirmar.** (1) Se desactivó y no se borró; si se quieren borrar de verdad hay que quitar antes sus asistencias y lotes (las llaves son `RESTRICT`).
(2) Entre las 500 que quedaron hay sesiones que ya se cruzaban (el histórico venía así): la regla no las toca, pero sí impide crear otra encima. (3) El margen es por sesión, no por cátedra: dos sesiones de la misma cátedra también
deben estar separadas. (4) Dos solicitudes de ponente **pendientes** pueden proponer la misma hora: la segunda que se apruebe será rechazada (409) y el administrador le ajusta la hora.
(5) El script manual `db/postgres/pruebas/prueba_sp_maestro_detalle_formulario.sql` (anterior a la D59) crea varias cátedras en las mismas horas: con la `059` aplicada esas sesiones se rechazan; hay que darles horas distintas
(o correrlo sin la `059`) antes de volver a usarlo.
Pruebas: capas 626/0, `prueba_d55_v71.py` 86/0 y `prueba_calendario_ponente_v71.py` 111/0 (API simulada), y contra PostgreSQL real, con rollback: misma hora, a 30 minutos (antes y después) y mover una sesión rechazadas; a
exactamente 60 minutos aceptada; editar otros datos de una sesión existente sigue permitido; una cátedra desactivada no bloquea y una activa sí.

## 4. Cómo se verificó

* `dotnet run --project pruebas`: **492 pasadas, 1 fallida**. La fallida
  («No quedan peticiones de escritura de Bitacora») **ya fallaba antes** de la
  v7.1 (467/1 sobre el código original) y no tiene relación con ella. La
  subversión agrega 25 pruebas de capas.
* `pruebas/verificar_v71.ps1` (API real + PostgreSQL): **49 / 0**. Cubre roles,
  semilla, geolocalización (422 sin/fuera de rango, la clave no se gasta,
  ubicación guardada, MANUAL sin ubicación), cambio de clave, recuperación
  (anulación del código anterior, un solo uso, tope de intentos).
* `verificar_v5.ps1` (se le agregó la ubicación a sus llamadas a `validar`):
  63 / 0. `verificar_v7.ps1`: 71 / 0 (sobre BD nueva).
  `prueba_sp_maestro_detalle_formulario.sql`: 32 / 0.
  `prueba_sp_retrofit_fase4.sql`: 160 / 0.
* Una BD nueva con las 48 migraciones arranca sin errores.
* Front: las 60 rutas `GET` de `/admin` sin parámetros renderizan con la
  barra; flujos de estudiante, cambio y recuperación de clave y autorregistro
  con ubicación recorridos contra la API real; barra (escritorio, colapsar y
  recordar; móvil, abrir/cerrar con Esc) y estados del script de ubicación
  (negado, no disponible, tiempo agotado, éxito) comprobados en el navegador.
* **No verificado:** el envío por un SMTP real (solo el modo consola); la
  ubicación con un GPS real (se simularon los 4 resultados de
  `getCurrentPosition`); `docker compose up` completo (la BD y la API se
  probaron en contenedor/proceso aparte con las mismas migraciones).

## 5. Pendiente de confirmar con el usuario / el equipo

1. **Datos del estudiante semilla marcados como provisionales** (`PENDIENTE-1…5`
   en la `048`): documento de identidad (se conserva el de relleno
   `CC 1011390488`), periodo de la matrícula (`2026-2`), inicio de la
   vinculación, clave inicial = su código `30000124724` con cambio obligatorio (D54; usuario `alejandro.zuleta`) y qué
   hacer con la matrícula de relleno `M0501 / 2026-1`, que se conservó.
2. **Correo (RF-S6): remitente definido, falta la credencial.** La cuenta de envío es
   `catedracambioc@gmail.com` (Gmail, SMTP `smtp.gmail.com:587` STARTTLS; ya está en
   `front_catedras/env.ejemplo`). Falta que el equipo active la verificación en 2 pasos de esa cuenta,
   cree una «contraseña de aplicación» y la ponga en `SMTP_CLAVE` de su `.env` local (no se sube al
   repositorio). Hasta entonces la recuperación de clave avisa que no pudo enviar el correo.
3. **Si cambiar la clave debe exigir también el código por correo.** Se dejó
   que **no** (D40); es un cambio de pocas líneas si el equipo lo quiere.
4. **«Inscripción de una cátedra»**: se había interpretado como el autorregistro de asistencia, pero el
   usuario pidió que el estudiante **se inscriba** a las cátedras; eso es ahora D50 (el autorregistro de
   asistencia sigue siendo aparte y no exige estar inscrito).
5. **Crear cuentas de estudiante** sigue siendo manual desde
   `/admin/usuarios` y `/admin/roles-usuario`; la de **ponente** tiene su propio formulario (D48) (ahora aparecen `ESTUDIANTE` y
   `PONENTE` en el `<select>`). Un flujo de autoservicio queda fuera de alcance.
6. **Opcionales que se dejaron:** el atajo de «pegar el enlace» en el panel del
   estudiante existe, pero solo reenvía al flujo público (que ya funciona con
   sesión iniciada); no hay límite de solicitudes por IP para `/recuperar`.
