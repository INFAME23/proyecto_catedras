# Guía operativa para la IA

> Este documento **no es la constitución** (`1_constitution.md`) y no la
> reemplaza ni la modifica. Son reglas técnicas transversales que se suman
> a las ya existentes y rigen TODAS las versiones a partir de su adopción
> (ver tabla de estado al final). Ante conflicto con la constitución,
> la constitución gana; ante conflicto con una spec de versión, esta guía
> gana porque es más específica en estos puntos.

## Regla 1 — Procedimientos almacenados para tablas maestro-detalle

Cuando una operación de negocio crea o reemplaza un **maestro junto con
sus detalles** en una sola petición del cliente, esa operación se resuelve
con **un único `CREATE PROCEDURE`** en PostgreSQL que inserta/actualiza
maestro y detalles **en una sola transacción**. El repositorio lo invoca
con `CALL` a través de Dapper — nunca con dos `INSERT` sueltos desde C#.
Si el procedimiento devuelve un valor (p. ej. el id generado), lo hace con
un parámetro `INOUT`: el `CALL` responde una fila con ese valor y Dapper la
lee con `QuerySingleAsync<T>` (así lo hace `sp_guardar_asistente`, v3).
(`SELECT * FROM` y `RETURNS TABLE` son de `FUNCTION`, no de `PROCEDURE`.)

**No aplica** cuando el detalle se crea en un momento distinto al maestro,
por su cuenta (ej. agregar un alias a un programa ya existente semanas
después): ahí cada tabla conserva su propio CRUD normal, porque no hay
"una sola petición" que unir.

Patrón de nombre: `sp_guardar_<maestro>` — parámetros del maestro +
un parámetro `jsonb` con el arreglo de detalles (mismo estilo que los SP
que ya trae la BD, ej. `sp_responder_encuesta`). Se admite abreviar el
nombre del maestro cuando es largo (`sp_guardar_lote_carga` para
`lote_carga_asistente`).

**Maestro-detalle identificados en el proyecto:**
| Maestro | Detalle(s) | Versión |
|---|---|---|
| `programa_academico` | `alias_programa` | V2 (retrofit hecho: `sp_guardar_programa_academico`) |
| `asistente` | `documento_asistente`, `vinculacion_asistente`, `programa_asistente`, `consentimiento_datos` | V3 (hecho: `sp_guardar_asistente`, 4 parámetros `jsonb`) |
| `lote_carga_asistente` | `novedad_carga` | V3 (hecho: `sp_guardar_lote_carga`, al CERRAR el lote — ADR D15) |
| `pregunta` | `opcion_pregunta` | V6 (hecho: `sp_guardar_pregunta`, ADR D30) |
| `catedra` | `sesion` | Formulario maestro-detalle (hecho: `sp_guardar_catedra`, migración 042; reutiliza `sp_crear_catedra` y `sp_crear_sesion`) |
| `encuesta` | `pregunta` → `opcion_pregunta` (2 niveles) | Formulario maestro-detalle (hecho: `sp_guardar_encuesta`, migración 043; reutiliza `sp_guardar_pregunta` por cada pregunta) |
| `sesion` | `enlace_registro` (el primero) | Formulario maestro-detalle (hecho: `sp_guardar_sesion`, migración 044; reutiliza `sp_crear_sesion` y `sp_emitir_enlace` del script) |

## Regla 2 — Las claves foráneas se seleccionan, no se digitan

Todo campo de un formulario del front que sea una FK se llena con un
`<select>` poblado desde el catálogo real (vía la API), con las opciones
`id · nombre`. Nunca un `<input type="text">` libre para un código de FK.

## Regla 3 — Contraseñas cifradas con BCrypt

Toda columna que guarde una credencial de usuario (ej. `usuario.clave`)
se cifra con `BCrypt.Net-Next` antes de persistir — nunca texto plano ni
cifrado reversible. El hash se calcula en el **servicio**, nunca en el
controlador ni en el repositorio. Se compara con `BCrypt.Verify`, nunca
comparando el hash manualmente.

## Regla 4 — En el front, al entrar al maestro aparecen sus detalles

La Regla 1 habla de **guardar** maestro y detalles juntos; esta habla de
**verlos** juntos. Todo maestro tiene en `/admin` una pantalla de detalle
(`/admin/<maestro>/<id>`) con su ficha arriba y, debajo, una tabla por
cada detalle, con el botón para agregar uno nuevo (la FK del maestro la
fija esa pantalla, no se digita: Regla 2).

| # | Maestro | Su detalle | Qué lo hace detalle | Pantalla (`routes/admin.py`) |
|---|---|---|---|---|
| 1 | `catedra` | `sesion` | La principal del sistema. Una sesión es de **una** cátedra y su número no se repite dentro de ella: `uq_sesion_reunion` | `catedras_detalle` |
| 2 | `sesion` | `enlace_registro` | Un enlace de registro es de **una** sesión | `sesiones_detalle` |
| 3 | `sesion` | `registro_asistencia` | Una asistencia es de **una** sesión | `sesiones_detalle` |
| 4 | `asistente` | `documento_asistente` | Un documento es **de** un asistente; sin él no significa nada | `asistentes_detalle` (v3) |
| 5 | `asistente` | `consentimiento_datos` | Un consentimiento es **de** un asistente | `asistentes_detalle` (v3) |
| 6 | `encuesta` | `pregunta` | Su `orden` es único **dentro de la encuesta**: `uq_pregunta_ord` | `encuestas_detalle` |
| 7 | `pregunta` | `opcion_pregunta` | Las opciones de una pregunta cerrada: `uq_opcion_orden` | `preguntas_detalle` |
| 8 | `registro_asistencia` | `respuesta_encuesta` | La respuesta cuelga del **registro**, no de la encuesta: `uq_respuesta_registro` | `registros_detalle` |
| 9 | `respuesta_encuesta` | `respuesta_item` | Un ítem por pregunta respondida | `respuestas_detalle` (v6) |
| 10 | `programa_academico` | `alias_programa` | Los nombres alternos de un programa | `programas_detalle` |

Cómo se navega:

- **Crear o editar un maestro** aterriza en su pantalla de detalle (así se
  ven enseguida sus detalles y se pueden agregar).
- En las **listas**, el id / nombre del maestro y el botón del ojo llevan
  a esa pantalla; en las listas de detalles, la columna de la FK lleva al
  maestro.
- Un detalle que se crea, edita o borra **desde la pantalla del maestro**
  manda `volver=<ruta del maestro>` y la acción regresa ahí. Sin `volver`,
  la acción conserva su destino de siempre. `volver` solo acepta rutas
  internas de `/admin/` (no es una redirección abierta).
### En el FORMULARIO del maestro también se agregan sus detalles

El formulario de crear / editar de cada maestro trae un bloque de **filas
de detalle** ("Agregar …" / "Quitar"; `templates/partials/filas_detalle.html`
y `static/main.js`). Cada campo de una fila se llama `<prefijo>_<campo>` y
Flask los lee con `request.form.getlist` (`_filas` en `routes/admin.py`).
Dentro de una fila no hay checkboxes (un checkbox sin marcar no se envía y
desalinearía las listas): los sí/no van en un `<select>`.

| Formulario | Detalles que se agregan ahí | Al CREAR (un solo POST, Regla 1) | Al EDITAR |
|---|---|---|---|
| Cátedra | sesiones (#1) | `sp_guardar_catedra` | `POST /api/Sesion` por fila |
| Sesión | enlace de registro (#2) y registros de asistencia (#3) | enlace: `sp_guardar_sesion`. Registros: no, la asistencia ocurre cuando la sesión ya existe | `POST /api/EnlaceRegistro` y `POST /api/RegistroAsistencia` por fila (la persona se busca en `/admin/asistentes/buscar`) |
| Asistente | documentos (#4, varios) y consentimiento (#5) | `sp_guardar_asistente` (v3) | `POST /api/DocumentoAsistente` por fila y `POST /api/ConsentimientoDatos` |
| Encuesta | preguntas (#6), cada una con sus opciones (#7) | `sp_guardar_encuesta` | `POST /api/Pregunta` por fila (que guarda la pregunta con SUS opciones: `sp_guardar_pregunta`) |
| Pregunta | opciones (#7) | `sp_guardar_pregunta` (v6) | `POST /api/OpcionPregunta` por fila |
| Programa | alias (#10) | `sp_guardar_programa_academico` (v2) | `POST /api/AliasPrograma` por fila |

- **Al crear**, maestro y filas viajan en el MISMO POST y se guardan en UNA
  transacción: si una fila falla, no se guarda nada y el formulario vuelve
  con todo lo escrito.
- **Al editar**, el maestro ya existe: cada fila nueva es su propia
  petición (el caso "No aplica" de la Regla 1). Las que fallan vuelven al
  formulario con su motivo; las demás ya quedaron. Los detalles que ya
  existían se muestran (solo lectura) y se editan o borran en la pantalla
  del maestro.
- El `orden` de preguntas y opciones lo pone la posición de la fila (al
  editar, sigue después del último que ya existe): nada que numerar a mano.
- **Sin formulario de detalle, a propósito**: #8 (registro → respuesta) y
  #9 (respuesta → ítems). La respuesta la da el propio asistente
  ("responder", ADR D31) y es un hecho auditable (D32): el administrador
  solo la consulta.

- Dos detalles que la API **no filtra por su maestro** se filtran en el
  front, porque son tablas pequeñas: `AliasPrograma` (no tiene
  `?fkPrograma=`) y `RespuestaEncuesta` (solo `?fkEncuesta=`, no
  `?fkRegistro=`; mismo criterio que `_ya_respondida` de la v6). Si
  crecieran, el arreglo correcto es agregar ese filtro al procedimiento de
  listado y a la API, no paginar en Python.

## Estado de cumplimiento (revisado 2026-10-11 al construir la V5; V6 y V7 agregadas el 2026-10-15, al cerrar la V7)

| Regla | V1-V7 | Acción |
|---|---|---|
| 1. SP maestro-detalle | V2: `sp_guardar_programa_academico` (ADR D14). V3: `sp_guardar_asistente` y `sp_guardar_lote_carga` (Fase 22 de `Versiones/version3/8_tasks.md`). **V4: no aplica** — `sesion` y `ponencia` se crean cada una por su cuenta, en peticiones distintas (es el caso "No aplica" de arriba); no hay migración nueva. **V5: no aplica** — ninguna de sus 5 tablas se guarda con SP maestro-detalle (enlaces y claves usan las funciones `fn_*_api`, ADR D24). **V6:** `sp_guardar_pregunta` (pregunta + opciones iniciales, ADR D30). **V7: cumple sin SP nuevo** — generar un lote con sus filas lo resuelve `sp_generar_lote_migracion`, provisto por el script; `sp_crear_lote_migracion` (`031`) solo lo llama (ADR D34) | Ninguna |
| 2. FK = select | Cumple en todos los formularios de admin. En V3, el `fk_asistente` de los detalles (14.811 personas: un `<select>` no es viable) no se digita: lo fija la URL de la ficha del asistente. En V4: `fk_catedra` de una sesión lo fija la URL (`?fk_catedra=`, se entra desde la cátedra); `fk_asistente` de una ponencia sale de un `<select>` llenado con una búsqueda (`?q=`); modalidad, sede, periodo y encuesta, de `<select>` con el catálogo. En V5: el `fk_asistente` de una cuenta y de un registro manual se busca (`?q=`) y se elige de un `<select>`; el `fk_sesion` de enlaces y registros lo fija la URL (se entra desde la lista de sesiones); `fk_usuario` y `fk_rol` salen de `<select>`. En V6: encuesta y tipo de pregunta, de `<select>` con el catálogo. En V7: el estado de un lote sale de un `<select>` con `GET /api/EstadoProceso`; resultado, tipo de dato y tabla de la bitácora, de `<select>` de lista cerrada; `fk_sesion` del lote lo fija la URL (se genera desde el detalle de la sesión) y `fk_usuario`/`fk_usuario_modifica` los fija la sesión del administrador | Ninguna |
| 3. BCrypt | V1-V4: no aplicaba (no había tabla con contraseña). **V5: cumple** — `BCrypt.HashPassword` y `Verify` solo en `ServicioUsuario` (un único `Hashear`); el repositorio trata `clave_hash` como cadena opaca y ninguna respuesta lo expone (ADR D23); la prueba de capas verifica el hash con `BCrypt.Verify` real. V6 y V7: no aplica (ninguna credencial nueva) | Ninguna |
| 4. Maestro → sus detalles (front) | Adoptada el 2026-10-15, después de la V7. Las 10 parejas tienen pantalla: 3 ya la tenían (#4 y #5 en `asistentes_detalle`, v3; #9 en `respuestas_detalle`, v6) y las otras 7 se resolvieron con 6 pantallas nuevas (`catedras_detalle`, `sesiones_detalle` —#2 y #3—, `encuestas_detalle`, `preguntas_detalle`, `registros_detalle`, `programas_detalle`). Además, el formulario de cada maestro agrega sus detalles: al crear, en un solo CALL (migraciones 042-044 + los SP de v2, v3 y v6); al editar, una petición por fila | Ninguna |

**Subversión v7.1 (2026-10-29)** — sobre las reglas:
- *Regla 1:* no aplica a nada nuevo (`sp_validar_clave_y_registrar_geo` y los de
  recuperación no guardan maestro-detalle); la semilla de la `048` sí usa
  `sp_guardar_asistente` cuando crea a la persona.
- *Regla 2:* cumple; `ESTUDIANTE` y `PONENTE` aparecen solos en el `<select>`
  de roles de `/admin/roles-usuario`.
- *Regla 3 (BCrypt):* cumple. El hash de cambiar y recuperar la clave se calcula
  solo en `ServicioUsuario` (se amplió, sigue siendo el único archivo que llama
  a BCrypt). **Única excepción:** la `048` siembra la clave temporal del
  estudiante con `crypt(..., gen_salt('bf'))` de pgcrypto porque es un script SQL
  sin API, igual que el administrador sembrado del script provisto; BCrypt.Net
  lo verifica (mismo algoritmo, prefijo `$2a$`).
- *Regla 4:* sin cambios; el panel de estudiante no es un maestro-detalle de
  `/admin`.

Detalle y decisiones: [Versiones/version7_1/v7_1_subversion.md](Versiones/version7_1/v7_1_subversion.md).

Guía específica de la V4 (orden de lectura, prompts por fase, trampas):
[Versiones/version4/guia_ia_v4.md](Versiones/version4/guia_ia_v4.md).

Guía específica de la V5 (BCrypt, funciones SQL, login y autorregistro):
[Versiones/version5/guia_ia_v5.md](Versiones/version5/guia_ia_v5.md).

Guía específica de la V7 (migración a ASIS, bitácora y parámetros; la
versión que llegó con su código ya escrito por el retrofit):
[Versiones/version7/guia_ia_v7.md](Versiones/version7/guia_ia_v7.md).
