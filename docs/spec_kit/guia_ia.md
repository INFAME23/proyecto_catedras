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
con `CALL` (o `SELECT * FROM` si el SP usa `INOUT`/`RETURNS TABLE`) a
través de Dapper — nunca con dos `INSERT` sueltos desde C#.

**No aplica** cuando el detalle se crea en un momento distinto al maestro,
por su cuenta (ej. agregar un alias a un programa ya existente semanas
después): ahí cada tabla conserva su propio CRUD normal, porque no hay
"una sola petición" que unir.

Patrón de nombre: `sp_guardar_<maestro>` — parámetros del maestro +
un parámetro `jsonb` con el arreglo de detalles (mismo estilo que los SP
que ya trae la BD, ej. `sp_responder_encuesta`).

**Maestro-detalle identificados en el proyecto:**
| Maestro | Detalle(s) | Versión |
|---|---|---|
| `programa_academico` | `alias_programa` | V2 (retrofit hecho: `sp_guardar_programa_academico`) |
| `asistente` | `documento_asistente`, `vinculacion_asistente`, `programa_asistente`, `consentimiento_datos` | V3 |
| `lote_carga_asistente` | `novedad_carga` | V3 |

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

## Estado de cumplimiento (revisado 2026-10-07)

| Regla | V1/V2 actuales | Acción |
|---|---|---|
| 1. SP maestro-detalle | `programa_academico`/`alias_programa`: **retrofit hecho** con `sp_guardar_programa_academico` (2026-10-07) | Ninguna — ver Fase 20b de `Versiones/version2/8_tasks.md` y ADR D14 |
| 2. FK = select | Ya cumple en todos los formularios de admin | Ninguna |
| 3. BCrypt | No aplica todavía (no hay tabla con contraseña hasta `usuario`, V5) | Queda registrada para cuando se escriba V5 |
