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
| RF-S3 | Cuentas reales de Estudiante y Ponente | BD `046` · front (`auth_helpers.py`, `auth.py`, `ponente.py`, `estudiante.py`, navbar) |
| RF-S4 | Estudiante semilla | BD `048` |
| RF-S5 | Cambio de contraseña para cualquier usuario autenticado | API (`POST /api/Usuario/{id}/cambiar-clave`, recuperación) · front (`routes/perfil.py`) · BD `047` |
| RF-S6 | Correo real para el código | front (`servicios/correo.py`, `config.py`, `env.ejemplo`, compose) |
| RF-S7 | `/admin` con barra lateral colapsable (reemplazada por categorías desplegables, D51) | front (`admin/base_admin.html`, `admin/panel.html`, `estilo.css`) |

Migraciones nuevas (en orden; se montan en el compose como `z045`…`z049`):
`045_geolocalizacion_registro` · `046_roles_estudiante_ponente` ·
`047_codigo_recuperacion` · `048_semilla_estudiante` · `049_inscripcion_catedra`.

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
   vinculación, clave temporal `Cambiar-123` (usuario `alejandro.zuleta`) y qué
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
