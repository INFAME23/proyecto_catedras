# Retrofit SP — lote Fases 5-6: ajustes al patrón de `sede`

> Lote: **Usuario, RolPorUsuario, EnlaceRegistro, ClaveAcceso,
> RegistroAsistencia, Pregunta, OpcionPregunta, RespuestaEncuesta,
> RespuestaItem**. Molde: `005_sp_sede_ejemplo.sql` +
> `RepositorioSedePostgres.cs`. Plan general: `PLAN_RETROFIT_SP.md`.
>
> Este documento es el registro de "qué tuve que corregir o ajustar del
> patrón de sede" que pide el entregable de prompts (§11 de VERSION_2.md).
>
> **Nota de fusión (2026-10-14).** Este lote se fusionó con la rama que ya
> traía los lotes de `AJUSTES_PATRON_SEDE_LOTE.md` (`006`-`014`), la Fase 2
> (`015`-`023`) y la Fase 4 (`024`-`032`, ver `RETROFIT_FASE4_NOTAS.md`).
> Para no chocar, las migraciones de este lote pasaron de `006`-`014` a
> **`033`-`041`** (mismo contenido, mismo orden relativo). Donde hubo
> conflicto con la otra rama se conservó la versión de esa rama.

---

## 1. Qué se entregó

| Migración | Tabla | Procedimientos | Repositorio |
|---|---|---|---|
| `033_sp_usuario.sql` | usuario | listar, consultar, crear, actualizar, eliminar (**lógico**) + `consultar_usuario_por_login`, `registrar_acceso_usuario` | 7/7 métodos por SP |
| `034_sp_rol_por_usuario.sql` | rol_por_usuario | listar, consultar, crear, actualizar, eliminar (físico) + `listar_rol_vigente_usuario`, `cerrar_rol_por_usuario` | 4/4 métodos por SP |
| `035_sp_enlace_registro.sql` | enlace_registro | listar, consultar, `revocar_enlace_registro` (emitir = `fn_emitir_enlace_api`, sin cambios) | 4/4 (Emitir sigue en su función) |
| `036_sp_clave_acceso.sql` | clave_acceso | listar, consultar (solicitar/validar = `fn_solicitar_clave_api` / `fn_validar_clave_api`, sin cambios) | 4/4 (Solicitar/Validar siguen en sus funciones) |
| `037_sp_registro_asistencia.sql` | registro_asistencia | listar, consultar, crear, actualizar*, eliminar* (físico) | 3/3 métodos por SP |
| `038_sp_pregunta.sql` | pregunta | listar, consultar, actualizar, eliminar (físico) (crear = `sp_guardar_pregunta`, migración 004, sin cambios) | 5/5 |
| `039_sp_opcion_pregunta.sql` | opcion_pregunta | listar, consultar, crear, actualizar, eliminar (físico) | 5/5 |
| `040_sp_respuesta_encuesta.sql` | respuesta_encuesta | listar, consultar, actualizar*, eliminar* (físico) (crear = `fn_responder_encuesta_api` → `sp_responder_encuesta`, sin cambios) | 3/3 |
| `041_sp_respuesta_item.sql` | respuesta_item | listar, consultar, crear*, actualizar*, eliminar* (físico) | 2/2 |

`*` = existe en la BD para cumplir el criterio al pie de la letra, **no está
expuesto** (ni en la interfaz ni en el controlador) y trae una guarda que
protege el hecho auditable (ver §3).

Montadas en `docker-compose.yml` como `z033_` … `z041_`. Nuevo archivo de
apoyo: `api_catedras/Repositorios/LectorJsonSp.cs` (ver ajuste A1).

**No cambió ni una línea** de `IRepositorio*.cs`, `Servicio*.cs`,
`Controllers/*.cs` ni `Peticiones/*.cs` (regla 1).

> ✅ **Numeración (resuelta en la fusión):** este lote tomó originalmente
> **006 a 014**, que ya usaban otros lotes. Se renombraron a **033 a 041**
> (`z033_` … `z041_` en el compose): los procedimientos son independientes
> entre sí y todos son `CREATE OR REPLACE`, así que no cambió su contenido.

---

## 2. Ajustes al patrón de `sede` (lo que no encajó tal cual)

### A. Ajustes generales (aplican a todo el lote, y probablemente a las demás fases)

**A1. Fechas `timestamptz` → hay que normalizarlas a UTC al leer el JSON.**
`sede` no tiene columnas de fecha y hora, así que su
`JsonSerializer.Deserialize` directo no lo notó. `row_to_json` escribe las
fechas con el desfase de la sesión (`"…-05:00"` o `"…+00:00"`) y
`JsonSerializer` las convierte a `DateTime` con `Kind=Local`. Antes,
Dapper/Npgsql las daba con `Kind=Utc` y la API respondía `"…Z"`, que es el
contrato del ADR D21 (v4) y lo que espera `front_catedras/servicios/fechas.py`.
→ Nuevo `Repositorios/LectorJsonSp.cs`: el mismo `Deserialize` de sede, con
dos convertidores (`DateTime` y `DateTimeOffset`) que devuelven UTC. Así la
respuesta de la API queda igual que antes. **Recomendación para las demás
fases:** usar `LectorJsonSp` en vez de `JsonSerializer` directo en cualquier
tabla con fechas y horas (Sesion, Asistente, LoteCarga, etc.).

**A2. `DateOnly` en un `CALL` necesita `CAST(@x AS date)`.**
`ManejadorTipoDateOnly` manda un `DateOnly` como `DateTime` (→ `timestamp`).
En un `INSERT`/`UPDATE` PostgreSQL lo convierte solo (cast de asignación),
pero **un `CALL` no aplica casts de asignación**: sin el `CAST` responde
`procedure catedras.sp_…(bigint, text, timestamp without time zone, unknown)
does not exist`. Verificado. Aplica a `rol_por_usuario` en este lote y a
cualquier tabla con columnas `date` en las demás fases
(PeriodoAcademico, VinculacionAsistente, ProgramaAsistente…).

**A3. Filtros opcionales (`long?`) se mandan con `::bigint`.**
`sede` no tiene filtros en el listar. Varias tablas de este lote sí
(`fk_usuario`, `fk_sesion`, `fk_asistente`, `fk_encuesta`…). En el SP el
filtro es `p_x IS NULL OR col = p_x`; en el `CALL` se escribe
`@fkSesion::bigint`, el mismo recurso que ya usaba el SQL a mano.

**A4. Ids generados por la BD → `v_id` + `RETURNING` en el `sp_crear_`.**
En `sede` la PK la manda el cliente. Aquí casi todas son `IDENTITY`: el
`sp_crear_` hace `RETURNING id INTO v_id` y devuelve la fila con ese id; el
repositorio lee el id del JSON (`CrearAsync` de OpcionPregunta, Usuario).

**A5. Parámetros con `DEFAULT` van al final.** PostgreSQL lo exige. En
`sp_crear_opcion_pregunta` el orden queda `(fk_pregunta, etiqueta, orden,
valor DEFAULT NULL)`, distinto del orden de las columnas.

**A6. Columnas `inet` y `tstzrange` se aplanan dentro del SP.** `host(ip)`
para que llegue `"10.0.0.1"` (y no `"10.0.0.1/32"`); `lower(ventana) AS
ventana_desde, upper(ventana) AS ventana_hasta`. Antes lo hacía el SELECT
de C#; ahora vive en la proyección del procedimiento.

**A7. Métodos de la interfaz que no son ninguno de los 5 verbos → SP
propio.** `ObtenerPorLoginAsync`, `ActualizarUltimoAccesoAsync`,
`ObtenerVigentesAsync`, `CerrarAsync`, `RevocarAsync`. Si no, el repositorio
habría quedado con SQL a mano. Se les dio nombre `sp_<verbo>_<tabla>`.

**A8. Verbos de negocio que ya pasaban por función/SP NO se envolvieron
otra vez.** `fn_emitir_enlace_api`, `fn_solicitar_clave_api`,
`fn_validar_clave_api`, `fn_responder_encuesta_api` y `sp_guardar_pregunta`
ya cumplen el criterio para ese verbo (Grupo C del plan). Crear un
`sp_crear_enlace_registro` que solo llamara a la función habría duplicado
la regla.

### B. Ajustes por tabla

**Usuario — la clave (cuidado BCrypt).**
- **B1. `clave_hash` no sale en ningún `p_resultado` público.** listar,
  consultar, crear y actualizar proyectan todas las columnas **menos**
  `clave_hash`. El único que lo trae es `sp_consultar_usuario_por_login`,
  de uso interno para el `BCrypt.Verify` del servicio (el modelo lo marca
  `[JsonIgnore]`, así que nunca sale en una respuesta de la API).
- **B2. `[JsonIgnore]` también bloquea la LECTURA.** `JsonSerializer` no
  llena `ClaveHash` al deserializar. Para el login, el repositorio lo saca
  del JSON aparte (`LectorJsonSp.Texto(json, "clave_hash")`) y lo asigna a
  mano. Sin esto el login fallaba siempre (sede no tiene campos ignorados).
- **B3. `sp_actualizar_usuario` recibe todas las columnas, pero
  `p_clave_hash NULL` = conservar.** El patrón de sede es "consultar +
  mezclar + llamar completo"; aquí no se puede mezclar el hash porque
  `sp_consultar_usuario` ya no lo devuelve (B1), y traerlo a C# solo para
  devolverlo igual sería sacarlo de la BD sin necesidad. Como `clave_hash`
  es `NOT NULL`, `NULL` nunca es un valor válido: no hay ambigüedad. El
  repositorio sigue sin hashear nada: manda lo que el servicio puso en
  `datos["clave_hash"]` (ya hasheado con BCrypt) o `NULL` si no llegó.
- **B4. Eliminar pasa a ser LÓGICO** (la tabla tiene `activo`). Antes era
  `DELETE` con `CASCADE` a `rol_por_usuario`. Ahora la cuenta queda
  inactiva, sus roles se conservan y el login la rechaza (ya lo hacía
  `ServicioUsuario`, ADR D27).

**RolPorUsuario.**
- **B5. Llave compuesta** (`fk_usuario, fk_rol, fecha_ini`) en consultar,
  actualizar, cerrar y eliminar.
- **B6. `CerrarAsync` usa `sp_cerrar_` y no `sp_actualizar_`** para
  conservar la guarda `fecha_fin IS NULL` (no cerrar dos veces). Mismo
  criterio que `sp_revocar_enlace_registro`.

**EnlaceRegistro / ClaveAcceso.**
- **B7. Sin `sp_actualizar_`/`sp_eliminar_` genéricos** (siguiendo el
  cuidado del lote: "solo agrégales Listar/Consultar"). En EnlaceRegistro la
  **revocación es la baja lógica** (`revocado_en` hace de `activo`), y un
  UPDATE libre de token/url/ventana rompería RN-14/RN-15. En ClaveAcceso la
  fila guarda el hash de una clave de un solo uso: editarla desarma el
  control de intentos y expiración, y no hace falta borrarla porque expira.
- **B8. Revocar devuelve la fila** (con `revocado_en`) en vez de
  `{"eliminado": …}`; `NULL` = no existe o ya revocado → el servicio sigue
  respondiendo igual que con "0 filas".

**Pregunta / OpcionPregunta.**
- **B9. `fk_encuesta` / `fk_pregunta` no entran en `sp_actualizar_`.** La
  lista blanca del repositorio anterior tampoco los dejaba cambiar (mover
  una opción de pregunta rompería RN-24 en respuestas ya dadas).

---

## 3. Hechos auditables — lo que **NO** se expuso (requiere tu decisión)

Las cuatro decisiones siguientes son de negocio. Los SP existen (criterio
cumplido en la BD) pero ninguno está en la interfaz ni en el controlador, y
la regla 1 impide agregarlos sin tu visto bueno.

| Tabla | SP no expuestos | Guarda dentro del SP | Mi recomendación |
|---|---|---|---|
| **RespuestaEncuesta** | actualizar, eliminar | `RAISE` si `completa = true` (D31). En el flujo normal **toda** respuesta queda cerrada en el mismo CALL que la crea, así que en la práctica nunca se pueden usar. | **No exponer.** Editar una respuesta entregada rompe "encuesta cerrada". |
| **RespuestaItem** | crear, actualizar, eliminar | `RAISE` si la respuesta madre está cerrada. `trg_respuesta_item_tipo` sigue validando. | **No exponer.** Igual que arriba. |
| **RegistroAsistencia** | actualizar | Recibe todas las columnas (molde) pero `RAISE` si cambia algo del **hecho**: sesión, asistente, `registrado_en` o las instantáneas ("NUNCA se actualizan"). Solo deja corregir trazabilidad (`fk_enlace`, `fk_clave`, `origen`, `ip`, `user_agent`). Además `trg_registro_validar` es `BEFORE INSERT`: un UPDATE libre se saltaría ventana, cupo y RN-07/RN-22. | **No exponer** por ahora. No veo un caso de negocio para corregir trazabilidad desde la API. |
| **RegistroAsistencia** | eliminar | `RAISE` si el registro ya tiene respuesta de encuesta (`fk_respenc_registro` es CASCADE: borraría una encuesta cerrada en silencio). Si ya se exportó al ASIS lo frena `fk_detmig_registro` (RESTRICT). `trg_bitacora_registro` deja copia en `bitacora`. | **Discutible.** Tiene un caso real (anular un registro MANUAL hecho por error, antes de exportar y sin encuesta). Si lo quieren, sería un `DELETE /api/RegistroAsistencia/{id}` nuevo → toca interfaz + servicio + controlador: avísenme y lo hago. |
| **RolPorUsuario** | consultar, actualizar, eliminar | — | **No exponer eliminar** (borra historia; lo correcto es cerrar). Consultar por llave compuesta no hace falta: ya hay listar con filtro. |

---

## 4. Cambios de comportamiento visibles (revisar con el front)

1. **`GET /api/Usuario` ya no lista los inactivos** (mismo criterio que
   `sede`: solo activos). `GET /api/Usuario/{id}` sí los sigue mostrando.
   ⚠️ La pantalla `admin/usuario_lista.html` tiene un badge "Inactivo" que
   ya no se verá nunca, y no habrá forma de encontrar un usuario inactivo
   desde la lista para reactivarlo. **Decisión pendiente:** o se acepta (es
   el molde), o `sp_listar_usuario` lista todos (desviación del molde, una
   línea).
2. **`DELETE /api/Usuario/{id}` ya no falla por roles**: es lógico. El
   texto del `confirm()` del front ("Si tiene roles asignados, la base de
   datos no lo permitirá") queda desactualizado. Un segundo DELETE responde
   404 (igual que sede).
3. Un usuario borrado lógicamente **sigue ocupando** su `usuario` y su
   `fk_asistente` (`uq_usuario_login`, `uq_usuario_persona`): para "volver a
   crearlo" hay que reactivarlo con PATCH `activo=true`.

Todo lo demás responde igual que antes (mismos códigos, mismo JSON, fechas
en UTC con `Z`).

---

## 5. Cómo se verificó (y qué falta)

En el entorno donde se hizo este lote **no había acceso a Docker Hub ni a
NuGet**, así que no se pudo correr `docker compose up` ni compilar la API
completa. Lo que sí se hizo:

- PostgreSQL 16 local + `script_bdcatedras.sql` real + migraciones 001-014 (hoy 001-005 + 033-041),
  en el **mismo orden** que `docker-entrypoint-initdb.d` (simulado desde
  `docker compose config`). Todas cargan; son idempotentes (2 corridas).
- Cada SP probado con los datos de ejemplo del script, incluidos los casos
  límite: no encontrado, doble eliminar/cerrar/revocar, duplicados, RESTRICT,
  y que **todas las guardas** de §3 disparan.
- .NET 10 SDK: compilan **los modelos, las interfaces, los 9 repositorios y
  sus 9 servicios reales** (0 errores, 0 advertencias) contra
  sustitutos mínimos de Dapper/Npgsql/BCrypt que ejecutan el SQL en el
  PostgreSQL local tipando cada parámetro como lo hace Npgsql. Ningún
  repositorio del lote usa ya `QueryAsync`/`QuerySingleOrDefaultAsync`
  (el sustituto ni los define). **60/60 pruebas extremo a extremo**
  (servicio → repositorio → SP): login con BCrypt, PATCH que conserva el
  hash, PUT que lo cambia, DELETE lógico, fechas en UTC, `DateOnly`, etc.

### Pendiente: tu prueba en Docker real

Si ya tienes el volumen creado, **las migraciones nuevas no corren solas**
(initdb solo se ejecuta con el volumen vacío). Dos opciones:

```bash
# Opción A — aplicarlas sobre la BD existente (conserva tus datos):
docker compose up -d --build
docker cp db/postgres/migraciones mi_v1_catedras_postgres:/tmp/migraciones
docker exec mi_v1_catedras_postgres sh -c 'for f in /tmp/migraciones/0*.sql; do psql -v ON_ERROR_STOP=1 -U sa -d bdcatedras_postgres_local -f "$f" || exit 1; done'

# Opción B — empezar de cero (BORRA los datos que hayas agregado):
docker compose down -v
docker compose up -d --build
```

Luego, en Swagger (`http://localhost:8142/swagger`):

- [ ] `GET /api/Usuario`, `GET /api/Usuario/1` → sin `clave_hash`, fechas con `Z`.
- [ ] `POST /api/Usuario/login` con el admin sembrado → 200 + roles.
- [ ] `POST /api/Usuario` → `PATCH` solo `usuario` → login con la misma clave sigue entrando.
- [ ] `PATCH` con `clave` nueva → la nueva entra, la vieja no. `PUT` igual.
- [ ] `DELETE /api/Usuario/{id}` → 2xx; `GET` lo muestra con `activo:false`; segundo `DELETE` → 404; login → 401.
- [ ] `GET /api/RolPorUsuario?fk_usuario=1`, `POST`, `PATCH /cerrar` (dos veces: la segunda falla).
- [ ] `GET /api/EnlaceRegistro`, `POST` (emitir), `PATCH /{id}/revocar` (dos veces).
- [ ] `GET /api/ClaveAcceso`, `GET /api/ClaveAcceso/1` → sin `clave_hash`.
- [ ] `GET /api/RegistroAsistencia?fk_sesion=5712`, `GET /api/RegistroAsistencia/1`, `POST` MANUAL.
- [ ] `Pregunta` y `OpcionPregunta`: los 5 verbos; `PATCH` parcial conserva lo no enviado; `DELETE /api/Pregunta/1` → falla (tiene respuestas).
- [ ] `GET /api/RespuestaEncuesta`, `GET /api/RespuestaItem?fk_respuesta=1`, `POST /api/RespuestaEncuesta` (responder).
- [ ] Correr `api_catedras/pruebas` (prueba de capas) — no debería cambiar nada: usa repositorios falsos.
