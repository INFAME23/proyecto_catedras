# Guía IA — Versión 1: el esqueleto y las 10 tablas sin llaves foráneas

> **Qué es este documento.** La guía operativa para construir, revisar o
> verificar la **v1** con un asistente de IA (Claude, Copilot, ChatGPT…),
> siguiendo el spec kit de esta versión. **No crea reglas nuevas**: aterriza
> a la v1 las que ya existen.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> (sus 3 reglas) › spec kit v1 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si esta guía contradice al spec kit, gana el spec kit y se corrige esta
> guía.
>
> **Estado de la v1 (2026-10-15):** código completo (Fases 0-13 marcadas en
> `8_tasks.md`), las 10 tablas sin FK expuestas y cubiertas por la prueba de
> capas. **Ojo:** algunos textos de `2_spec.md`/`3_plan.md`/`7_quickstart.md`
> quedaron **desactualizados** (dicen "4 tablas construidas y 6 pendientes" o
> que el front está fuera del compose) y el código actual **ya pasó por el
> retrofit de procedimientos almacenados** (§5.bis). Esta guía te dice
> cuál de las dos fotos usar (§0, modos A y B). Además, el
> [mapa de versiones](../0_mapa_versiones.md) y `ESTADO_V6_Y_CUMPLIMIENTO.md`
> todavía listan la v1 como **no cerrada formalmente** (ver §11).

---

## 0. Cómo usar esta guía

1. **Elige el modo** (es lo primero que debes decirle a la IA):

   | Modo | Cuándo | Qué significa |
   |---|---|---|
   | **A — Reconstruir "como se construyó"** | Ejercicio de estudio: rehacer la v1 desde cero siguiendo el spec kit | SQL a mano en el repositorio, con `@parametros` (Dapper). Es lo que describe `3_plan.md` §4.4 |
   | **B — Trabajar sobre el código de hoy** | Revisar, arreglar o ampliar el repositorio actual | Los repositorios llaman procedimientos (`CALL catedras.sp_<verbo>_<tabla>(…)`), migraciones 005-023. Capas superiores **sin cambios** |

   Si no lo dices, la IA mezclará ambos y escribirá SQL en un repositorio
   que ya usa `CALL` (o al revés).
2. Abre una conversación nueva y pega el **prompt de arranque** (§1).
   Adjunta o da acceso al repositorio.
3. Trabaja **una fase a la vez** (§8). Cada fase trae un prompt, la lista de
   "terminado cuando…" y los errores típicos.
4. No marques una casilla de `8_tasks.md` hasta que la verificación pase **en
   tu máquina** (Artículo 1). La IA escribe código; quien verifica eres tú.
5. Si la IA propone algo de §5 o §9, recházalo citando el ADR.
6. **Si la IA encuentra dos documentos que se contradicen** (spec vs
   `VERSION_2.md`, 500 vs 409, etc.), debe **reportarlo** y preguntarte; no
   resolverlo en silencio.

## 1. Prompt de arranque (cópialo tal cual)

```text
Vas a trabajar en el Proyecto Cátedras, VERSIÓN 1 (esqueleto en capas + las
10 tablas sin llaves foráneas salientes). Es material de estudio: todo en
español y con comentarios línea a línea.

MODO: [ A = reconstruir como se construyó | B = trabajar sobre el código de hoy ]

Antes de escribir código, lee EN ESTE ORDEN y confirma con un resumen de
5 líneas que lo entendiste:
  1. docs/spec_kit/1_constitution.md          (reglas permanentes, ganan siempre)
  2. docs/spec_kit/guia_ia.md                 (Reglas 1-3; en la v1 NINGUNA aplica todavía)
  3. docs/spec_kit/Versiones/0_mapa_versiones.md (qué tablas puede nombrar cada versión)
  4. docs/spec_kit/Versiones/version1/2_spec.md → 8_tasks.md (en orden numérico)
  5. docs/spec_kit/Versiones/version1/guia_ia_v1.md (esta guía)
  6. SOLO en modo B: docs/spec_kit/Versiones/PLAN_RETROFIT_SP.md y
     db/postgres/migraciones/005_sp_sede_ejemplo.sql (el molde de los SP)

Reglas que no se negocian en la v1:
  - Solo puedes nombrar las 10 tablas SIN FK saliente: sede, encuesta,
    estado_proceso, modalidad, periodo_academico, rol, tipo_documento,
    tipo_evento, tipo_pregunta, tipo_vinculacion. catedra, sesion, asistente,
    usuario... NO se consultan ni se exponen, aunque la BD ya las tenga.
  - C# + ASP.NET Core (.NET 10) + Dapper + Npgsql + Swashbuckle. Exactamente
    3 paquetes NuGet. Sin Entity Framework.
  - Modo A: SQL a mano en el repositorio, SIEMPRE con @parametros y prefijo
    catedras. Modo B: el repositorio solo hace CALL al procedimiento.
  - Capas: Controller -> IServicio -> IRepositorio -> RepositorioXPostgres.
    Solo Program.cs conoce clases concretas.
  - Una petición por verbo (XCrear, XReemplazo, XActualizar). La FORMA se
    valida en la petición (422); las reglas de NEGOCIO, en el servicio (400).
  - NO hay fábrica multi-motor ni selección de motor (ADR D3, RNF6). Un solo
    motor: PostgreSQL.
  - La BD se usa tal cual (script_bdcatedras.sql): no se modifica ni una
    columna, ni un índice, ni los datos semilla (Art. 5).
  - Contratos exactos: sobres {tabla, limite, total, datos} y
    {estado, mensaje, detalle}; 422 forma · 400 negocio · 404 no existe ·
    500 motor (Art. 8). Lista vacía = 204.

Trabajaremos fase por fase (Fases 0-13 de 8_tasks.md). No avances a la
siguiente fase hasta que te confirme que la verificación pasó.
```

## 2. Orden de lectura y qué sacar de cada documento

| # | Documento | Qué debe extraer la IA |
|---|---|---|
| 1 | `1_constitution.md` | Art. 1 (por versiones, YAGNI), Art. 2 (Dapper, SQL a mano, 3 paquetes), Art. 3 (capas e interfaces), Art. 4 (un solo comando), Art. 5 (BD dada), Art. 6 (español, comentarios línea a línea), Art. 7 (contratos exactos), Art. 8 (puertos, sobres, códigos) |
| 2 | `guia_ia.md` | Ninguna de las 3 reglas aplica a la v1: no hay maestro-detalle (R1), no hay FK (R2), no hay contraseñas (R3) |
| 3 | `0_mapa_versiones.md` | Fila V1: las 10 tablas y el front mínimo (CRUD de catálogos) |
| 4 | `2_spec.md` | RF1-RF61 (Sede, Encuesta, EstadoProceso, Modalidad detallados; las otras 6 en el bloque "Pendientes de implementación"), RNF1-RNF6 y los **9 criterios de aceptación** |
| 5 | `3_plan.md` | §4.1 interfaz de 5 métodos · §4.2 peticiones · §4.4 SQL tipo · §4.5 tabla de excepciones→HTTP · §4.7 IDENTITY + `RETURNING` + `DateOnly` · §4.8 reglas entre dos campos · §4.9 listas cerradas |
| 6 | `4_research.md` | ADR D1 (Dapper), D2 (capas desde el día 1), D3 (sin fábrica), D4 (BD completa), D5 (validación en peticiones), D6 (PostgreSQL), D7 (`dotnet watch`), D8 (compose) — el **porqué** |
| 7 | `5_data_model.md` | Columnas, `CHECK`, FK **entrantes** de cada tabla (RESTRICT/CASCADE/SET NULL) y datos semilla |
| 8 | `6_contracts.md` | Puertos (15542 / 8142→8042 / 5000), variables de entorno, credenciales, reset |
| 9 | `7_quickstart.md` | Smoke test de 8 pasos + front + problemas frecuentes |
| 10 | `8_tasks.md` | Fases 0-13: qué está hecho y cómo se verificó |

## 3. La v1 en una página

- **Qué agrega:** el esqueleto completo (compose, API en capas, Swagger,
  prueba de capas, front mínimo) y **10 recursos** de la API (uno por tabla
  sin FK saliente). 61 RF (RF1-RF61), 6 RNF, 8 ADR (D1-D8).
- **Qué NO agrega:** ninguna tabla con FK saliente, ningún maestro-detalle,
  ninguna contraseña, otro motor, fábrica de repositorios, Entity Framework
  ni autenticación.
- **Lo que enseña:** los **5 verbos HTTP** y las **3 vías de envío de datos**
  (ruta, query string, body); capas con interfaces; validación de forma
  (422) frente a validación de negocio (400); la BD como última muralla
  (500); una PK autogenerada (`RETURNING`); y qué hacer cuando Dapper no
  conoce un tipo (`DateOnly`).

```
                       ┌──────── 10 tablas, ninguna con FK saliente ────────┐
 Controller ─► IServicioX ─► IRepositorioX ─► RepositorioXPostgres ─► PostgreSQL
 (6 endpoints)  (reglas 400/404)  (interfaz)     (Dapper / CALL sp_*)   esquema catedras

 sede · encuesta · estado_proceso · modalidad · periodo_academico
 rol  · tipo_documento · tipo_evento · tipo_pregunta · tipo_vinculacion
```

Seis endpoints por recurso: `GET` lista (`?limite=`), `GET /{id}`, `POST`,
`PUT /{id}`, `PATCH /{id}`, `DELETE /{id}`. Más `GET /` (diagnóstico) y
`/swagger`.

## 4. Reglas permanentes aterrizadas a la v1

| Regla | En la v1 significa… |
|---|---|
| Art. 1 — no anticipar | Nada de fábrica multi-motor, devcontainer, login ni tablas con FK. Si una tabla de v2+ "se vería bien" aquí, es un error |
| Art. 2 — SQL a mano | (Modo A) todo `SELECT/INSERT/UPDATE/DELETE` escrito en el repositorio con `catedras.` y `@parametros`; el `SET` del `UPDATE` solo con columnas de una **lista blanca** (`ColumnasActualizables`). (Modo B) el SQL vive en las migraciones y el repositorio hace `CALL` |
| Art. 3 — capas | Controller sin SQL; servicio sin HTTP ni motor; repositorio sin HTTP. Contratos con `interface` |
| Art. 4 — un comando | `docker compose up -d --build` deja `postgres` (sembrado solo) + `api` + `front` |
| Art. 5 — BD dada | Las 37 tablas existen desde el primer arranque; la v1 solo toca 10 |
| Art. 6 — comentarios | Cada archivo lleva cabecera con la definición real de la tabla y comentarios que expliquen el *porqué* |
| Art. 7 — contratos | `POST /api/Sede` → `{estado, mensaje}`; `POST /api/Encuesta` → `{estado, mensaje, id_encuesta}`; `PUT/PATCH/DELETE` → `filasAfectadas`; lista → `{tabla, limite, total, datos}` |
| Art. 8 — códigos | 422 forma (con `errores:[…]`) · 400 regla de negocio (límite ≤ 0, PATCH vacío, fechas invertidas, coherencia) · 404 no existe · 500 error del motor (duplicado, FK, CHECK) en `detalle` |
| Guía IA, Reglas 1-3 | No aplican en la v1 |

## 5. Las decisiones que la IA tiende a "arreglar" mal

### D5 / RNF5 — La FORMA en la petición, el NEGOCIO en el servicio

```csharp
// ✗ MAL: validar la forma en el servicio, o una regla de negocio en la petición.
if (string.IsNullOrWhiteSpace(sede.Nombre)) throw new ArgumentException(...); // forma → 422, no 400

// ✓ BIEN: la petición declara la forma; ASP.NET responde 422 solo.
[Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
[StringLength(80, MinimumLength = 1)]
public string? Nombre { get; set; }
public bool? EsVirtual { get; set; }   // bool? : un "texto" no encaja y cae en 422
```

`PUT` (`XReemplazo`) = todos los campos obligatorios; `PATCH` (`XActualizar`)
= todos opcionales; `POST` (`XCrear`) = obligatorios **más** la PK si la
manda el cliente. El PATCH con body vacío es **400** (lo decide el servicio:
es regla, no forma).

### §4.8 — Reglas entre dos campos viven en el SERVICIO (y el PATCH solo las aplica si llegan las dos mitades)

```csharp
// ✗ MAL: [Range]/[Required] no pueden comparar dos campos; ni leer la fila en PATCH "por si acaso".
// ✓ BIEN: ServicioEncuesta / ServicioEstadoProceso / ServicioPeriodoAcademico / ServicioTipoPregunta
if (vigenteHasta is not null && vigenteHasta < vigenteDesde)
    throw new ArgumentException("'vigente_hasta' no puede ser anterior a 'vigente_desde'.");
```

| Tabla | Regla | `CHECK` de la BD (última muralla) |
|---|---|---|
| `encuesta` | `vigente_hasta >= vigente_desde` | `chk_encuesta_vig` |
| `estado_proceso` | `es_exitoso` ⇒ `es_final` | `chk_estado_coherente` |
| `periodo_academico` | `fecha_fin > fecha_ini` (estricto) | `chk_periodo_fechas` |
| `tipo_pregunta` | **exactamente una** de `guarda_numero`/`guarda_texto`/`guarda_opcion` | `chk_tipo_pregunta_destino` |

En **PATCH original** (modo A) la comprobación solo corre si llegan las dos
mitades; con una sola, el `CHECK` responde 500 (costo aceptado y
documentado). En **modo B** el PATCH ya mezcla con la fila guardada (§5.bis),
así que el servicio puede comparar contra el valor actual; **no cambies ese
comportamiento sin preguntar**: está documentado en `RETROFIT_FASE4_NOTAS.md`.

### §4.9 — Listas cerradas y longitudes fijas: en la petición

```csharp
// ✗ MAL: comprobar canal_difusion en el servicio (es forma de UN campo, no relación entre dos).
// ✓ BIEN:
[RegularExpression("^(QR|ENLACE|AMBOS)$", ErrorMessage = "...debe ser 'QR', 'ENLACE' o 'AMBOS'.")]
public string? CanalDifusion { get; set; }                                   // → 422

[RegularExpression("^[0-9]{4}-[1-3]$")]  public string? IdPeriodo { get; set; }  // PeriodoAcademicoCrear
[StringLength(4, MinimumLength = 4)]     public string? IdTipoEvento { get; set; } // TipoEventoCrear: longitud FIJA
```

`tipo_vinculacion.es_interno` es **obligatorio sin `DEFAULT`**: un `POST` sin
él es 422, no se "completa" con `false`.

### §4.7 — `Encuesta`: IDENTITY, `RETURNING` y `DateOnly`

```csharp
// ✗ MAL: EncuestaCrear con IdEncuesta; ExecuteAsync (no devuelve el id); parámetro DateOnly sin manejador.
// ✓ BIEN:
Task<long> CrearAsync(Encuesta encuesta);        // id generado por la BD
// SQL (modo A):  INSERT INTO catedras.encuesta (...) VALUES (...) RETURNING id_encuesta;
// Dapper:        QuerySingleAsync<long>(...)     // no ExecuteAsync
// Program.cs:    SqlMapper.AddTypeHandler(new ManejadorTipoDateOnly());  // DateOnly y DateOnly?
```

La ruta de `Encuesta` usa `long idEncuesta`; las demás, `string`. Un `POST`
sin body extra crea con los defaults (`version=1`, `vigente_desde`=hoy,
`activa=true`).

### D3 / RNF6 — Sin fábrica de motores

```csharp
// ✗ MAL: IRepositorioFabrica, selección por appsettings, segundo proveedor "por si acaso".
// ✓ BIEN (Program.cs): una línea por pareja.
builder.Services.AddScoped<IRepositorioSede>(_ => new RepositorioSedePostgres(cadenaConexion));
builder.Services.AddScoped<IServicioSede, ServicioSede>();
```

### D1 — Dapper, no Entity Framework

Dapper ejecuta el SQL tal cual y mapea columna→propiedad por nombre
(`DefaultTypeMap.MatchNamesWithUnderscores = true` en `Program.cs`). No
agregues `Microsoft.EntityFrameworkCore.*` ni un cuarto paquete.

### 5.bis Retrofit de la v1 (solo modo B): qué cambió y qué no

El criterio 3 de `VERSION_2.md` obligó a pasar las tablas expuestas por la API
(hoy 37 controladores) a procedimientos almacenados. Para las 10 tablas de la v1 (migraciones
**005, 015-023**):

| Spec original (modo A) | Código de hoy (modo B) |
|---|---|
| `SELECT … LIMIT @limite` en el repositorio | `CALL catedras.sp_listar_<t>(@limite, @incluirInactivos, NULL)` → JSON → `JsonSerializer.Deserialize<List<T>>` |
| `INSERT … VALUES (@…)` | `CALL sp_crear_<t>(…)`; devuelve la fila como quedó (en `encuesta`, `id_encuesta` sale del JSON) |
| `UPDATE` dinámico con lista blanca (PUT y PATCH) | **Un solo** `sp_actualizar_<t>` con todas las columnas; el PATCH **consulta + mezcla + llama** al mismo SP |
| `DELETE` físico | **Lógico** (`SET activo=false`) en `sede`, `modalidad`, `periodo_academico`, `tipo_documento`, `tipo_evento`, `tipo_vinculacion`; **físico** en `encuesta`, `estado_proceso`, `rol`, `tipo_pregunta` (no tienen columna `activo`) |
| Lista = todas las filas | Solo `activo = true`; `?incluirInactivos=true` trae todas |
| `DateOnly` con `ManejadorTipoDateOnly` | Sigue, pero los `CALL` exigen `CAST(@x AS date)` (el `CALL` resuelve por **tipos** y `timestamp → date` no es implícito) |
| Duplicado / FK / `CHECK` → 500 | **Igual** (criterio 7 — 409 en vez de 500 — **NO está implementado**; no lo "arregles" aquí, es una decisión abierta) |

`IRepositorioX`, `ServicioX`, `XController` y `Peticiones/` **no cambiaron**
(SOLID · D). Si la IA toca esas capas para "adaptarlas" al SP, está
equivocada.

## 6. Mapa de archivos de la v1

| Capa | Archivo(s) | Nota |
|---|---|---|
| Proyecto | `api_catedras/ApiCatedras.csproj`, `Dockerfile`, `appsettings.json` | 3 paquetes: `Npgsql`, `Dapper`, `Swashbuckle.AspNetCore`; SDK Web .NET 10 |
| Ensamblador | `api_catedras/Program.cs` | DI, `MatchNamesWithUnderscores`, `ManejadorTipoDateOnly`, 422 personalizado, `GET /`, Swagger |
| Excepción | `Excepciones/NoEncontradoExcepcion.cs` | → 404 |
| Modelos | `Modelos/{Sede,Encuesta,EstadoProceso,Modalidad,PeriodoAcademico,Rol,TipoDocumento,TipoEvento,TipoPregunta,TipoVinculacion}.cs` | Cabecera con la definición real de la tabla |
| Peticiones | `Peticiones/<Entidad>{Crear,Reemplazo,Actualizar}.cs` | 30 archivos; anotaciones de forma |
| Repositorios | `Repositorios/IRepositorio<Entidad>.cs` + `Repositorio<Entidad>Postgres.cs` | 5 métodos (en modo B, `ObtenerTodosAsync` de las tablas con `activo` suma `incluirInactivos`) |
| Repositorios | `Repositorios/ManejadorTipoDateOnly.cs` | `Encuesta` y `PeriodoAcademico` |
| Servicios | `Servicios/IServicio<Entidad>.cs` + `Servicio<Entidad>.cs` | Reglas 400 / 404 |
| Controladores | `Controllers/<Entidad>Controller.cs` | 6 endpoints, `try/catch` plano → código HTTP |
| BD (solo modo B) | `db/postgres/migraciones/005_sp_sede_ejemplo.sql`, `015…023_sp_*.sql` | Molde: `005` |
| BD | `db/postgres/script_bdcatedras.sql` | **Dado.** Solo corre con volumen vacío |
| Compose | `docker-compose.yml` | `postgres` 15542→5432 · `api` 8142→8042 · `front` 5000 |
| Pruebas | `api_catedras/pruebas/{PruebaCapas.csproj,Programa.cs}` | Un `Repositorio<Entidad>Falso` en memoria por entidad |
| Front | `front_catedras/{app.py,config.py,run.py}`, `servicios/api_catedras.py`, `routes/admin.py`, `templates/admin/<entidad>_{lista,form}.html` | CRUD de las 10; `<form>` traducido a PUT/DELETE |
| Docs | `docs/GUIA_ESTUDIANTE.md`, `docs/RESUMEN_SOLID_ACID.md` | Entregables de la Fase 8 |

> Verifica cada nombre con `ls` antes de citarlo: esta tabla describe la
> estructura, no sustituye mirar el repositorio.

## 7. Datos de ejemplo verificados contra `script_bdcatedras.sql` (2026-10-15)

| Para probar… | Usa | Resultado esperado |
|---|---|---|
| Lista de `Sede` (criterio 2) | `GET /api/Sede` · `?limite=2` | 3 filas (`SAN_BENITO`, `BELLO`, `VIRTUAL`); con límite, 2 |
| Obtener / 404 (criterio 3) | `SAN_BENITO` · `NO_EXISTE` | 200 "Campus San Benito" · 404 |
| `VIRTUAL` | `es_virtual: true`, `direccion: null` | Un `null` en PUT es válido |
| Ciclo de 5 verbos (criterio 4) | Crear una sede de prueba (`id_sede` ≤ 15) | `POST` 200 → `PUT` → `PATCH {activo:false}` → `GET` → `DELETE` → 2º `DELETE` **404** (en modo B: un `DELETE` lógico repetido también es 404 porque ya estaba inactiva) |
| `PUT` sin `nombre` frente a `PATCH` | Mismo body | 422 frente a 200 |
| Duplicado | `POST` con `id_sede` existente o `nombre` repetido | 500 con el mensaje del motor en `detalle` |
| `Encuesta` (criterio 7) | `GET /api/Encuesta` | `total: 1`: `id_encuesta 1`, "Calidad de la catedra abierta", `version 1`, `vigente_desde 2026-01-01`, `vigente_hasta null`, `activa true` |
| `DELETE` de `Encuesta` 1 | — | 500 en ambos modos: `respuesta_encuesta` (62 filas) la bloquea con `RESTRICT`; sus 4 `pregunta` irían en `CASCADE`, 3 `sesion` en `SET NULL`. **No uses la encuesta 1 para pruebas de borrado; crea otra** |
| `EstadoProceso` (criterio 8) | `GET` | 6: `GENERADO`, `EN_COLA`, `EN_CURSO` (final=false) · `ERROR` (final) · `CORRECTO` (final+exitoso) · `INCORRECTO` (final) |
| Coherencia | `POST {es_exitoso:true, es_final:false}` | 400 |
| `Modalidad` (criterio 9) | `GET` | 4: `PRESENCIAL` (QR), `VIRTUAL` (ENLACE), `TELEPRESENCIAL` (ENLACE), `HIBRIDA` (AMBOS) |
| Lista cerrada | `POST {canal_difusion:"FAX"}` | 422 |
| `PeriodoAcademico` | `GET` | 4: `2025-1`, `2025-2`, `2026-1`, `2026-2` |
| Formato / fechas | `id_periodo: "2026-4"` · `fecha_fin <= fecha_ini` | 422 · 400 |
| `Rol` | `GET` | 3: `ADMIN`, `COORDINADOR`, `CONSULTA` |
| `TipoDocumento` | `GET` | 6: `CC`, `TI`, `CE`, `PA`, `PEP`, `NIT` |
| `TipoEvento` | `GET` | 8: `CAAB`, `DLLH`, `DEPR`, `SLDI`, `ARCU`, **`'MTG '` (con espacio final: `CHAR(4)`)**, `DIIB`, `EIIC` |
| `TipoPregunta` | `GET` | 4: `ESCALA_5`, `SI_NO` (numero) · `TEXTO_LIBRE` (texto) · `OPCION_UNICA` (opción) |
| Regla de los 3 flags | `POST` con las tres en `false` o dos en `true` | 400 |
| `TipoVinculacion` | `GET` | 7: `ESTUDIANTE`, `DOCENTE`, `ADMINISTRATIVO`, `EGRESADO`, `RUTAS_PAZ`, `COLEGIO`, `EXTERNO` |
| `es_interno` sin `DEFAULT` | `POST` sin él | 422 |

**Borrados en modo A (físico) según FK entrantes del script** — qué filas se
pueden borrar y cuáles las bloquea `RESTRICT`:

| Tabla | Libres (200) | Bloqueadas (500) y por qué |
|---|---|---|
| `sede` | solo sedes creadas por ti | las 3 de siembra: `sesion` las usa (5712 / 1 / 1) |
| `modalidad` | `TELEPRESENCIAL`, `HIBRIDA` | `PRESENCIAL` (5713 sesiones), `VIRTUAL` (1) |
| `periodo_academico` | `2025-1`, `2025-2` | `2026-1` (5711 sesiones y 15349 `programa_asistente`), `2026-2` (3 sesiones) |
| `tipo_evento` | `'MTG '` | `CAAB`, `DEPR`, `DLLH`, `ARCU`, `SLDI`, `DIIB`, `EIIC` (en uso por `catedra`) |
| `tipo_documento` | `TI`, `CE`, `PA`, `PEP`, `NIT` | `CC` (15517 `documento_asistente`) |
| `tipo_vinculacion` | `DOCENTE`, `EGRESADO`, `COLEGIO` | `ESTUDIANTE`, `ADMINISTRATIVO`, `RUTAS_PAZ`, `EXTERNO` |
| `estado_proceso` | `EN_COLA`, `EN_CURSO`, `ERROR`, `INCORRECTO` | `GENERADO`, `CORRECTO` (`lote_migracion`) |
| `rol` | `COORDINADOR` | `ADMIN`, `CONSULTA` (`rol_por_usuario`) |
| `tipo_pregunta` | `OPCION_UNICA` | `ESCALA_5`, `SI_NO`, `TEXTO_LIBRE` (`pregunta`) |

> En **modo B** las seis tablas con `activo` responden 200 incluso a las
> "bloqueadas" (el borrado es `UPDATE`, no toca la FK). Es el comportamiento
> esperado del retrofit, no un bug. Y recuerda: **si una prueba modifica
> datos, restablece con `docker compose down -v && docker compose up -d --build`.**

## 8. Fases con prompts listos

> El código de las Fases 0-13 **ya está escrito**. Si reconstruyes desde
> cero (modo A), usa los prompts tal cual; si solo verificas, salta a
> "Terminado cuando…". Las fases 2, 5 y 6 son **N/A** en la v1 (otros
> motores, cambio de motor, devcontainer): dilo y sigue.

### Fases 0 y 1 — Repositorio y base de datos (dada)

```text
Fases 0-1 de docs/spec_kit/Versiones/version1/8_tasks.md.
Fase 0: git init, .gitignore (bin/, obj/, .vs/), README, carpetas db/postgres y
docs/spec_kit. Fase 1: copia el script PROVISTO script_bdcatedras.sql a
db/postgres/ SIN modificarlo y crea el servicio postgres en docker-compose.yml
(postgres:16-alpine, volumen pgdata, script en /docker-entrypoint-initdb.d/,
puerto 15542->5432, healthcheck pg_isready -U sa -d bdcatedras_postgres_local).
No inventes datos semilla ni toques el esquema.
```

**Terminado cuando:** `docker compose up -d postgres` y, conectado a
`localhost:15542` (`sa` / `Construccion123!`), el esquema `catedras` tiene
**37 tablas** y `sede` tiene 3 filas.

**Errores típicos:** editar el script (Art. 5); esperar que un cambio al
script se aplique sin `down -v`; olvidar el healthcheck (la API arranca antes
que la BD).

### Fase 3 — Capas de `Sede` (la rebanada vertical)

```text
Fase 3 (RF1-RF7, criterios 2-5). Crea el .csproj (SDK Web, .NET 10, solo Npgsql,
Dapper, Swashbuckle.AspNetCore), Modelos/Sede.cs, Peticiones/Sede{Crear,
Reemplazo,Actualizar}.cs, IRepositorioSede + RepositorioSedePostgres,
IServicioSede + ServicioSede, SedeController (6 endpoints), Excepciones/
NoEncontradoExcepcion.cs y Program.cs (DI, 422 personalizado, GET /, Swagger,
MatchNamesWithUnderscores). MODO A: SQL a mano parametrizado, lista blanca en
el SET del UPDATE. NO dupliques en la API lo que ya garantizan pk_sede y
uq_sede_nombre: un choque se propaga como 500 con el mensaje del motor.
Servicio: limite > 0, PATCH sin campos -> 400, no existe -> 404.
Comentarios en español, línea a línea, con la definición real de la tabla.
```

**Terminado cuando:** criterios 2-5 del spec: `GET` devuelve las 3 sedes
(`?limite=2` → 2), `GET /api/Sede/NO_EXISTE` → 404, el ciclo de los 5 verbos
cierra con un segundo `DELETE` → 404, `PUT` sin `nombre` → 422 y el mismo
body en `PATCH` → 200, `es_virtual: "texto"` → 422, duplicado → 500.

**Errores típicos:** declarar `EsVirtual` como `bool` (un campo ausente se
vuelve `false` y no hay 422): debe ser `bool?`; devolver 200 con lista vacía
en vez de 204; olvidar `LIMIT @limite`; concatenar valores en el SQL (RNF3).

### Fase 4 — Integración en compose

```text
Fase 4 (criterio 1). Agrega al docker-compose.yml el servicio api (8142->8042):
Dockerfile propio con imagen SDK + dotnet watch, volumen ./api_catedras:/app con
volúmenes anónimos para bin/ y obj/, restart unless-stopped, depends_on postgres
con condition: service_healthy y ConnectionStrings__Postgres. Comenta en el
compose por qué v1 solo tiene api + postgres (D8).
```

**Terminado cuando:** un solo `docker compose up -d --build` deja todo; `GET
http://localhost:8142/` devuelve el JSON de diagnóstico; `/swagger` muestra
`Sede`; guardar un `.cs` recarga sin reconstruir la imagen.

**Errores típicos:** imagen `runtime` en vez de `sdk` (no hay `dotnet watch`);
no aislar `bin/obj` (mezcla binarios de Windows y Linux).

### Fases 7 y 8 — Persistencia, reset y documentación

```text
Fases 7-8. Dame el guion de prueba de persistencia (crear -> down -> up ->
sigue; down -v -> up --build -> solo las 3 sedes) y redacta docs/GUIA_ESTUDIANTE.md
(instalación, primer recorrido por /swagger, comandos del día a día) y
docs/RESUMEN_SOLID_ACID.md (dónde vive cada principio SOLID y cada letra de
ACID en el código de la v1).
```

**Terminado cuando:** el guion funciona en tu máquina y los dos documentos
existen.

### Fase 9 — Prueba de capas (criterio 6)

```text
Fase 9. Crea api_catedras/pruebas/PruebaCapas.csproj y Programa.cs (top-level
statements, sin librerías de prueba): ServicioSede con un RepositorioSedeFalso en
memoria, sin PostgreSQL. 10 verificaciones: CRUD, limite <= 0, PATCH vacío y
404. Sale con código 0 solo si todo pasa.
```

**Terminado cuando:** `docker compose exec api dotnet run --project pruebas`
termina con `0 fallidas` y código 0 (el spec registró **10** en esta fase).

**Errores típicos:** que la prueba abra una conexión real; usar el mismo
nombre de variable en dos secciones (todas comparten ámbito).

### Fase 10 — Capas de `Encuesta`, `EstadoProceso`, `Modalidad`

```text
Fase 10 (RF8-RF25, criterios 7-9). Lee 3_plan.md §4.7, §4.8 y §4.9 ANTES.
Encuesta: PK IDENTITY, EncuestaCrear sin IdEncuesta, CrearAsync -> Task<long> con
RETURNING, ruta con long, ManejadorTipoDateOnly registrado en Program.cs,
ServicioEncuesta valida vigente_hasta >= vigente_desde. EstadoProceso: es_exitoso
=> es_final en el servicio. Modalidad: canal_difusion con [RegularExpression]
en la petición. Controllers idénticos al de Sede. Registra las 3 parejas en
Program.cs.
```

**Terminado cuando:** `POST /api/Encuesta` sin extras crea con los defaults y
devuelve `id_encuesta`; fechas invertidas → 400; `EstadoProceso`
`{es_exitoso:true, es_final:false}` → 400; `canal_difusion:"FAX"` → 422;
duplicados → 500; las tablas quedan con solo sus filas de siembra.

**Errores típicos:** olvidar el manejador de `DateOnly` ("can't write CLR type
System.DateOnly" / lectura nula); poner `canal_difusion` en el servicio;
olvidar registrar la pareja en `Program.cs` (500 "Unable to resolve service").

### Fase 11 — Prueba de capas extendida

```text
Fase 11. Extiende pruebas/Programa.cs con un repositorio FALSO por entidad
(Encuesta, EstadoProceso, Modalidad), mismo patrón que RepositorioSedeFalso.
RepositorioEncuestaFalso simula la IDENTITY con un contador (_siguienteId).
Prueba CRUD, cada regla de negocio, PATCH vacío y 404.
```

**Terminado cuando:** la prueba termina con `0 fallidas` (el spec registró
**45** en esta fase; hoy el total es mayor porque se sumaron v2-v7: **no lo
compares con 45**, compara con el último total anotado en `8_tasks.md` de la
versión más reciente).

### Fase 12 — Las 6 tablas restantes

```text
Fase 12 (RF26-RF61, §2.5-2.10 de 5_data_model.md). PeriodoAcademico,
Rol, TipoDocumento, TipoEvento, TipoPregunta y TipoVinculacion con el mismo molde.
Casos especiales: PeriodoAcademicoCrear con [RegularExpression("^[0-9]{4}-[1-3]$")] y
ServicioPeriodoAcademico fecha_fin > fecha_ini (estricto, reutiliza
ManejadorTipoDateOnly); TipoEventoCrear con [StringLength(4, MinimumLength = 4)];
ServicioTipoPregunta exige EXACTAMENTE una de las tres banderas; TipoVinculacion
con es_interno obligatorio sin default. REGISTRA LAS 6 PAREJAS en Program.cs y
agrega las 6 secciones a la prueba de capas.
```

**Terminado cuando:** los 6 recursos aparecen en `/swagger`; fechas invertidas
→ 400; tres banderas en `false` (o dos en `true`) → 400; `TipoVinculacion` sin
`es_interno` → 422; la prueba de capas sigue en 0 fallidas con las 10
entidades.

**Errores típicos:** código escrito pero **sin registrar** en `Program.cs`
(fue exactamente el hallazgo con `TipoPregunta` y `TipoVinculacion`: 500 hasta
la v2); recortar `'MTG '` a `'MTG'` en un dato de ejemplo (la PK es
`CHAR(4)`).

### Fase 13 — Front (`front_catedras/`)

```text
Fase 13. Flask + Jinja + Bootstrap 5, identidad USB Medellín (naranja #EF7D00,
negro #1D1D1B). servicios/api_catedras.py: cliente HTTP que traduce el sobre de
error de la API a mensajes. Blueprint admin: CRUD de las 10 entidades; los
formularios HTML traducen a PUT/DELETE (el navegador solo manda GET/POST). Sin
datos mock: si la API falla, se muestra el error. En v1 solo catálogos;
catedras/auth/ponente/estudiante/registro son placeholders. Sin Regla 2
(no hay FK).
```

**Terminado cuando:** `http://localhost:5000/admin/` permite listar, crear,
editar y eliminar las 10 entidades; los errores 400/422/500 de la API se ven
como mensajes claros; con `docker compose stop api` cada pantalla muestra "No
se pudo contactar la API".

**Errores típicos:** mostrar datos de ejemplo cuando la API cae; dejar que el
navegador mande un verbo que no es GET/POST (la ruta de Flask es la que llama
a `PUT`/`DELETE` de la API, vía `servicios/api_catedras.py`); omitir la
columna `activo`/`activa` del formulario.

### Cierre de la v1

```text
Cierre. Con el entorno recién reseteado, acompáñame a correr los 9 criterios
de aceptación de 2_spec.md §5 en orden. Para cada uno dime el comando, el
resultado esperado y qué revisar si no coincide. Al final: texto del commit en
español y recordatorio del tag v1. No escribas nada de la v2 todavía (Art. 1).
```

**Terminado cuando:** los 9 criterios pasan, las casillas de `8_tasks.md`
están marcadas con el dónde y el cuándo, y existe el tag `v1`
(`git tag --list`; **no está verificado desde esta guía**).

## 9. Trampas conocidas y diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| `POST /api/Sede` responde 500 en vez de 422 | La petición no declara bien la forma | Revisar `SedeCrear`, no la BD |
| `es_virtual: "texto"` pasa | Propiedad `bool` en vez de `bool?` | `bool?` |
| Falla un parámetro o una columna `DATE` | Dapper no mapea `DateOnly` | `ManejadorTipoDateOnly` registrado en `Program.cs` |
| (Modo B) "procedure … does not exist" / "could not determine data type" | El `CALL` no coincide en **tipos**; fecha enviada como `timestamp` | `CAST(@x AS date)`; ver `005` y `RepositorioEncuestaPostgres` |
| (Modo B) "function … is not unique" | Se cambió la firma de un SP con `CREATE OR REPLACE` sin `DROP PROCEDURE` previo (sobrecarga ambigua) | `DROP PROCEDURE IF EXISTS` con la firma anterior (ver `005`) |
| (Modo B) `DELETE /api/Sede/SAN_BENITO` → 200 | Borrado lógico (tiene `activo`) | Correcto; no devuelve 500 |
| (Modo B) La sede "borrada" no sale en la lista | `activo=false` | `?incluirInactivos=true` |
| Endpoint de `TipoPregunta`/`TipoVinculacion` → 500 "Unable to resolve service" | Pareja sin registrar en `Program.cs` | Registrarla |
| `DELETE` de un catálogo en uso → 500 `RESTRICT` | FK entrante (§7) | Correcto; usar filas libres o crear otra |
| `PATCH` con una sola fecha deja la combinación inválida y da 500 | (Modo A) Sin leer antes; lo detiene el `CHECK` | Documentado en 3_plan §4.8; no "arreglar" sin preguntar |
| `Cannot write DateTimeOffset…` | No pertenece a la v1 (llega en la v4, D21) | No introducir fechas con hora |
| Cambié el script SQL y nada cambia | Solo corre con volumen vacío | `docker compose down -v && up -d --build` |
| Puerto ocupado | 8142 / 15542 / 5000 | Cerrar el otro programa o remapear |

## 10. Lista de revisión para cualquier cambio de la v1

- [ ] Solo nombra las 10 tablas de la v1 (Art. 1, 5).
- [ ] Modo A: SQL a mano con `catedras.` y `@parametros`, lista blanca en el
      `SET` (Art. 2). Modo B: el repositorio solo hace `CALL`; el SQL está en
      `db/postgres/migraciones/`.
- [ ] Interfaz, servicio, controlador y peticiones no cambian por el retrofit.
- [ ] 3 paquetes NuGet exactos; sin fábrica multi-motor.
- [ ] Forma → petición (422); negocio → servicio (400); no existe → 404;
      motor → 500 (Art. 8).
- [ ] `bool?` en booleanos de petición; `[RegularExpression]` para listas
      cerradas; longitud fija solo en `tipo_evento`.
- [ ] `Encuesta`: `Task<long> CrearAsync`, sin `IdEncuesta` en la petición.
- [ ] Cada entidad nueva está **registrada en `Program.cs`** y tiene sección
      en la prueba de capas.
- [ ] Front: sin mock, `PUT`/`DELETE` vía formulario, errores de la API
      visibles.
- [ ] Comentarios en español que explican el porqué (Art. 6).
- [ ] `8_tasks.md` actualizado con lo verificado **y dónde**.

## 11. Lo que la v1 le deja a la v2 (no construir todavía)

- Las tablas **con** FK saliente: `facultad`, `dependencia`,
  `programa_academico`, `alias_programa`, `catedra`. La v2 introduce la
  **Regla 2** de la guía IA (FK = `<select>`) y su primer SP de
  maestro-detalle (Regla 1).
- Paginación de catálogos en el front (la v2 la agrega: ADR D13).
- **Pendientes de cierre formal de la v1** (documentados, no resueltos aquí):
  `0_mapa_versiones.md` y `ESTADO_V6_Y_CUMPLIMIENTO.md` la marcan como "no
  cerrada" aunque la Fase 12 esté en ✅; faltan por unificar los textos
  desactualizados de `2_spec.md` ("6 pendientes") y `7_quickstart.md` (front
  "aparte del compose"); y la comprobación del tag `v1`. Si la IA los
  encuentra, debe **reportarlos**, no corregirlos en silencio.
- La spec de la v2 se escribe **después** del tag `v1` (Artículo 1).
