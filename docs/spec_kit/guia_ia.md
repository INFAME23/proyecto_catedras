# Guía operativa para la IA

> Este documento **no es la constitución** (`1_constitution.md`) y no la
> reemplaza ni la modifica. Son reglas técnicas transversales que se suman
> a las ya existentes y rigen TODAS las versiones a partir de su adopción
> (ver tabla de estado al final). Ante conflicto con la constitución,
> la constitución gana; ante conflicto con una spec de versión, esta guía
> gana porque es más específica en estos tres puntos.

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

## Estado de cumplimiento (revisado 2026-10-11 al construir la V5; V6 y V7 agregadas el 2026-10-15, al cerrar la V7)

| Regla | V1-V7 | Acción |
|---|---|---|
| 1. SP maestro-detalle | V2: `sp_guardar_programa_academico` (ADR D14). V3: `sp_guardar_asistente` y `sp_guardar_lote_carga` (Fase 22 de `Versiones/version3/8_tasks.md`). **V4: no aplica** — `sesion` y `ponencia` se crean cada una por su cuenta, en peticiones distintas (es el caso "No aplica" de arriba); no hay migración nueva. **V5: no aplica** — ninguna de sus 5 tablas se guarda con SP maestro-detalle (enlaces y claves usan las funciones `fn_*_api`, ADR D24). **V6:** `sp_guardar_pregunta` (pregunta + opciones iniciales, ADR D30). **V7: cumple sin SP nuevo** — generar un lote con sus filas lo resuelve `sp_generar_lote_migracion`, provisto por el script; `sp_crear_lote_migracion` (`031`) solo lo llama (ADR D34) | Ninguna |
| 2. FK = select | Cumple en todos los formularios de admin. En V3, el `fk_asistente` de los detalles (14.811 personas: un `<select>` no es viable) no se digita: lo fija la URL de la ficha del asistente. En V4: `fk_catedra` de una sesión lo fija la URL (`?fk_catedra=`, se entra desde la cátedra); `fk_asistente` de una ponencia sale de un `<select>` llenado con una búsqueda (`?q=`); modalidad, sede, periodo y encuesta, de `<select>` con el catálogo. En V5: el `fk_asistente` de una cuenta y de un registro manual se busca (`?q=`) y se elige de un `<select>`; el `fk_sesion` de enlaces y registros lo fija la URL (se entra desde la lista de sesiones); `fk_usuario` y `fk_rol` salen de `<select>`. En V6: encuesta y tipo de pregunta, de `<select>` con el catálogo. En V7: el estado de un lote sale de un `<select>` con `GET /api/EstadoProceso`; resultado, tipo de dato y tabla de la bitácora, de `<select>` de lista cerrada; `fk_sesion` del lote lo fija la URL (se genera desde el detalle de la sesión) y `fk_usuario`/`fk_usuario_modifica` los fija la sesión del administrador | Ninguna |
| 3. BCrypt | V1-V4: no aplicaba (no había tabla con contraseña). **V5: cumple** — `BCrypt.HashPassword` y `Verify` solo en `ServicioUsuario` (un único `Hashear`); el repositorio trata `clave_hash` como cadena opaca y ninguna respuesta lo expone (ADR D23); la prueba de capas verifica el hash con `BCrypt.Verify` real. V6 y V7: no aplica (ninguna credencial nueva) | Ninguna |

Guía específica de la V4 (orden de lectura, prompts por fase, trampas):
[Versiones/version4/guia_ia_v4.md](Versiones/version4/guia_ia_v4.md).

Guía específica de la V5 (BCrypt, funciones SQL, login y autorregistro):
[Versiones/version5/guia_ia_v5.md](Versiones/version5/guia_ia_v5.md).

Guía específica de la V7 (migración a ASIS, bitácora y parámetros; la
versión que llegó con su código ya escrito por el retrofit):
[Versiones/version7/guia_ia_v7.md](Versiones/version7/guia_ia_v7.md).
