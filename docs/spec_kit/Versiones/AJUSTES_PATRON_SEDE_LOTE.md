# Retrofit criterio 3 — lote Facultad · Dependencia · ProgramaAcademico · AliasPrograma · Catedra · Sesion · Ponencia · Parametro · Bitacora

> Ajustes que hubo que hacerle al patrón de `sede`
> (`005_sp_sede_ejemplo.sql` + `RepositorioSedePostgres.cs`) para que
> funcionara en cada tabla. Es el insumo para el entregable de prompts
> (§11 de `VERSION_2.md`).

## 1. Qué se entregó

| Migración | Tabla | Procedimientos |
|---|---|---|
| `006_sp_facultad.sql` | facultad | listar · consultar · crear · actualizar · eliminar |
| `007_sp_dependencia.sql` | dependencia | listar · consultar · crear · actualizar · eliminar |
| `008_sp_programa_academico.sql` | programa_academico | listar · consultar · actualizar · eliminar (el Crear sigue siendo `sp_guardar_programa_academico`, migración 001, sin tocar) |
| `009_sp_alias_programa.sql` | alias_programa | listar · consultar · crear · actualizar · eliminar |
| `010_sp_catedra.sql` | catedra | listar · **contar** · consultar · crear · actualizar · eliminar |
| `011_sp_sesion.sql` | sesion | listar · consultar · crear · actualizar · eliminar |
| `012_sp_ponencia.sql` | ponencia | listar · consultar · crear · actualizar · eliminar |
| `013_sp_parametro.sql` | parametro | listar · consultar · crear · actualizar · eliminar |
| `014_sp_bitacora.sql` | bitacora | listar · consultar · crear · actualizar · eliminar |

Todas montadas en `docker-compose.yml` como `z006_` … `z014_`.

**Qué capas cambiaron.** En la primera ronda solo cambiaron los 7
`Repositorio*Postgres.cs`; `Parametro` y `Bitacora` se crearon enteras. En la
segunda ronda (pedido: *"si un procedimiento no se llama, haz que se llame"*) se
tocaron también interfaces, servicios, controllers y peticiones — ver §6. Resumen:

| Cambio | Tablas |
|---|---|
| **Nuevo verbo expuesto** (el SP no tenía llamador) | Ponencia `PUT` (cambia el rol) · AliasPrograma `PUT`/`PATCH` · Bitacora `POST`/`PUT`/`PATCH`/`DELETE` |
| **Nuevo parámetro `?incluirInactivos=true`** en el listado | Sede · Facultad · Dependencia · ProgramaAcademico · Catedra |
| **Corrección de `sp_crear_sede`** (ignoraba `activo`) | Sede (`005_sp_sede_ejemplo.sql` + `RepositorioSedePostgres.CrearAsync`) |
| **Mensajes del front** que ya no eran ciertos + toggle "Ver inactivas" | `front_catedras` |

## 2. Ajustes al patrón de `sede`, tabla por tabla

### Comunes a varias tablas

| Ajuste | Por qué |
|---|---|
| **`sp_crear_*` recibe `p_activo`** (Facultad, Dependencia, Catedra) | `sp_crear_sede` no lo recibe: una sede creada con `"activo": false` queda activa (ver §4). Estas tablas sí mandan `Activo` desde el servicio. |
| **Fechas normalizadas a UTC en el repositorio** (`ToUniversalTime()`) | `row_to_json` escribe las fechas con el desfase de la zona horaria de la *sesión* de PostgreSQL (`...-05:00`) y `System.Text.Json` las entrega como `DateTime` **Local**. Antes Npgsql las entregaba en UTC. Sin la normalización, la API pasaría de responder `...Z` a responder con el desfase de la máquina. `sede` no tiene columnas de fecha, por eso el patrón no lo necesitaba. |
| **Mezcla del PATCH con `TryGetValue` + valor actual** | Igual que `sede`. Un `null` explícito en un PUT sí borra la columna; un campo ausente en un PATCH se conserva. |

### Facultad · Dependencia
- Idénticas a `sede` (tienen `activo`, clave de texto) salvo el `p_activo` de arriba y la columna `fk_*_padre` (autorreferencia, admite NULL).
- El listado ahora trae **solo activas**; el borrado es lógico. Antes era `DELETE` físico (con `ON DELETE SET NULL` sobre hijas/programas/cátedras): el mensaje del front *"sus hijas y programas quedan sin facultad"* deja de ser cierto.

### ProgramaAcademico
- **No hay `sp_crear_programa_academico`**: el Crear sigue siendo `sp_guardar_programa_academico`. El repositorio conserva su `CrearAsync` tal cual.
- Eliminar pasó de `DELETE` a lógico: la FK `RESTRICT` desde `programa_asistente` ya no bloquea el borrado (el comentario del repositorio original lo documentaba como caso especial).

### AliasPrograma
- **Se agregó `sp_crear_alias_programa`**: el alta *suelta* (`POST /api/AliasPrograma`) no pasaba por ningún SP; `sp_guardar_programa_academico` solo cubre el alta junto con el programa.
- Sin `activo` → **DELETE físico** y listado completo.
- `CrearAsync` devuelve `(IdAlias, CreadoEn)`: salen de la fila JSON que devuelve el SP (antes del `RETURNING`).
- `sp_actualizar_alias_programa` ahora **sí tiene llamador**: `IRepositorioAliasPrograma.ActualizarAsync` (consulta, mezcla el PATCH, llama el SP), `ServicioAliasPrograma.ReemplazarAsync/ActualizarAsync`, peticiones `AliasProgramaReemplazo` / `AliasProgramaActualizar` y `PUT`/`PATCH /api/AliasPrograma/{id}`. Esto **revierte la decisión de la v2 (ADR D12, "tabla solo-anexo")**; se actualizaron los comentarios que la afirmaban. En el PUT, `origen` es opcional y vuelve a `FORMULARIO` si no llega (la columna no admite NULL).

### Catedra
- **Se agregó `sp_contar_catedra`** (6.º procedimiento) para `ContarAsync`, y cuenta **solo activas**, igual que el listado: si no, el `total` de la respuesta no coincidiría con las filas.
- **Columna generada `nombre_asis`**: nunca va en el INSERT ni en el UPDATE; sí viaja en el JSON de vuelta (el servicio la devuelve al crear).
- `CrearAsync` lee `id_catedra` y `nombre_asis` de la fila que devuelve el SP (antes, `RETURNING`).
- Eliminar pasó a lógico: ya no cae en cascada a las sesiones (`fk_sesion_catedra`).

### Sesion
- **Trigger `trg_sesion_consecutivo`**: `sp_crear_sesion` recoge `id_sesion` con `RETURNING` y **relee la fila después del INSERT**; así el JSON trae el `numero_reunion` que calculó el trigger (probado: sin número → 1, 2; con número explícito → se respeta).
- **Orden de parámetros de `sp_crear_sesion`**: los 6 obligatorios primero y los opcionales con `DEFAULT` después (PostgreSQL exige que todo `IN` posterior a uno con default también lo tenga).
- **`inicio`/`fin` viajan como `DateTimeOffset`** (siempre `timestamptz`), no como `DateTime`: así el instante no depende de la zona horaria de la sesión de PostgreSQL ni del `Kind` del `DateTime`.
- **Merge del PATCH con `Convert.ToInt64/Int16/Int32`** para `fk_catedra`, `numero_reunion`, `numero_reunion_asis`, `fk_encuesta`, `cupo`: el diccionario trae `long`, `short` o `int` según la petición, y el cast directo `(long)` de `sede` revienta si el tipo en la caja no es exactamente ese. Además, un procedimiento exige el tipo exacto del parámetro (`int4 → int2` no es una conversión implícita en `CALL`).
- Sin `activo` → DELETE físico; las FK `RESTRICT` (`fk_registro_sesion`, `fk_lotemig_sesion`) siguen bloqueándolo con `NpgsqlException` (→ 500, como antes).
- `ServicioSesion` sigue haciendo su propia consulta para validar `fin > inicio` en el PATCH; el repositorio consulta otra vez para mezclar. Son dos lecturas por PATCH; es el costo de no tocar el servicio.

### Ponencia (llave compuesta)
- `consultar`, `actualizar` y `eliminar` reciben `(fk_sesion, fk_asistente, rol)`.
- `listar` recibe los dos filtros opcionales (`fk_sesion`, `fk_asistente`) + límite.
- **`sp_actualizar_ponencia` recibe además `p_rol_nuevo`**: las 3 columnas de la tabla *son* la llave, así que "reemplazar todas las columnas" con los mismos 3 valores no cambia nada. Lo único con sentido es cambiar el rol. Ahora tiene llamador: `IRepositorioPonencia.ActualizarAsync(fkSesion, fkAsistente, rol, rolNuevo)`, `ServicioPonencia.ReemplazarAsync`, petición `PonenciaReemplazo { rol }` y `PUT /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` (404 si la llave vieja no existe; 500 `error_motor` si el rol nuevo ya existe para ese asistente en esa sesión). No hay PATCH: no queda otro campo. **Decisión del equipo a confirmar** (revierte ADR D19, "sin actualizar").
- Sin `activo` → DELETE físico.

### Parametro (capa completa nueva)
- PK `clave` (varchar 60). Sin `activo` → DELETE físico.
- `sp_actualizar_parametro` **refresca `modificado_en = now()`**; la clave no se cambia.
- `valor` permite cadena vacía (`[Required(AllowEmptyStrings = true)]`); `tipo_dato` se valida contra los 5 valores del `CHECK`.
- **No se validó que `valor` sea coherente con `tipo_dato`** (p. ej. `ENTERO` con `"abc"`): la BD no lo exige y los 9 valores sembrados siguen formatos distintos. Si se quiere, va en `ServicioParametro`.
- `fk_usuario_modifica` es opcional en las peticiones (la API aún no tiene sesión de usuario que lo llene sola).

### Bitacora (capa nueva, lectura **y** escritura)
- Los **5 procedimientos** tienen llamador: `GET` lista/uno, `POST`, `PUT`, `PATCH`, `DELETE /api/Bitacora`. (En la primera ronda se dejó solo lectura a propósito; se abrió por el pedido de que todos los CRUD llamen a su SP.)
- `sp_crear_bitacora` **no recibe** `ocurrido_en` ni `usuario_bd` (los pone la BD) y `sp_actualizar_bitacora` **no los toca**. Probado: un POST con `"ocurrido_en": "2000-01-01..."` y `"usuario_bd": "falso"` los ignora; quedan la fecha actual y el usuario real de la conexión.
- `datos_antes` / `datos_despues` (jsonb) viajan como texto JSON con `CAST(@x AS jsonb)`; en las peticiones son `JsonElement?`. PUT reemplaza las 6 columnas de negocio (lo que no llega queda NULL; `nombre_tabla` y `operacion` obligatorias). PATCH solo cambia lo enviado (no puede dejar un campo en NULL: para eso PUT).
- Listado: más recientes primero, con filtro opcional `?nombreTabla=`.
- ⚠️ **Riesgo, hay que decidirlo**: la bitácora es el log de auditoría (RN-35). Con estos endpoints cualquiera que llegue a la API puede **crear entradas falsas, reescribir el antes/después o borrar entradas**. Los triggers la seguían alimentando igual. Antes de producción conviene protegerlos con autorización (o quitar PUT/PATCH/DELETE del controller y dejar solo lectura + POST).

## 3. Cómo aplicar las migraciones

`initdb` solo ejecuta los archivos con el volumen **vacío**:

```bash
docker compose down -v        # borra el volumen (la BD se vuelve a sembrar sola)
docker compose up -d --build
```

Con una BD ya creada y datos que quieras conservar, aplica a mano, en orden:

```bash
for f in 006 007 008 009 010 011 012 013 014; do
  docker exec -i mi_v1_catedras_postgres psql -U sa -d bdcatedras_postgres_local \
    -v ON_ERROR_STOP=1 < db/postgres/migraciones/${f}_*.sql
done
```

## 4. Hallazgos sobre el patrón de `sede`

1. **`sp_crear_sede` ignoraba `activo`.** → **CORREGIDO** (§6): `POST /api/Sede` con `"activo": false` guardaba `activo = true`.
2. **El listado "solo activas" ocultaba filas que el front mostraba** (badge "Activo: Sí/No", "Inactiva" en la vista pública, y los `<select>` de padre/facultad/sede). → **CORREGIDO** (§6) con `?incluirInactivos=true`; por defecto el listado sigue siendo "solo activas", como el ejemplo del profesor.
3. **El borrado lógico cambia errores de FK por éxito silencioso**: eliminar un programa con asistentes, o una cátedra con sesiones, antes fallaba o arrastraba filas; ahora responde 200 y deja todo en su sitio. (Se mantiene; es lo que pide el patrón.) Un segundo `DELETE` sobre una fila ya inactiva responde 404 (el SP no cuenta como retirada una fila que ya lo estaba).

## 5. Cómo se verificó (y qué NO)

Verificado (PostgreSQL 16 local con `script_bdcatedras.sql` + migraciones 001–014; las migraciones 005–014 también se re-aplicaron una segunda vez sin errores y sin dejar procedimientos sobrecargados):
- Los **50 procedimientos** del lote (incluidos `sp_contar_catedra`, `sp_crear_alias_programa` y el `sp_crear_sede` corregido).
- Los controllers, servicios, peticiones y repositorios **reales** se compilaron (0 errores, 0 advertencias) contra stubs mínimos de Npgsql/Dapper que ejecutan el SQL con `psql`, y se probaron por HTTP los 5 verbos de las tablas del lote, y en la segunda ronda: `POST /Sede` con `activo=false`; `incluirInactivos` en Sede/Facultad/Dependencia/Programa/Catedra (incluido que el `total` de Catedra cambie de 5502 a 5503 al pedir inactivas); Alias `PUT`/`PATCH` (200, 400 body vacío, 404, 422, 500 por texto repetido y por FK); Ponencia `PUT` (200, 404, 422, 500 por rol repetido); Bitácora `POST`/`PUT`/`PATCH`/`DELETE` (200, 400, 404, 422, 500 por FK de usuario; `ocurrido_en`/`usuario_bd` no se pueden falsear; jsonb ida y vuelta).
- `pruebas/Programa.cs` (fakes en memoria) se actualizó a las firmas nuevas; compila salvo los errores propios del entorno (faltan BCrypt y ASP.NET en el proyecto auxiliar). No se pudo **ejecutar** (depende de BCrypt).
- Front: `routes/*.py` compilan (`ast`) y las plantillas de lista se parsean con Jinja; no se levantó el front contra la API.

**NO verificado** (el entorno no tiene Docker ni acceso a NuGet):
- `docker compose up -d --build` real y Swagger sobre Npgsql/Dapper reales. Los puntos que dependen del driver real y que `sede` ya asumía sin probar: el `CALL ...(@p, NULL)` con un `INOUT json` leído con `QuerySingleAsync<string?>`, los tipos de los parámetros, y — **nuevo** — `CAST(@x AS jsonb)` con un `string` (Bitácora) y los `bool` de `incluirInactivos`. Hay que confirmarlos en tu máquina.

## 6. Segunda ronda: qué se cambió para que todos los SP se llamen y qué falló

| # | Problema | Arreglo |
|---|---|---|
| 1 | `sp_actualizar_ponencia` sin llamador | `PUT /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}` (cuerpo `{"rol": ...}`) |
| 2 | `sp_actualizar_alias_programa` sin llamador | `PUT`/`PATCH /api/AliasPrograma/{id}` |
| 3 | `sp_crear/actualizar/eliminar_bitacora` sin llamador | `POST`/`PUT`/`PATCH`/`DELETE /api/Bitacora` (ver riesgo en §2) |
| 4 | `sp_crear_sede` ignoraba `activo` | `005_sp_sede_ejemplo.sql`: `p_activo boolean DEFAULT true` (+ `DROP PROCEDURE IF EXISTS` de la firma anterior para no dejar una sobrecarga ambigua); `RepositorioSedePostgres.CrearAsync` manda `@Activo` |
| 5 | Filas inactivas invisibles | `sp_listar_*` de sede/facultad/dependencia/programa_academico/catedra (y `sp_contar_catedra`) reciben `p_incluir_inactivos boolean DEFAULT false`; viaja como `?incluirInactivos=true` (controller → servicio → repositorio). Cambió la firma de `IRepositorio{...}.ObtenerTodosAsync(limite, incluirInactivos = false)` y `IServicio{...}.ListarAsync(limite, incluirInactivos = false)`, por lo que se actualizaron los fakes de `pruebas/Programa.cs` |
| 6 | Front: mensajes falsos tras el borrado lógico ("sus hijas quedan sin facultad", "y sus sesiones, por ON DELETE CASCADE", "Programa eliminado"…) | `front_catedras/routes/admin.py`: ahora dicen "desactivada (borrado lógico…)" |
| 7 | Front: las listas de administración ya no mostraban inactivas | Enlace "Ver también las inactivas" (`?inactivas=1`) en las 5 listas; los `<select>` (`_opciones`) y el catálogo público de cátedras y el índice de programas de la carga masiva piden `incluirInactivos=true` para conservar el comportamiento previo (si no, editar una fila cuyo padre/sede está inactivo habría puesto ese campo en NULL al guardar) |

**Cuidado al aplicar en una BD existente**: cambiaron las firmas de `sp_listar_*`, `sp_contar_catedra` y `sp_crear_sede`. Cada archivo hace `DROP PROCEDURE IF EXISTS` de la firma vieja antes de crear la nueva, así que se pueden re-aplicar tal cual con `psql` sobre una BD ya creada (probado), o recrear el volumen con `docker compose down -v`.
