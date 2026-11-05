# Guía IA — Versión 7: migración a ASIS, bitácora y parámetros

> **Qué es este documento.** La guía operativa para construir, revisar o
> verificar la **v7** con un asistente de IA (Claude, Copilot, ChatGPT…),
> siguiendo el spec kit de esta versión. **No crea reglas nuevas**: aterriza
> a la v7 las que ya existen y señala dónde el borrador del spec kit ya no
> coincide con el repositorio.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> (sus 3 reglas) › spec kit v7 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si esta guía contradice al spec kit, gana el spec kit y se corrige esta
> guía. **Excepción explícita:** donde el §2 de esta guía muestra que el
> borrador contradice la BD real (Artículo 5), gana la BD y se corrige el
> borrador en la Fase 54.
>
> **Estado de partida (revisado el 2026-10-15 sobre el ZIP del repositorio):**
> - **v6:** completa según el README (prueba de capas 385/0, 33 recursos
>   verificados en Docker, front probado a mano). **Tag `v6` creado**
>   (confirmado por el equipo el 2026-10-15), como exige el Artículo 1.
> - **Fase 54 hecha (2026-10-15):** decisiones A-F = todas las
>   recomendadas (ADR D37 de `4_research.md`) y documentos de `version7/`
>   alineados con el código y la BD. Las tablas del §2 y del §7 describen
>   el estado **anterior** a esa fase.
> - **Fases 55-61 hechas (2026-10-15)**, verificadas contra PostgreSQL real
>   y la API real por HTTP; el detalle y la evidencia están en
>   `8_tasks.md`. Falta correr `pruebas/correr_todo_v7.ps1` contra Docker,
>   el criterio 8 a mano, quitar el banner y el tag `v7`.
> - **v7, API:** las capas de las 4 entidades **ya existen** (nacieron
>   durante el retrofit "todos los verbos por procedimiento", criterio 3 de
>   `VERSION_2.md`), con migraciones `013`, `014`, `031` y `032`. **No
>   siguen al pie de la letra el borrador** (ver §2).
> - **v7, prueba de capas:** cubre `LoteMigracion` y `DetalleMigracion`
>   (21 verificaciones); **no cubre** `Parametro` ni `Bitacora`.
> - **v7, front:** **no existe nada** (ni rutas, ni plantillas, ni tarjetas
>   en `/admin`).
> - **Nunca se corrió en Docker** la API de la v7. Los datos del §8 se
>   verificaron cargando `script_bdcatedras.sql` + las 41 migraciones en
>   PostgreSQL 16, en el mismo orden que `docker-entrypoint-initdb.d`, y
>   llamando a los mismos procedimientos que llaman los repositorios. Eso
>   **no** sustituye la verificación por HTTP de las Fases 58-59.

---

## 0. Cómo usar esta guía

1. **Antes de nada**, cierra la v6 (tag `v6`) o confirma con el equipo que
   se trabajará la v7 sin él. No es burocracia: el Artículo 1 dice que la
   spec siguiente se revisa contra lo que *quedó* en la anterior.
2. Abre una conversación nueva con tu asistente, adjunta o da acceso al
   repositorio y pega el **prompt de arranque** (§1).
3. Lee el **§2 (discrepancias)** y el **§3 (decisiones)** antes de la
   Fase 54. Son la diferencia entre esta versión y las anteriores: aquí casi
   todo el código ya existe, y el riesgo no es "no saber construirlo" sino
   **construirlo dos veces o contra un spec desactualizado**.
4. Trabaja **una fase a la vez** (§9, Fases 54-61). Cada una trae un prompt,
   la lista "Terminado cuando…" y los errores típicos.
5. No marques una casilla de `8_tasks.md` hasta que la verificación pase
   **en tu máquina, contra Docker**. La IA escribe; quien verifica eres tú.
6. Anota cada corrección que le hagas a la IA (§12): es el entregable de
   prompts del §11 de `VERSION_2.md`, que todavía no existe en ninguna
   versión.
7. Si quieres reconstruir la v7 de punta a punta en una sola sesión (otra
   máquina, otra rama, otro compañero), usa el **prompt de replicación**
   del §13.

## 1. Prompt de arranque (cópialo tal cual)

```text
Vas a trabajar en el Proyecto Cátedras, VERSIÓN 7 (migración a ASIS,
bitácora y parámetros). Es la ÚLTIMA versión del mapa: al cerrarla, la API
expone las 37 tablas de bdcatedras. Es material de estudio: responde en
español, comenta el porqué en el código y NO inventes resultados de pruebas.

Antes de escribir código, lee EN ESTE ORDEN:
  1. docs/spec_kit/1_constitution.md
  2. docs/spec_kit/guia_ia.md
  3. docs/spec_kit/Versiones/0_mapa_versiones.md
  4. docs/spec_kit/Versiones/PLAN_RETROFIT_SP.md
  5. docs/spec_kit/Versiones/AJUSTES_PATRON_SEDE_LOTE.md   (Parametro, Bitacora)
  6. docs/spec_kit/Versiones/RETROFIT_FASE4_NOTAS.md        (LoteMigracion, DetalleMigracion)
  7. docs/spec_kit/Versiones/version7/2_spec.md → 8_tasks.md (en orden)
  8. docs/spec_kit/Versiones/version7/guia_ia_v7.md (esta guía; §2 y §3 primero)

Después, resume en 8-10 puntos: alcance, entidades, endpoints, qué YA
existe en el código, qué falta, y las discrepancias del §2 de la guía que
confirmes tú mismo en el repositorio. No escribas código hasta que te
confirme las decisiones del §3.

Reglas que no se negocian en la v7:
  - Solo 4 tablas nuevas: lote_migracion, detalle_migracion, bitacora,
    parametro. No refactorices v1-v6 como parte de la v7.
  - C# + ASP.NET Core (.NET 10) + Dapper, sin Entity Framework, sin
    paquetes nuevos. Capas: Controller -> IServicio -> IRepositorio ->
    Repositorio*Postgres. Solo Program.cs conoce clases concretas.
  - Molde vigente del proyecto (retrofit, criterio 3): cada verbo de un
    repositorio es un CALL catedras.sp_<verbo>_<tabla>(..., NULL) que
    devuelve json en un INOUT. Nada de SQL suelto en los repositorios.
  - Generar un lote NO se reimplementa: sp_crear_lote_migracion llama a
    sp_generar_lote_migracion (script provisto). Ese procedimiento crea el
    maestro, los detalles y pone la sesión en MIGRADA, todo en una
    transacción. El servicio no toca Sesion por su cuenta.
  - DetalleMigracion nace solo dentro de "generar": sin POST, PUT ni DELETE.
  - LoteMigracion: sin PUT ni DELETE; PATCH solo de estado/observaciones.
  - Bitacora: lo decide el equipo (§3, decisión A). No cambies sus verbos
    sin esa decisión escrita.
  - JSON de los cuerpos en snake_case con [JsonPropertyName] (fk_sesion,
    fk_estado_proceso), como los 33 recursos anteriores. Query en camelCase
    (?fkSesion=, ?fkLoteMigracion=, ?nombreTabla=).
  - Códigos: 422 forma, 400 regla del servicio o PATCH vacío, 404 no
    existe, 500 todo rechazo de la BD (FK, UNIQUE, RAISE). Sin 409 en esta
    versión salvo decisión explícita del equipo para todo el proyecto.
  - No des por aprobada ninguna fase sin ejecutar su verificación.
Trabajaremos fase por fase (54-61). No avances sin mi confirmación.
```

## 2. El borrador frente al repositorio real

El spec kit de la v7 se escribió **antes** del retrofit a procedimientos.
Estas diferencias se comprobaron leyendo el código y ejecutando los
procedimientos contra la BD real. Ninguna se corrige "en silencio": cada una
se resuelve en la Fase 54 (documento) o en la fase indicada (código).

| # | El borrador dice | El repositorio / la BD dice | Qué hacer |
|---|---|---|---|
| 1 | V7 no agrega SP ni migración (ADR D34) | Existen `013_sp_parametro`, `014_sp_bitacora`, `031_sp_lote_migracion`, `032_sp_detalle_migracion` (5 SP cada una), montadas como `z013`, `z014`, `z031`, `z032` | Reescribir D34: el *maestro-detalle* sigue sin SP nuevo (lo hace `sp_generar_lote_migracion`), pero los verbos sí pasan por SP propios por el criterio 3 |
| 2 | `GenerarAsync` = `SELECT * FROM fn_generar_lote_api(...)` | `CALL catedras.sp_crear_lote_migracion(@fkSesion, @fkUsuario, NULL)`, que **llama** a `sp_generar_lote_migracion` y devuelve la fila completa | Mantener el código; corregir §4.1 de `3_plan.md` y §4 de `6_contracts.md` |
| 3 | Cuerpos en camelCase (`fkSesion`, `fkEstadoProceso`, `tipoDato`) | snake_case (`fk_sesion`, `fk_estado_proceso`, `tipo_dato`), igual que v1-v6 | Decisión B (§3) |
| 4 | `POST /api/LoteMigracion` → `{estado, mensaje, idLote}` | `{estado, mensaje, id_lote_migracion, lote}` | Decisión B (§3) |
| 5 | `Bitacora` sin ningún verbo de escritura (D36, RF191, criterio 5) | `BitacoraController` expone `POST`/`PUT`/`PATCH`/`DELETE` (se abrieron para que los 5 SP "tuvieran llamador") | **Decisión A (§3)** — la más importante de la versión |
| 6 | `GET /api/Bitacora?nombreTabla=&fkUsuario=` | Solo `?nombreTabla=` y `?limite=`; `sp_listar_bitacora` no recibe usuario | Decisión C (§3) |
| 7 | "El único trigger de bitácora es `trg_bitacora_registro`"; `nombre_tabla` siempre es `registro_asistencia` | También existe `trg_bitacora_asistente` (`AFTER UPDATE OR DELETE` sobre `asistente`). Con el borrado lógico del retrofit, **cada `DELETE /api/Asistente/{id}` agrega una fila** `asistente / UPDATE` | Corregir RF190, §2.3 de `5_data_model.md` y `4_research.md` (D36) |
| 8 | RF185: el externo "se cuenta en `total_filas`" | `total_filas` = `count(*) FROM fn_exportar_asis(...)`, que **excluye** externos y a quien no tiene `id_asis` o programa. En 5712: 168 registros, 1 externo, **166** migrables = `total_filas` = filas de detalle | Corregir RF185 |
| 9 | RF197: el **repositorio** fija `modificado_en = now()` | Lo fija `sp_actualizar_parametro` dentro del SP | Corregir RF197 y §4.3 de `3_plan.md` (sigue siendo cierto que no hay trigger) |
| 10 | Verbos inexistentes → **404** de ASP.NET | ASP.NET Core responde **405 Method Not Allowed** cuando la ruta existe con otro verbo (p. ej. `PUT /api/LoteMigracion/1`) | Comprobar en Docker y escribir en el spec lo que salga |
| 11 | Criterio 2: tras generar, `GET /api/Sesion/5712` confirma `MIGRADA` | 5712 **ya está** `MIGRADA` en los datos sembrados: el criterio pasa sin probar nada | Ver §8: poner la sesión en `CERRADA` antes de generar |
| 12 | Quickstart paso 5: "si el mismo `fkRegistro` ya tiene otra fila `ACEPTADA`…" | El lote 1 (sembrado, `CORRECTO`) ya tiene **164 de los 166** registros `ACEPTADO`. Solo los registros **167** y **169** aceptan un `ACEPTADO` nuevo | Ver §8 |
| 13 | RF194: `clave` con `[RegularExpression]`; `tipoDato` obligatorio | `ParametroCrear.Clave` solo tiene `[StringLength(60)]`; `tipo_dato` es opcional (la BD pone `TEXTO`) | Decisión D (§3) |
| 14 | Criterio 7: prueba de capas de 37 entidades | Faltan `Parametro` y `Bitacora` en `pruebas/Programa.cs` | Fase 58 |
| 15 | Criterio 8: 4 tarjetas nuevas en `/admin` + botón en `detalle_sesion.html` | No existe nada del front de v7 | Fase 60 |
| 16 | Diagnóstico de la API | `Program.cs` sigue diciendo `version = "v6"` | Fase 61 |

## 3. Decisiones que toma el equipo (no la IA)

La IA debe **presentarlas y esperar**. Se registran por escrito en
`4_research.md` (nuevo ADR o enmienda) antes de tocar código.

### Decisión A — ¿La API puede escribir en la bitácora?

- **Opción 1 (recomendada): solo lectura en la API**, como dicen D36, el
  Supuesto 3 del mapa de versiones y el criterio 5. Los 5 SP de `014` se
  quedan en la BD (el criterio 3 se cumple en la base de datos) pero solo
  `listar` y `consultar` tienen llamador. Hay precedente en el propio
  proyecto: `RespuestaEncuesta`, `RespuestaItem`, `RegistroAsistencia`,
  `ConsentimientoDatos`, `NovedadCarga` y el `sp_eliminar_lote_migracion`
  tienen SP que existen y **no se exponen** porque protegen un hecho
  auditable. Una bitácora que cualquiera puede reescribir o borrar deja de
  ser una bitácora (el propio comentario de `BitacoraController` lo advierte).
  *Cambios:* quitar los 4 métodos de escritura de `BitacoraController`,
  `IServicioBitacora`/`ServicioBitacora` e `IRepositorioBitacora`/
  `RepositorioBitacoraPostgres`; borrar `BitacoraCrear`, `BitacoraReemplazo`
  y `BitacoraActualizar`; agregar una prueba por reflexión que falle si
  reaparecen; ajustar el comentario de `Program.cs`.
- **Opción 2: dejar los 6 verbos** (decisión del lote de retrofit). Entonces
  hay que **reescribir** D36, RF190-RF191, el criterio 5 y el paso 6 del
  quickstart, y dejar documentado el riesgo (falsificar o borrar auditoría
  sin autorización en la API, ADR D28).

### Decisión B — Nombres del JSON

- **Recomendado:** conservar snake_case en cuerpos y respuestas (lo que ya
  hacen el código y los 33 recursos anteriores; la guía v5 lo fija como
  regla) y **corregir los documentos** de la v7 (`2_spec.md`,
  `6_contracts.md`, `7_quickstart.md`). La respuesta de generar queda
  `{estado, mensaje, id_lote_migracion, lote}`.
- Cambiar el código a camelCase rompería la coherencia con todo el
  proyecto y con el front. No se hace sin una razón escrita.

### Decisión C — Filtro de bitácora por usuario

`fk_usuario` es `NULL` en todas las filas (ningún endpoint propaga el
usuario autenticado a la conexión de PostgreSQL). Un filtro por usuario
siempre devolvería vacío.
- **Recomendado:** quitar `fkUsuario` de RF190 y del front, explicando por
  qué (YAGNI, Artículo 1).
- Alternativa: agregar `p_fk_usuario` a `sp_listar_bitacora` en una
  migración nueva (`042_…`, con `DROP PROCEDURE IF EXISTS` de la firma
  vieja) y el parámetro en las 4 capas.

### Decisión D — Validación de `Parametro`

- `clave`: proponer `[RegularExpression("^[A-Z][A-Z0-9_]*$")]` (coincide
  con las 9 claves sembradas) o retirar esa exigencia de RF194.
- `tipo_dato`: dejarlo opcional (la BD usa `TEXTO`) y corregir RF194, o
  volverlo `[Required]`.
- Coherencia `valor`/`tipo_dato` (p. ej. `ENTERO` con `"abc"`): hoy nadie la
  valida. Si se quiere, va en `ServicioParametro` → 400. Es opcional.

### Decisión E — Dónde vive el front de la v7

`/admin` está protegido por **un** `before_request` del blueprint
`admin_bp` (ADR D28). El borrador propone blueprints nuevos
(`routes/migracion.py`, `bitacora.py`, `parametros.py`), que **no** heredan
esa protección.
- **Recomendado:** bloques nuevos dentro de `routes/admin.py` (como las
  versiones anteriores) y la acción "generar migración" también bajo
  `/admin` (p. ej. `POST /admin/sesiones/<id>/migrar`); en
  `detalle_sesion.html` el botón solo se pinta si el usuario de la sesión
  de Flask tiene rol `ADMIN`.
- Si se eligen blueprints nuevos, cada uno replica la guardia de `ADMIN`.

### Decisión F — `instancia_proceso_asis` en el PATCH del lote

`sp_actualizar_lote_migracion` la recibe, `LoteMigracionActualizar` no la
expone y el borrador no la pide. **Recomendado:** dejarlo así (Artículo 1);
agregarla es una propiedad y una línea en `ACamposEnviados`.

## 4. La v7 en una página

- **Qué agrega:** 4 tablas, RF179-RF197, ADR D34-D36 (D34 enmendado). La API
  pasa de 33 a **37** recursos.
  - `LoteMigracion` — 4 endpoints: `GET` lista (`?fkSesion=`), `GET/{id}`,
    `POST` (= generar), `PATCH/{id}` (estado y observaciones).
  - `DetalleMigracion` — 3: `GET` lista (`?fkLoteMigracion=`), `GET/{id}`,
    `PATCH/{id}` (resultado y mensaje).
  - `Bitacora` — 2 (opción 1) o 6 (opción 2): `GET` lista
    (`?nombreTabla=&limite=`), `GET/{id}`.
  - `Parametro` — 6, llave de texto `{clave}`.
- **Qué NO agrega:** servicios en `docker-compose.yml`, paquetes NuGet ni de
  Python, autorización por rol en la API.
- **Lo que enseña:** reutilizar un procedimiento provisto en vez de
  reescribirlo; un efecto colateral real sobre una tabla de otra versión
  (`sesion.estado`); un índice único **parcial** como regla de negocio
  (`ux_detmig_aceptado`); una entidad cuyo ciclo de vida sigue después de
  crearse (D35); y por qué una bitácora no se edita.

```
sesion (v4) ──< lote_migracion >──┬── estado_proceso (v1)   GENERADO → EN_COLA → EN_CURSO
                     │             └── usuario (v5, opcional)          → CORRECTO | ERROR | INCORRECTO
                     └──< detalle_migracion >── registro_asistencia (v5)
                              resultado: PENDIENTE | ACEPTADO | RECHAZADO
                              ux_detmig_aceptado: un registro ACEPTADO una sola vez, en cualquier lote

bitacora   ← la llenan trg_bitacora_registro y trg_bitacora_asistente (fn_trg_bitacora)
parametro  ← tabla plana; la leen funciones de v5 (clave, ventana, dominios…)
```

Cadena de "generar": `POST /api/LoteMigracion` → `ServicioLoteMigracion`
→ `CALL sp_crear_lote_migracion` → `CALL sp_generar_lote_migracion`
(cuenta con `fn_exportar_asis`; si es 0 → `RAISE`; inserta el lote, inserta
los detalles, `UPDATE sesion SET estado = 'MIGRADA'`) → devuelve la fila.

## 5. Reglas permanentes aterrizadas a la v7

| Regla | En la v7 significa… |
|---|---|
| Art. 1 — por versiones | No hay v8. Cerrar la v6 con tag antes de oficializar la v7. No agregar lo que nadie pidió (webhook a ASIS, usuario en la bitácora, autorización) |
| Art. 2 — stack | Dapper + `CALL` a SP; sin paquetes nuevos |
| Art. 3 — capas | Validaciones de cruce en el **servicio**; el `CALL` en el **repositorio**; traducir excepciones a HTTP en el **controlador**. `Program.cs` registra las 4 parejas |
| Art. 5 — BD dada | `sp_generar_lote_migracion`, `fn_generar_lote_api`, `fn_exportar_asis`, `fn_trg_bitacora` y los 2 triggers ya están en el script: no se reescriben ni se tocan |
| Art. 6 — comentarios | Cabecera con la tabla real; comentar el porqué de D35, D36, el efecto sobre `sesion` y `ux_detmig_aceptado` |
| Art. 7 — contratos | Los de `6_contracts.md` **después** de corregirlos en la Fase 54 |
| Art. 8 — códigos | 422 forma · 400 servicio (PATCH vacío, límite inválido) · 404 no existe · 500 BD (FK, `pk_parametro`, `ux_detmig_aceptado`, "No hay registros migrables") |
| Guía IA, Regla 1 | El maestro-detalle de la v7 (lote + detalles) **ya** es un SP provisto; `sp_crear_lote_migracion` lo llama. No se diseña otro |
| Guía IA, Regla 2 | `fk_estado_proceso` por `<select>` desde `GET /api/EstadoProceso`; `resultado` y `tipo_dato` por `<select>` de lista cerrada; `fk_sesion` lo fija la URL; filtro de bitácora por `<select>` de tablas |
| Guía IA, Regla 3 | No aplica (ninguna credencial nueva) |

## 6. Lo que la IA tiende a "arreglar" mal

### Reimplementar la generación del lote
Síntoma: un `INSERT INTO lote_migracion` + `INSERT INTO detalle_migracion`
en C# o en un SP nuevo, o un `UPDATE sesion` en el servicio. Rechazar: la
regla RN-08/RN-28, el nombre del archivo y el cambio de estado ya viven en
`sp_generar_lote_migracion` (D34). Duplicarlos es la forma más segura de que
diverjan.

### "Arreglar" que generar cambie la sesión
`MIGRADA` **es el comportamiento esperado** (RF184). No se revierte, no se
valida, no se documenta como error.

### Dar `PUT`/`DELETE` a `LoteMigracion` o `POST` a `DetalleMigracion`
"Para completar el molde". No: el lote es evidencia (RF183) y el detalle
nace solo dentro de generar (RF189). Que el SP exista en la BD no obliga a
exponerlo (mismo criterio que la v6 con las respuestas).

### Quitar el `PATCH` de `LoteMigracion` "porque `LoteCargaAsistente` no lo tiene"
D35: el lote de carga termina al cerrarse; el de migración **empieza** al
generarse y avanza según responda ASIS.

### Capturar `23505` para devolver 409 solo aquí
El 409 (criterio 7 de `VERSION_2.md`) es una decisión de **todo** el
proyecto, pendiente en `ESTADO_V6_Y_CUMPLIMIENTO.md`. Hacerlo solo en la v7
deja dos contratos de error distintos.

### Validar en C# que el `fk_estado_proceso` exista o que no haya otro `ACEPTADO`
D9: lo rechaza el motor (FK `fk_lotemig_estado`, índice
`ux_detmig_aceptado`) → 500. No se revalida.

### Mover `modificado_en` al repositorio "porque lo dice RF197"
Hoy lo pone `sp_actualizar_parametro`. Lo correcto es corregir RF197, no
duplicar el `now()`.

### Llenar `fk_usuario` de la bitácora con un `SET LOCAL`
Nadie lo pidió (nota de §2.3 de `5_data_model.md`). Queda fuera.

## 7. Mapa de archivos de la v7 (estado al 2026-10-15)

| Capa | Archivo | Estado |
|---|---|---|
| BD | `db/postgres/migraciones/013_sp_parametro.sql`, `014_sp_bitacora.sql`, `031_sp_lote_migracion.sql`, `032_sp_detalle_migracion.sql` | ✅ existen, cargan sin error |
| Modelo | `Modelos/LoteMigracion.cs` (10 prop.), `DetalleMigracion.cs` (8), `Bitacora.cs` (9), `Parametro.cs` (6) | ✅ |
| Petición | `LoteMigracionGenerar.cs`, `LoteMigracionActualizar.cs`, `DetalleMigracionActualizar.cs` | ✅ (snake_case) |
| Petición | `ParametroCrear.cs`, `ParametroReemplazo.cs`, `ParametroActualizar.cs` | ✅ (Decisión D pendiente) |
| Petición | `BitacoraCrear.cs`, `BitacoraReemplazo.cs`, `BitacoraActualizar.cs` | ⚠️ sobran con la opción 1 de la Decisión A |
| Repositorio | `IRepositorio{LoteMigracion,DetalleMigracion,Bitacora,Parametro}` + `…Postgres` | ✅ todos por `CALL`; Bitácora ⚠️ (Decisión A) |
| Servicio | `IServicio…` + `Servicio…` de las 4 | ✅; Bitácora ⚠️ |
| Controlador | `LoteMigracionController` (4), `DetalleMigracionController` (3), `ParametroController` (6), `BitacoraController` (6 hoy) | ✅ / ⚠️ |
| Ensamblador | `Program.cs` | ✅ registra las 4 parejas; ❌ `version = "v6"` |
| Pruebas | `pruebas/Programa.cs` | ✅ Lote/Detalle (21); ❌ Parametro y Bitácora |
| Pruebas | `pruebas/verificar_retrofit_fase4.ps1` | ✅ Lote/Detalle por HTTP (requiere Docker) |
| Pruebas | `db/postgres/pruebas/prueba_sp_retrofit_fase4.sql` | ✅ SP de Lote/Detalle |
| Front | `routes/admin.py` (bloques), `templates/admin/…`, `panel.html`, `detalle_sesion.html` | ❌ nada |
| Docs | `version7/2_spec.md` … `8_tasks.md` | ⚠️ borrador con las discrepancias del §2 |

## 8. Datos de ejemplo verificados contra la BD (2026-10-15)

Verificados con `script_bdcatedras.sql` + migraciones `001`-`041` en
PostgreSQL 16, llamando a los mismos SP que los repositorios. Los ids
nuevos suponen una BD recién creada (`docker compose down -v`).

| Para probar… | Usa | Resultado esperado |
|---|---|---|
| Conteos de partida | tablas recién creadas | `lote_migracion` **5** · `detalle_migracion` **823** · `bitacora` **168** · `parametro` **9** |
| Lotes sembrados | `GET /api/LoteMigracion?fkSesion=5712` | 5 lotes, todos de la sesión 5712, archivo `ME_CATEDRA_000099001_R1.xlsx`. Lote 1 `CORRECTO` (164 detalles **ACEPTADO**); lotes 2-5 `GENERADO` (164, 164, 165, 166 detalles **PENDIENTE**) |
| Sesión de prueba | 5712 | `estado: "MIGRADA"` **ya de partida**, reunión 1, 168 registros, 1 externo, **166 migrables**. Es la **única** sesión con registros |
| Ver el efecto RF184 | `PATCH /api/Sesion/5712 {"estado":"CERRADA"}` → luego generar | Antes de generar: `CERRADA`; después: `MIGRADA` |
| Generar | `POST /api/LoteMigracion {"fk_sesion": 5712, "fk_usuario": null}` | 200; lote **6**, `total_filas: 166`, `fk_estado_proceso: "GENERADO"`; `GET /api/DetalleMigracion?fkLoteMigracion=6` → **166** filas `PENDIENTE` |
| Sin nada que migrar | `POST /api/LoteMigracion {"fk_sesion": 1}` (cualquier sesión ≠ 5712) | 500; mensaje "No hay registros migrables en la sesion 1. Revise I10…" (`check_violation`) |
| PATCH de estado | `PATCH /api/LoteMigracion/6 {"fk_estado_proceso":"EN_CURSO"}` | 200; `{"fk_estado_proceso":"NO_EXISTE"}` → 500 (`fk_lotemig_estado`); `{}` → 400 |
| Estados válidos | `GET /api/EstadoProceso` | No finales: `GENERADO`, `EN_COLA`, `EN_CURSO`. Finales: `ERROR`, `CORRECTO` (único exitoso), `INCORRECTO` |
| `ACEPTADO` que pasa | detalle del lote 6 con `fk_registro` **167** o **169** | 200 (son los 2 únicos migrables sin `ACEPTADO` previo) |
| `ACEPTADO` que choca | detalle del lote 6 con `fk_registro` **1** (o cualquiera de los otros 164) | 500 `ux_detmig_aceptado` **al primer intento** (ya está `ACEPTADO` en el lote 1) |
| Bitácora de partida | `GET /api/Bitacora?limite=5` | 168 filas, todas `registro_asistencia` / `INSERT`, `fk_usuario: null` |
| Bitácora que crece sola | `DELETE /api/Asistente/2` (borrado lógico) | Nueva fila `nombre_tabla: "asistente"`, `operacion: "UPDATE"` (`trg_bitacora_asistente`) |
| Parámetros sembrados | `GET /api/Parametro` | `CLAVE_LONGITUD` 6 · `CLAVE_MAX_INTENTOS` 3 · `CLAVE_VIGENCIA_MINUTOS` 10 · `DOMINIOS_INSTITUCIONALES` · `PERMITE_EXTERNOS` true · `URL_BASE` · `VENTANA_MINUTOS_ANTES` 30 · `VENTANA_MINUTOS_DESPUES` 30 · `VERSION_POLITICA_DATOS` 2026.1 |
| Crear / duplicar | `POST /api/Parametro {"clave":"PRUEBA_QUICKSTART","valor":"1","tipo_dato":"ENTERO","descripcion":"Prueba v7"}` | 200; repetir → 500 (`pk_parametro`) |
| `modificado_en` | `PATCH /api/Parametro/PRUEBA_QUICKSTART {"valor":"2"}` | 200; `modificado_en` posterior al del `POST` |
| Borrar | `DELETE /api/Parametro/PRUEBA_QUICKSTART` | 200; un segundo `DELETE` → 404 |

Después de cualquier prueba que modifique datos:
`docker compose down -v && docker compose up -d --build` (y volver a fijar
la clave del administrador: `PATCH /api/Usuario/1 {"clave":"Admin12345"}`).

## 9. Fases con prompts listos

> El código de las Fases 55-57 **ya existe**: sus prompts piden *auditar y
> alinear*, no reescribir. Si trabajas sobre un repositorio sin ese código,
> usa el §13.

### Fase 54 — Preparación: decisiones y corrección del borrador

```text
Fase 54 de version7/8_tasks.md. NO escribas código C# todavía.
1. Contra la BD (script + migraciones), confirma con \sf que
   sp_generar_lote_migracion, fn_generar_lote_api y fn_exportar_asis
   existen y qué hace cada una; lista los triggers que escriben en
   bitacora (\d catedras.asistente y \d catedras.registro_asistencia).
2. Recorre la tabla del §2 de guia_ia_v7.md y confirma o refuta cada fila
   con evidencia (archivo y línea, o salida de psql).
3. Preséntame las decisiones A-F del §3 con tu recomendación y espera mi
   respuesta.
4. Con mis respuestas, propón el diff de los documentos de version7/
   (2_spec, 3_plan, 4_research con D34 enmendado y lo que diga la decisión
   A, 5_data_model, 6_contracts, 7_quickstart, 8_tasks). Mantén el banner
   BORRADOR hasta la Fase 61.
```

**Terminado cuando:** las decisiones A-F están escritas en `4_research.md`
y ningún documento de `version7/` contradice al código ni a la BD.

### Fase 55 — `LoteMigracion` y `DetalleMigracion`

```text
Fase 55. Las capas ya existen. Audítalas contra 2_spec (RF179-RF189),
5_data_model y 6_contracts ya corregidos en la Fase 54:
- LoteMigracionController: exactamente GET lista, GET/{id}, POST, PATCH.
- DetalleMigracionController: exactamente GET lista, GET/{id}, PATCH.
- RepositorioLoteMigracionPostgres.GenerarAsync: CALL
  sp_crear_lote_migracion; el servicio no toca Sesion.
- Interfaces sin CrearAsync/ReemplazarAsync/EliminarAsync donde no toca.
- Nombres JSON según la decisión B; respuesta del POST según el contrato.
- PATCH con body vacío -> 400; resultado fuera de lista -> 422.
- Fechas: GeneradoEn es DateTimeOffset; confirma qué texto sale en la
  respuesta dentro de Docker (UTC) y si front_catedras/servicios/fechas.py
  lo lee bien.
Lista las diferencias; corrige solo lo que contradiga el contrato.
Ejecuta dotnet build y dime el resultado real.
```

**Terminado cuando:** `dotnet build` sin errores ni advertencias nuevas y
cada endpoint coincide con el contrato corregido.

### Fase 56 — `Bitacora`

Prompt para la **opción 1** de la Decisión A:

```text
Fase 56, decisión A = solo lectura en la API. Deja BitacoraController con
GET lista y GET/{id}. Quita CrearAsync/ReemplazarAsync/ActualizarAsync/
EliminarAsync de IServicioBitacora, ServicioBitacora, IRepositorioBitacora y
RepositorioBitacoraPostgres, y borra Peticiones/BitacoraCrear.cs,
BitacoraReemplazo.cs y BitacoraActualizar.cs. NO toques
014_sp_bitacora.sql: los 5 SP siguen en la BD (criterio 3), igual que los
SP no expuestos de RespuestaEncuesta. Actualiza los comentarios de cabecera
y el de Program.cs. Busca con grep cualquier otro uso de los métodos
borrados (front incluido). Ejecuta dotnet build.
```

Con la **opción 2**, el prompt se limita a revisar el controlador contra el
contrato reescrito y a dejar el riesgo documentado en la cabecera.

**Terminado cuando:** compila, y (opción 1) `IRepositorioBitacora` solo
declara `ObtenerTodosAsync` y `ObtenerPorIdAsync`.

### Fase 57 — `Parametro`

```text
Fase 57. Las capas de Parametro ya existen. Audítalas contra RF192-RF197:
6 endpoints con {clave}; clave nunca en el body de PUT/PATCH; PATCH vacío
-> 400; tipo_dato en lista cerrada -> 422; aplica la decisión D
([RegularExpression] de clave, obligatoriedad de tipo_dato). Confirma que
modificado_en lo pone sp_actualizar_parametro y que el repositorio lo
devuelve en UTC. No dupliques now() en C#. Ejecuta dotnet build.
```

**Terminado cuando:** compila y la validación coincide con la Decisión D.

### Fase 58 — Prueba de capas (37 entidades)

```text
Fase 58. En api_catedras/pruebas/Programa.cs ya hay pruebas de
LoteMigracion y DetalleMigracion (bloque "Retrofit Fase 4"). Agrega, con el
MISMO patrón de repositorio FALSO en memoria:
- Parametro: crear, clave duplicada (el falso lanza como lo haría la BD),
  obtener inexistente -> NoEncontradoExcepcion, PATCH vacío -> 400, y que
  ModificadoEn avance en el ActualizarAsync falso.
- Bitacora: listar con filtro nombreTabla, obtener inexistente -> 404, y
  (si decisión A = opción 1) una verificación por reflexión de que
  IRepositorioBitacora e IServicioBitacora no declaran ningún método de
  escritura y que BitacoraController solo tiene atributos HttpGet.
- LoteMigracion: confirma que el falso de GenerarAsync no modifica ninguna
  Sesion (el servicio no debe recibir IRepositorioSesion).
Corre `dotnet run --project pruebas` y dame el total REAL de pasadas y
fallidas. Dime cuántas entidades cubre ahora.
```

**Terminado cuando:** código de salida 0, total anotado en `8_tasks.md`
(debe superar las 385 de la v6) y las 37 entidades cubiertas.

### Fase 59 — Integridad y reglas de negocio contra Docker

```text
Fase 59. Acompáñame a ejecutar contra Docker real, EN ESTE ORDEN, con los
datos del §8 de guia_ia_v7.md:
0. docker compose down -v && docker compose up -d --build; /swagger debe
   mostrar 37 recursos.
1. PATCH /api/Sesion/5712 {"estado":"CERRADA"}; POST /api/LoteMigracion
   {"fk_sesion":5712}; GET del detalle -> 166 filas; GET /api/Sesion/5712
   -> MIGRADA.
2. POST con fk_sesion 1 -> 500 "No hay registros migrables".
3. PATCH lote EN_CURSO -> 200; NO_EXISTE -> 500; {} -> 400.
4. PATCH detalle (registro 167) ACEPTADO -> 200; detalle (registro 1)
   ACEPTADO -> 500 ux_detmig_aceptado.
5. Bitacora: GET ?limite=5; DELETE /api/Asistente/2 y ver la fila nueva
   'asistente'/UPDATE; POST/PUT/PATCH/DELETE sobre /api/Bitacora: anota el
   código REAL (esperable 405 con la opción 1).
6. Parametro: POST, POST duplicado (500), PATCH (modificado_en avanza),
   DELETE (200), DELETE otra vez (404).
7. PUT y DELETE sobre /api/LoteMigracion/6 y POST sobre
   /api/DetalleMigracion: anota el código real.
8. Corre también pruebas/verificar_retrofit_fase4.ps1 (regresión) y dime
   su resumen.
Para cada paso dame el comando (PowerShell Invoke-RestMethod o curl), el
resultado esperado y qué revisar si no coincide. No marques nada como
pasado sin que yo pegue la salida.
```

**Terminado cuando:** los 8 pasos coinciden con el §8 y los códigos reales
de los pasos 5 y 7 quedaron escritos en `2_spec.md` y `7_quickstart.md`.

### Fase 60 — Front

```text
Fase 60 (criterio 8), con la decisión E. En front_catedras:
1. routes/admin.py: bloques nuevos con el mismo molde que los existentes
   (lista + formulario + mensajes flash, sin mock):
   - Parametros: lista, crear, editar (clave de solo lectura al editar),
     eliminar con confirm(). tipo_dato por <select> de lista cerrada.
   - Lotes de migración: lista con filtro por sesión; detalle del lote con
     sus filas (GET /api/DetalleMigracion?fkLoteMigracion=); formulario
     para cambiar estado (<select> desde GET /api/EstadoProceso) y
     observaciones; formulario por fila para resultado (<select>
     PENDIENTE/ACEPTADO/RECHAZADO) y mensaje.
   - Bitacora: SOLO lectura, filtro por tabla con <select>, limite, y
     datos_antes/datos_despues legibles (pre con JSON indentado).
   - "Generar migración" de una sesión: POST bajo /admin que llama
     POST /api/LoteMigracion y redirige al detalle del lote nuevo.
2. templates/admin/: plantillas nuevas con el layout de las existentes.
3. panel.html: 4 tarjetas nuevas.
4. detalle_sesion.html: botón "Generar migración" solo si el usuario es
   ADMIN, la sesión ya terminó y tiene registros.
Los errores de la API (500 de ux_detmig_aceptado, sesión sin migrables) se
muestran como mensaje en castellano. En ninguna pantalla pueden aparecer
"PUT", "PATCH", "422" ni "FK" (criterio 11 de VERSION_2.md). Dame la lista
de rutas nuevas y los pasos para probarlas a mano.
```

**Terminado cuando:** desde el botón de `detalle_sesion.html` se genera un
lote contra Docker real, la sesión aparece como migrada sin haber tocado
`Sesion` desde el front, y apagar la API produce un error claro en cada
pantalla nueva.

### Fase 61 — Cierre de la v7 y del proyecto

```text
Fase 61. Con el entorno recién reseteado, acompáñame a correr los 8
criterios de aceptación de 2_spec.md §5 en orden (comando, esperado, qué
revisar). Después:
- Program.cs: version = "v7" y contratos -> version7/6_contracts.md.
- Quita el banner BORRADOR de version7/ (y de los que sigan con él).
- Actualiza README.md (sección v7 + estado), 0_mapa_versiones.md (estado
  de la V7), guia_ia.md (fila de la v7 en la tabla de cumplimiento y
  enlace a esta guía) y 8_tasks.md con el total de la prueba de capas.
- Confirma la cuenta de control 10+5+7+2+5+4+4 = 37 contra /swagger.
- Repasa ESTADO_V6_Y_CUMPLIMIENTO.md: 409, carpeta de prompts (§11),
  cierre formal de v1. Indica qué sigue abierto; no lo resuelvas sin mi
  confirmación.
- Genera el texto del commit en español y recuérdame el tag v7.
```

**Terminado cuando:** los 8 criterios pasan, todas las casillas de
`8_tasks.md` están marcadas con evidencia, y existen los tags `v6` y `v7`.

## 10. Trampas conocidas y diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| `POST /api/LoteMigracion` → 422 "fk_sesion es obligatorio" aunque lo mandaste | Cuerpo en camelCase (`fkSesion`) | snake_case en cuerpos (Decisión B) |
| `POST /api/LoteMigracion` → 500 "No hay registros migrables" | La sesión no tiene registros (todas salvo 5712) o solo externos / sin `id_asis` / sin programa | Usar 5712 |
| `GET /api/Sesion/5712` dice `MIGRADA` antes de generar | Datos sembrados | `PATCH` a `CERRADA` antes (§8) |
| `PATCH` de detalle con `ACEPTADO` → 500 | Ese registro ya está `ACEPTADO` en otro lote (`ux_detmig_aceptado`); pasa con 164 de 166 | Correcto. Para el 200, registros 167 o 169 |
| `PATCH /api/DetalleMigracion/{id}` → 404 con un id "del lote nuevo" | Los ids de detalle dependen de la secuencia; con lotes generados antes cambian | Tomar el `id_detalle` de `GET …?fkLoteMigracion=` |
| Verbos inexistentes devuelven 405 y el spec dice 404 | Comportamiento de ASP.NET Core con rutas que existen para otro verbo | Escribir en el spec el código real |
| La bitácora tiene filas de `asistente` | `trg_bitacora_asistente` + borrado lógico de `DELETE /api/Asistente` | Correcto; corregir los documentos que dicen lo contrario |
| `fk_usuario` siempre `null` en bitácora y lotes | Nadie propaga el usuario a la BD | Correcto (Decisión C) |
| `generado_en` sale con `+00:00` y `modificado_en` con `Z` | `GeneradoEn` es `DateTimeOffset` | Aceptable; verificar que `fechas.py` lo lea |
| Migración nueva sin efecto en una BD existente | `initdb` solo corre con el volumen vacío | `docker compose down -v` o aplicar el `.sql` con `psql` |
| `procedure … does not exist` en un `CALL` | Tipos del parámetro (`int`→`smallint`, `DateOnly`→`timestamp`) | Ver ajustes de `RETROFIT_FASE4_NOTAS.md` §2 |
| La prueba de capas no compila tras la Fase 56 | Falsos que todavía implementan métodos borrados de `IRepositorioBitacora` | Quitarlos del falso |
| `/admin/...` nuevo accesible sin login | Blueprint nuevo sin la guardia de `admin_bp` | Decisión E |

## 11. Lista de revisión para cualquier cambio de la v7

- [ ] Solo toca las 4 tablas de la v7 (y `Program.cs`, `pruebas`, front).
- [ ] Ningún repositorio con SQL suelto: todo `CALL catedras.sp_…`.
- [ ] Generar lote = `sp_crear_lote_migracion` → `sp_generar_lote_migracion`;
      ningún `UPDATE sesion` en C#.
- [ ] `LoteMigracion` sin `PUT`/`DELETE`; `DetalleMigracion` sin
      `POST`/`PUT`/`DELETE`; `Bitacora` según la Decisión A, por escrito.
- [ ] Sin revalidar FK ni `ux_detmig_aceptado` en C# (D9).
- [ ] JSON según la Decisión B, igual en código y en `6_contracts.md`.
- [ ] `modificado_en` lo pone el SP; ningún `now()` en C#.
- [ ] 422/400/404/500 como en v1-v6; sin 409 aislado.
- [ ] Front: FK por `<select>`, sin mock, rutas bajo la protección de `ADMIN`.
- [ ] Comentarios en español que explican el porqué.
- [ ] `8_tasks.md` actualizado con lo verificado **y dónde** (comando + salida).
- [ ] Ninguna afirmación de "pasa" sin salida real pegada.

## 12. Registro de prompts (entregable §11 de `VERSION_2.md`)

Crea `docs/spec_kit/Versiones/version7/prompts/` con un archivo por fase
(`fase54.md`, …). En cada uno:

```markdown
## Prompt usado
(texto exacto, de esta guía o propio)

## Herramienta
(chat / IDE agéntico, y cuál)

## Qué hubo que corregirle a la IA
- (qué propuso, por qué estaba mal, qué ADR o documento lo contradice)

## Evidencia
(comando ejecutado y salida resumida)
```

Las correcciones más probables en esta versión son justamente las del §6.

## 13. Prompt de replicación completa de la v7

Para reconstruir la v7 en una sola sesión partiendo de una v6 cerrada (otra
máquina, rama limpia o un compañero). Funciona tanto si los archivos de la
v7 no existen como si existen a medias. Las líneas marcadas con `[DECISIÓN]`
llevan la opción recomendada en el §3: cámbialas si el equipo decidió otra
cosa.

```text
Eres mi asistente para replicar la VERSIÓN 7 del Proyecto Cátedras
(migración a ASIS, bitácora y parámetros), la última del mapa de versiones.
Responde en español, comenta el porqué en el código y nunca afirmes que algo
compila o pasa sin haberlo ejecutado y mostrado la salida.

CONTEXTO
- Repositorio: docker-compose (postgres + api en :8142), api_catedras (C#,
  ASP.NET Core .NET 10, Dapper, sin EF), front_catedras (Flask en :5000),
  db/postgres/script_bdcatedras.sql (BD completa, PROVISTA, no se toca) y
  db/postgres/migraciones/ (SP propios, montados como zNNN_ en initdb).
- La v6 está cerrada: 33 recursos. La v7 agrega 4 y llega a 37:
  LoteMigracion, DetalleMigracion, Bitacora, Parametro.
- Molde vigente (retrofit criterio 3): cada verbo de un repositorio es
  CALL catedras.sp_<verbo>_<tabla>(..., NULL) con INOUT p_resultado json;
  NULL = no encontrado. Plantilla: 005_sp_sede_ejemplo.sql +
  RepositorioSedePostgres.cs (PATCH = consultar, mezclar, llamar al
  sp_actualizar_ completo). Fechas timestamptz -> normalizar a UTC al leer.

LEE PRIMERO (y no escribas código hasta resumirlos):
1_constitution.md, guia_ia.md, Versiones/0_mapa_versiones.md,
PLAN_RETROFIT_SP.md, AJUSTES_PATRON_SEDE_LOTE.md, RETROFIT_FASE4_NOTAS.md,
version7/2_spec.md..8_tasks.md y version7/guia_ia_v7.md (§2 y §3).
Luego inspecciona qué archivos de la v7 YA existen y dime: existe y cumple /
existe y difiere / falta. No reescribas lo que cumple.

ALCANCE Y CONTRATO
1. Migraciones (si faltan): 013_sp_parametro, 014_sp_bitacora,
   031_sp_lote_migracion, 032_sp_detalle_migracion, montadas en
   docker-compose como z013/z014/z031/z032, todas CREATE OR REPLACE.
   - sp_crear_lote_migracion(p_fk_sesion, p_fk_usuario, INOUT json) LLAMA a
     sp_generar_lote_migracion (provisto) y devuelve la fila. Prohibido
     reimplementar la generación, el conteo o el UPDATE de sesion.
   - sp_actualizar_lote_migracion solo cambia instancia_proceso_asis,
     fk_estado_proceso y observaciones.
   - sp_actualizar_detalle_migracion solo cambia resultado y mensaje (el
     parámetro de la columna se llama p_resultado_migracion porque
     p_resultado es el INOUT).
   - sp_actualizar_parametro pone modificado_en = now() (no hay trigger).
   - sp_eliminar_* son DELETE físicos (ninguna de las 4 tablas tiene activo).
2. LoteMigracion: GET /api/LoteMigracion?fkSesion=&limite=, GET /{id},
   POST (generar; body {"fk_sesion", "fk_usuario"?}) -> 200
   {estado, mensaje, id_lote_migracion, lote}, PATCH /{id}
   ({"fk_estado_proceso"?, "observaciones"?}; vacío -> 400). Sin PUT ni
   DELETE. El servicio no toca Sesion: el SP la deja en MIGRADA.
3. DetalleMigracion: GET ?fkLoteMigracion=&limite=, GET /{id}, PATCH /{id}
   ({"resultado"? PENDIENTE|ACEPTADO|RECHAZADO -> 422 si no, "mensaje"?}).
   Sin POST, PUT ni DELETE. Segundo ACEPTADO del mismo registro -> 500
   (ux_detmig_aceptado), sin revalidar en C#.
4. Bitacora: [DECISIÓN A] SOLO LECTURA en la API: GET ?nombreTabla=&limite=
   (más recientes primero) y GET /{id}. Los 5 SP existen en la BD pero
   crear/actualizar/eliminar NO tienen llamador; ni peticiones ni métodos
   de escritura en interfaz, servicio o controlador.
   [DECISIÓN C] Sin filtro por usuario (fk_usuario siempre es NULL).
5. Parametro: GET (lista), GET/POST/PUT/PATCH/DELETE con {clave}. clave
   solo en POST, nunca en el body de PUT/PATCH. tipo_dato en lista cerrada
   TEXTO|ENTERO|BOOLEANO|FECHA|DECIMAL (opcional en POST, default TEXTO).
   [DECISIÓN D] clave con [RegularExpression("^[A-Z][A-Z0-9_]*$")].
   Clave duplicada -> 500.
6. [DECISIÓN B] Cuerpos y respuestas en snake_case con [JsonPropertyName];
   query en camelCase. Códigos: 422 forma, 400 servicio, 404 no existe,
   500 cualquier NpgsqlException. Sin 409.
7. Program.cs: registrar las 4 parejas IRepositorio/IServicio y poner
   version = "v7".
8. pruebas/Programa.cs: repositorios FALSOS para las 4 entidades (generar
   crea maestro y detalles en memoria sin tocar sesiones; clave duplicada;
   ModificadoEn avanza; PATCH vacío -> 400; inexistente -> 404) y
   verificaciones por reflexión: sin EliminarAsync en IRepositorioLoteMigracion,
   sin CrearAsync/EliminarAsync en IRepositorioDetalleMigracion y sin ningún
   método de escritura en IRepositorioBitacora / BitacoraController.
9. Front (Flask), dentro de routes/admin.py para heredar la guardia ADMIN:
   CRUD de parámetros; lista y detalle de lotes con PATCH de estado
   (<select> desde /api/EstadoProceso) y PATCH de resultado por fila;
   bitácora de solo lectura con filtro por tabla; acción "generar migración"
   bajo /admin; botón en detalle_sesion.html visible solo para ADMIN en
   sesiones terminadas con registros; 4 tarjetas en panel.html. FK por
   <select>, sin mock, mensajes en castellano, sin "PUT/PATCH/422/FK" en la
   interfaz.
10. Documentos de version7/: alinearlos con lo construido (D34 enmendado:
    hay SP por el criterio 3 pero no un maestro-detalle nuevo; total_filas
    excluye externos; trg_bitacora_asistente también escribe en bitácora;
    modificado_en lo pone el SP; códigos reales de verbos inexistentes).

DATOS PARA VERIFICAR (BD recién creada)
5 lotes / 823 detalles / 168 bitácora / 9 parámetros. Solo la sesión 5712
tiene registros (168, 1 externo, 166 migrables) y ya está MIGRADA: hacer
PATCH /api/Sesion/5712 {"estado":"CERRADA"} antes de generar. Generar ->
lote 6 con 166 detalles. fk_sesion 1 -> 500 "No hay registros migrables".
ACEPTADO pasa solo con los registros 167 y 169; con el registro 1 -> 500.
fk_estado_proceso NO_EXISTE -> 500. DELETE /api/Asistente/2 agrega una fila
'asistente'/UPDATE a la bitácora.

FORMA DE TRABAJO
Fases 54 a 61 de version7/8_tasks.md, una por turno: plan -> cambios ->
dotnet build -> pruebas -> resumen con comandos y salidas reales. Al final
de cada fase, lista lo que tuve que corregirte (lo guardo en
version7/prompts/). No crees el tag v7 ni quites el banner BORRADOR hasta
que yo confirme los 8 criterios de aceptación contra Docker.
```

## 14. Después de la v7

No hay v8. Lo que queda abierto no pertenece a ninguna fila del mapa y se
decide como equipo (ver `ESTADO_V6_Y_CUMPLIMIENTO.md`):

- 409 en vez de 500 para FK inexistente y pareja repetida (criterio 7 de
  `VERSION_2.md`), idealmente con un filtro global y no controlador por
  controlador.
- Carpeta de prompts (§11) de las versiones anteriores.
- Cierre formal de la v1 (Fase 12) y tags pendientes (`v1`, `v5`, `v6`).
- Si algún día se integra ASIS de verdad, el `PATCH` manual de D35 se
  sustituye por un proceso que consulte su estado; no antes.

---

**Regla final:** la IA ayuda a construir y revisar; la evidencia de que
funciona sale de comandos ejecutados sobre el proyecto real.
