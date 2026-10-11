# Guía IA — Versión 5: usuarios, login y registro de asistencia

> **Qué es este documento.** La guía operativa para construir, revisar o
> verificar la **v5** con un asistente de IA (Claude, Copilot, ChatGPT…),
> siguiendo el spec kit de esta versión. **No crea reglas nuevas**: aterriza
> a la v5 las que ya existen.
>
> **Jerarquía ante un conflicto:**
> [constitución](../../1_constitution.md) › [guía IA general](../../guia_ia.md)
> (sus 3 reglas) › spec kit v5 (`2_spec.md` … `8_tasks.md`) › esta guía.
> Si esta guía contradice al spec kit, gana el spec kit y se corrige esta
> guía.
>
> **Estado de la v5 (2026-10-11):** spec kit revisado contra la BD real y
> contra la v4 cerrada (tag `v4`); **todavía sin construir** — ninguna casilla
> de `8_tasks.md` está marcada. Esta guía describe cómo construirla.

---

## 0. Cómo usar esta guía

1. Abre una conversación nueva con tu asistente y pega el **prompt de
   arranque** (§1). Adjunta o da acceso al repositorio.
2. Trabaja **una fase a la vez** (§8, Fases 38-46). Cada fase trae un
   prompt listo para copiar, la lista de "terminado cuando…" y los errores
   típicos.
3. No marques una casilla de `8_tasks.md` hasta que la verificación de la
   fase pase **en tu máquina, contra Docker** (Artículo 1). La IA puede
   escribir código; quien verifica eres tú.
4. Si la IA propone algo de la lista de §5 o §9 ("lo que la IA tiende a
   arreglar mal"), recházalo citando el ADR.
5. Esta es la versión con **más decisiones de diseño nuevas** del
   proyecto (8 ADR). Léela entera una vez antes de la Fase 38.

## 1. Prompt de arranque (cópialo tal cual)

```text
Vas a trabajar en el Proyecto Cátedras, VERSIÓN 5 (usuarios, login y
registro de asistencia). Es material de estudio: todo en español y con
comentarios línea a línea.

Antes de escribir código, lee EN ESTE ORDEN y confirma con un resumen de
5 líneas que lo entendiste:
  1. docs/spec_kit/1_constitution.md          (reglas permanentes, ganan siempre)
  2. docs/spec_kit/guia_ia.md                 (Reglas 1-3; la Regla 3, BCrypt, se aplica por primera vez)
  3. docs/spec_kit/Versiones/0_mapa_versiones.md (qué tablas puede nombrar cada versión)
  4. docs/spec_kit/Versiones/version5/2_spec.md → 8_tasks.md (en orden numérico)
  5. docs/spec_kit/Versiones/version5/guia_ia_v5.md (esta guía)

Reglas que no se negocian en la v5:
  - Solo puedes nombrar tablas de v1-v5. usuario, rol_por_usuario,
    enlace_registro, clave_acceso y registro_asistencia son las nuevas.
    pregunta, opcion_pregunta, respuesta_* (v6) y lote_migracion,
    detalle_migracion, bitacora, parametro (v7) NO se consultan ni se
    exponen, aunque la BD ya las tenga (las funciones SQL leen parametro
    por dentro: es un detalle de la BD, no de la API).
  - C# + ASP.NET Core (.NET 10) + Dapper, SQL a mano y SIEMPRE con
    @parametros. Sin Entity Framework. El ÚNICO paquete nuevo permitido es
    BCrypt.Net-Next 4.0.3 (RNF14). Sin SP nuevo ni migración nueva.
  - Capas: Controller -> IServicio -> IRepositorio -> RepositorioXPostgres.
    Solo Program.cs conoce clases concretas.
  - BCrypt (HashPassword / Verify) se llama SOLO desde ServicioUsuario
    (ADR D23, RNF13). Ni el controlador ni el repositorio ven la clave en
    texto plano. Usuario.ClaveHash lleva [JsonIgnore] en el MODELO.
  - EnlaceRegistro y ClaveAcceso NO son CRUD (ADR D22, D24): los repositorios
    llaman SELECT * FROM catedras.fn_emitir_enlace_api / fn_solicitar_clave_api /
    fn_validar_clave_api. Prohibido reimplementar ventanas, cupos, intentos,
    expiración o el hash de la clave en C#.
  - Las excepciones de negocio de la BD caen en el catch de NpgsqlException
    y responden 500 (ADR D25). No inventes un catch por SqlState.
  - El login responde 401 con el MISMO mensaje genérico para usuario
    inexistente, inactivo o clave incorrecta (ADR D27).
  - RegistroAsistencia POST directo: origen solo MANUAL/IMPORTADO, validado
    en el SERVICIO -> 400 (ADR D26). Sin [RegularExpression] en la petición.
  - Fechas: DateTime UTC en los modelos, nunca DateTimeOffset (ADR D21 de v4).
  - La API NO autoriza por rol (ADR D28). La protección de /admin es del FRONT.
  - JSON en snake_case, como v1-v4 (fk_sesion, id_clave, user_agent): cada
    propiedad lleva [JsonPropertyName]. Los parámetros de query van en
    camelCase (?fkSesion=) y los de ruta ({idEnlace}). Nada de camelCase en bodies.
  - Contratos exactos de 6_contracts.md (sobres, códigos 400/401/404/422/500).

Trabajaremos fase por fase (Fases 38-46 de 8_tasks.md). No avances a la
siguiente fase hasta que te confirme que la verificación pasó.
```

## 2. Orden de lectura y qué sacar de cada documento

| # | Documento | Qué debe extraer la IA |
|---|---|---|
| 1 | `1_constitution.md` | Art. 1 (por versiones, YAGNI), Art. 2 (Dapper, SQL a mano; **excepción**: un paquete nuevo si la spec lo pide con razón real), Art. 3 (capas), Art. 5 (la BD ya existe: no se toca el script), Art. 6 (español, comentarios), Art. 7 (contratos), Art. 8 (códigos) |
| 2 | `guia_ia.md` | **Regla 3** (BCrypt en el servicio, `Verify`, nunca comparar hashes a mano) por primera vez; Regla 2 (FK = `<select>`); Regla 1 no aplica |
| 3 | `0_mapa_versiones.md` | Fila V5: las 5 tablas, sus FK hacia v1-v4 y la salvedad de la FK circular `enlace_registro.fk_usuario_crea` |
| 4 | `2_spec.md` | RF132-RF158, RNF12-RNF14 y los **10 criterios de aceptación** (la definición de "terminado") |
| 5 | `3_plan.md` | Estructura de archivos, `SELECT * FROM fn_..._api(...)`, BCrypt en el servicio, front (`auth.py`, `registro.py`, `before_request`) |
| 6 | `4_research.md` | ADR D22-D29 (el **porqué**) y la sección «Ajustes al borrador» (qué se corrigió contra la BD real) |
| 7 | `5_data_model.md` | Columnas, `CHECK`, FK, mapeo de `inet`/rango/`bytea` y **datos de ejemplo útiles** |
| 8 | `6_contracts.md` | Peticiones/respuestas de los 21 endpoints, el 401, contrato del front |
| 9 | `7_quickstart.md` | Smoke test de 9 pasos + front + problemas frecuentes |
| 10 | `8_tasks.md` | Fases 38-46 y sus verificaciones |

## 3. La v5 en una página

- **Qué agrega:** 5 tablas (`usuario`, `rol_por_usuario`, `enlace_registro`,
  `clave_acceso`, `registro_asistencia`), RF132-RF158, RNF12-RNF14 y 8 ADR
  (D22-D29). La API pasa de 24 a **29** recursos y suma **21 endpoints**
  (Usuario 7 con login, RolPorUsuario 3, EnlaceRegistro 4, ClaveAcceso 4,
  RegistroAsistencia 3).
- **Qué NO agrega:** ningún SP ni migración (`db/postgres/migraciones/` no
  cambia), ningún servicio en `docker-compose.yml`, ningún paquete de
  Python. Solo `BCrypt.Net-Next 4.0.3` en `ApiCatedras.csproj`.
- **Lo que enseña:** una contraseña bien guardada (BCrypt, `[JsonIgnore]`),
  el primer `401`, llamar **funciones** SQL con `SELECT * FROM` (no `CALL`),
  por qué algunos recursos no son CRUD sino verbos de negocio, y una sesión
  de Flask real en el front.

```
usuario ──< rol_por_usuario >── rol (v1)           (login de PERSONAL, BCrypt)
   └──> asistente (v3)   uq_usuario_persona: máximo 1 usuario por persona

sesion (v4) ──< enlace_registro ──< clave_acceso >── asistente (v3)
                     │                    │          (auto-registro, clave de UN uso, sha256)
                     └────────────────────┴──< registro_asistencia
                                                (snapshots de vinculacion_asistente / programa_asistente)
```

Dos mecanismos que **no se mezclan** (ADR D29): `usuario`+BCrypt para el
personal que administra; `enlace_registro`+`clave_acceso` para el asistente
que se registra una vez en una sesión.

## 4. Reglas permanentes aterrizadas a la v5

| Regla | En la v5 significa… |
|---|---|
| Art. 1 — no anticipar | Nada de encuestas respondidas (v6), importaciones, bitácora ni parámetros (v7). Aunque `fn_validar_clave_api` lea `parametro`, esa tabla no se expone |
| Art. 2 — SQL a mano, 1 paquete nuevo | SQL con prefijo `catedras.` y `@parametros`. `BCrypt.Net-Next` es la única excepción y solo se usa en `ServicioUsuario` |
| Art. 3 — capas | Hash y `Verify` en el **servicio**; `SELECT * FROM fn_…` en el **repositorio**; traducir `null` de `LoginAsync` a 401 en el **controlador** |
| Art. 5 — BD dada | Las 5 tablas, los 3 `fn_*_api` y los 3 SP ya están en `script_bdcatedras.sql`. No se toca el script, no se crea migración |
| Art. 6 — comentarios | Cabecera con la definición real de la tabla; en `ServicioUsuario`, `ServicioRegistroAsistencia` y los repositorios de funciones, comentar el *porqué* (D23, D26, D24) |
| Art. 7 — contratos | `POST /api/Usuario/login` → `{estado, usuario, roles}`; `POST /solicitar` → `{estado, mensaje, id_clave, clave, enviadoA}`; `PATCH …/revocar` reusa `filasEliminadas` |
| Art. 8 — códigos | 422 forma · 400 negocio del servicio · **401 solo el login** · 404 no existe (lo lanza el servicio) · 500 cualquier error de la BD, incluidos los `RAISE EXCEPTION` de negocio |
| Guía IA, Regla 3 | **Aplica por primera vez**: `usuario.clave_hash` con BCrypt, `Verify` para comparar |
| Guía IA, Regla 2 | `fk_usuario`/`fk_rol` por `<select>` o fijados por URL; `fk_sesion` del enlace por `<select>` de sesiones o fijado por URL; `fk_asistente` por búsqueda (`?q=`) + `<select>` |
| Guía IA, Regla 1 | No aplica: ninguna de las 5 tablas se guarda con SP maestro-detalle |

## 5. Las decisiones que la IA tiende a "arreglar" mal

### D23 — BCrypt solo en el servicio, y el hash nunca sale

```csharp
// ✗ MAL: el controlador o el repositorio hashean / comparan.
//   (rompe RNF13 y la Regla 3, y duplica la lógica)
usuario.ClaveHash = BCrypt.Net.BCrypt.HashPassword(clave);   // en el repositorio
if (usuario.ClaveHash == BCrypt.Net.BCrypt.HashPassword(clave)) ...   // ✗✗ nunca se compara a mano

// ✓ BIEN: solo ServicioUsuario; el repositorio trata ClaveHash como cadena opaca.
ClaveHash = BCrypt.Net.BCrypt.HashPassword(peticion.Clave!),           // CrearAsync / ReemplazarAsync
!BCrypt.Net.BCrypt.Verify(peticion.Clave, usuario.ClaveHash)            // LoginAsync
```

```csharp
// ✗ MAL: crear un UsuarioRespuesta aparte, o devolver el hash "por comodidad".
// ✓ BIEN: [JsonIgnore] en el MODELO (no en la petición: el cliente SÍ manda `clave`).
[JsonIgnore] public required string ClaveHash { get; set; }
```

Detalles que la IA suele olvidar: en `PATCH`, si `clave` no viene, **no se
toca el hash** (se comprueba volviendo a loguearse con la clave original);
`ClaveAcceso.ClaveHash` (`bytea`, `byte[]`) también lleva `[JsonIgnore]`.

### D24 — `EnlaceRegistro` y `ClaveAcceso`: funciones, no `INSERT`

```csharp
// ✗ MAL: INSERT directo en enlace_registro / clave_acceso, generar el token
//   o la clave de 6 dígitos en C#, calcular la ventana, contar intentos.
await conexion.ExecuteAsync("INSERT INTO catedras.enlace_registro ...", enlace);

// ✓ BIEN: la BD ya trae las reglas; el repositorio solo llama y lee.
const string sql = "SELECT * FROM catedras.fn_emitir_enlace_api(@FkSesion, @MinutosAntes, @MinutosDespues, @FkUsuario);";
// `SELECT * FROM` porque es una FUNCTION que devuelve datos; `CALL` es solo para PROCEDURE.
```

Consecuencias: el cliente **nunca** manda `canal`, `token`, `ventana` ni
`url_publica` (los pone la BD; `PRESENCIAL` → `QR`); no hay `POST` plano,
`PUT`, `PATCH` genérico ni `DELETE` (esas rutas dan 404 de ASP.NET); emitir
un enlace nuevo para una sesión **revoca el anterior** (`RN-14`) y no es un
error.

### D22 — `EnlaceRegistro` tiene `id` y aun así no se borra

| | `VinculacionAsistente` / `RolPorUsuario` (D16) | `Ponencia` (D19) | `EnlaceRegistro` (D22) |
|---|---|---|---|
| ¿Tiene `id`? | No | No | **Sí** (`id_enlace`) |
| ¿Cómo se "termina"? | `PATCH /cerrar` con la llave en el body | `DELETE` con 3 segmentos | `PATCH /{id}/revocar` |
| ¿`DELETE`? | No | Sí | **No** |

El `revocar` filtra `AND revocado_en IS NULL`: el segundo intento afecta 0
filas y el servicio lo traduce a **400**; antes confirma que el enlace
existe (**404**).

### D25 — Un solo 500 para todo lo que diga la BD

```csharp
// ✗ MAL: "no se encontró el asistente" -> 404, "clave expirada" -> 410,
//   mapeando SqlState a códigos nuevos.
catch (PostgresException ex) when (ex.SqlState == "P0002") { return NotFound(...); }

// ✓ BIEN: el catch de siempre. El 404 SOLO lo lanza el servicio tras un
//   ObtenerPorIdAsync que devolvió null.
catch (NpgsqlException ex) { ... 500 ... }
```

Identificador no resuelto, enlace revocado/fuera de ventana, clave
incorrecta/usada/expirada, sesión sin cupo, asistente sin vinculación
vigente: **todos 500**. El texto de la excepción dice cuál fue.

### D26 — `origen` del registro manual: 400 desde el servicio

```csharp
// ✗ MAL: [RegularExpression("^(MANUAL|IMPORTADO)$")] en RegistroAsistenciaCrear.Origen
//   -> `origen: "QR"` da 422 y el servicio ni se entera (contradice RF156 / criterio 6).
// ✗ MAL: confiar en el CHECK chk_registro_clave -> deja pasar origen QR sin clave
//   (una mentira de auditoría silenciosa).

// ✓ BIEN: petición solo [Required]; el servicio, ANTES del repositorio:
if (peticion.Origen is not ("MANUAL" or "IMPORTADO"))
    throw new ArgumentException("El origen debe ser MANUAL o IMPORTADO.");   // -> 400
```

`fk_clave`, `fk_enlace` y los dos *snapshots* no existen en la petición: el
trigger `fn_trg_registro_validar` calcula los snapshots igual que en la vía
del QR.

### D27 — 401 genérico

```csharp
// ✗ MAL: distinguir causas ("usuario no existe", "cuenta inactiva", "clave mala"),
//   o devolver 404 / 400.
// ✓ BIEN: LoginAsync devuelve null en las 3 causas; el controlador:
return Unauthorized(new { estado = "no_autorizado", mensaje = "Usuario o clave incorrectos" });
```

Una cuenta `activo: false` responde 401 aunque la clave sea correcta (a
propósito). El éxito actualiza `ultimo_acceso` y devuelve solo los roles
**vigentes** (`fecha_fin IS NULL`).

### D28 y D29 — Autenticar sí, autorizar no (en la API)

La API sigue abierta: no hay middleware por rol. La sesión vive en **Flask**
(firmada con `SECRET_KEY`) y el `before_request` de `admin_bp` es el
**único** lugar donde se exige rol `ADMIN` (sin sesión → `/login`; sin rol →
403). No lo "completes" agregando `[Authorize]` en la API.

### RF153 — Límite conocido: los intentos no se acumulan

`sp_validar_clave_y_registrar` hace `UPDATE intentos + 1` y luego
`RAISE EXCEPTION`; la excepción deshace el `UPDATE`. Resultado: `intentos`
se queda en 0 y la clave **no** se anula tras 3 fallos (vale sus 10
minutos). **No lo corrijas**: el Artículo 5 prohíbe tocar el script y
reimplementarlo en C# viola D24. Se **comprueba** en la Fase 44; si
`intentos` sí subiera, se revierte ese punto de «Ajustes al borrador».

## 6. Mapa de archivos de la v5

| Capa | Archivo | Nota |
|---|---|---|
| Proyecto | `api_catedras/ApiCatedras.csproj` | + `BCrypt.Net-Next 4.0.3`. `PruebaCapas.csproj` lo hereda por `ProjectReference` |
| Modelo | `Modelos/Usuario.cs` | 7 propiedades; `ClaveHash` `[JsonIgnore]`; `usuario` → `NombreUsuario` con `[JsonPropertyName("usuario")]`; `DateTime` UTC |
| Modelo | `Modelos/RolPorUsuario.cs` | 4 propiedades, **sin `Id`** (D16) |
| Modelo | `Modelos/EnlaceRegistro.cs` | `VentanaDesde`/`VentanaHasta` planas (`lower(ventana)`/`upper(ventana)`) |
| Modelo | `Modelos/ClaveAcceso.cs` | `ClaveHash` (`byte[]`) `[JsonIgnore]`; `ip_solicitud` por `host()` |
| Modelo | `Modelos/RegistroAsistencia.cs` | Snapshots sin `required` (los pone el trigger); `ip` por `host(ip)` |
| Petición | `Peticiones/Usuario{Crear,Reemplazo,Actualizar}.cs`, `UsuarioLogin.cs` | `Clave` `[MinLength(8)]`; en `Actualizar`, `string?` |
| Petición | `Peticiones/RolPorUsuario{Crear,Cerrar}.cs` | Sin Reemplazo/Actualizar |
| Petición | `Peticiones/EnlaceRegistroEmitir.cs` | **Única** de la entidad |
| Petición | `Peticiones/ClaveAccesoSolicitar.cs`, `ClaveAccesoValidar.cs` | **Únicas**; `ip` como `string?` |
| Petición | `Peticiones/RegistroAsistenciaCrear.cs` | **Única**; `Origen` solo `[Required]` |
| Repositorio | `IRepositorioUsuario` + `…Postgres` | CRUD + `ObtenerPorLoginAsync` + `ActualizarUltimoAccesoAsync`; sin BCrypt |
| Repositorio | `IRepositorioRolPorUsuario` + `…Postgres` | `ObtenerTodosAsync`, `CrearAsync`, `CerrarAsync` + `ObtenerVigentesAsync(fkUsuario)` |
| Repositorio | `IRepositorioEnlaceRegistro` + `…Postgres` | `EmitirAsync` (función), `RevocarAsync` |
| Repositorio | `IRepositorioClaveAcceso` + `…Postgres` | `SolicitarAsync`, `ValidarAsync` (funciones) |
| Repositorio | `IRepositorioRegistroAsistencia` + `…Postgres` | `INSERT` directo sin `fk_clave`/`fk_enlace` |
| Servicio | `IServicioUsuario` + `ServicioUsuario` | **Único** archivo con `BCrypt`; `LoginAsync` → `(Usuario, roles)?` |
| Servicio | `…RolPorUsuario`, `…EnlaceRegistro`, `…ClaveAcceso`, `…RegistroAsistencia` | Existencia (404), ya cerrado/revocado (400), `origen` (400), `IPAddress.TryParse` (400) |
| Controlador | `UsuarioController` (6 + `login`), `RolPorUsuarioController` (3), `EnlaceRegistroController` (4), `ClaveAccesoController` (4), `RegistroAsistenciaController` (3) | 21 endpoints en total |
| Ensamblador | `Program.cs` | 5 parejas nuevas; diagnóstico `version = "v5"` |
| Pruebas | `pruebas/Programa.cs` | Repos falsos de las 5 entidades; `BCrypt.Verify` real |
| Front | `routes/auth.py`, `templates/login.html` | Login/logout con sesión de Flask |
| Front | `auth_helpers.py` | Lee la sesión real; `rol_requerido('ADMIN')` |
| Front | `routes/registro.py`, `templates/registro_solicitar.html`, `registro_validar.html` | Pantalla **pública**: `/registro`, `/registro/<token>`, `/registro/<token>/validar` |
| Front | `routes/admin.py` (+ `before_request`), `templates/admin/…`, `panel.html`, `partials/navbar.html` | 5 bloques nuevos; navbar con usuario y «Cerrar sesión» |

## 7. Datos de ejemplo verificados contra la BD (2026-10-08)

| Para probar… | Usa | Resultado esperado |
|---|---|---|
| Conteos de partida | tablas recién creadas | `usuario` 1 · `rol_por_usuario` 3 · `enlace_registro` 9 · `clave_acceso` 167 · `registro_asistencia` **168** |
| Cuenta de ejemplo | `admin.catedras` (id 1, asistente 14809, roles `ADMIN` y `CONSULTA` vigentes) | Su clave **no se conoce**: fijarla con `PATCH /api/Usuario/1 {"clave":"Admin12345"}`; **repetir tras cada `down -v`** |
| Crear usuario nuevo | `fkAsistente: 100` (sin `usuario`) | 200, sin campo de clave en la respuesta ni en el `GET` |
| Login | `admin.catedras` / `Admin12345` | 200 con `ADMIN`, `CONSULTA`; clave mala o cuenta inactiva → 401 |
| Emitir enlace | **sesión nueva** con `inicio`/`fin` que rodeen "ahora" (±2 h), `fk_catedra` 5498, `PRESENCIAL`, `SAN_BENITO`, `2026-2` | `canal: "QR"`; ventana `[inicio−30 min, fin+30 min)` |
| ⚠ No usar para solicitar | enlaces sembrados | **Todos vencieron en agosto de 2026** (RN-15 → 500) |
| Solicitar clave | identificador `1000090` (= asistente 1; también el asistente 2) | `clave` de 6 dígitos; vale **10 minutos** |
| Validar | esa clave, enseguida | 200 con `id_registro`, `origen: "QR"`; repetir → 500 (RN-20) |
| Registro manual | asistente 2 (distinto del que se registró por clave: `uq_registro_sesion_asis`) | `QR` → 400 · `MANUAL` → 200 |
| `RESTRICT` (criterio 7) | `DELETE /api/Asistente/1` (ya tiene `registro_asistencia`) | 500; `GET /api/DocumentoAsistente?fkAsistente=1` sigue intacto. *(En v4 el 1 no servía para RF131; aquí es justo el que se necesita)* |
| Límite RF153 | clave pedida y sin usar, 4 `validar` con clave mala | 500 cada uno; `GET /api/ClaveAcceso/{id}` → `intentos: 0`; la clave buena aún entra |

Después de cualquier prueba que modifique datos:
`docker compose down -v && docker compose up -d --build` (y volver a fijar la
clave del administrador).

## 8. Fases con prompts listos

> **Ninguna fase de la v5 está hecha todavía.** Cada prompt es para
> construirla; la sección "Terminado cuando…" es lo que debes comprobar tú.

### Fase 38 — Paquete NuGet

```text
Fase 38 de docs/spec_kit/Versiones/version5/8_tasks.md (RNF13-RNF14).
Agrega SOLO <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" /> a
ApiCatedras.csproj. No toques ningún otro PackageReference. No escribas más
código en esta fase.
```

**Terminado cuando:** `dotnet restore` y `dotnet build` pasan sin
advertencias nuevas; un `using BCrypt.Net;` temporal resuelve
`HashPassword`/`Verify` (bórralo después); `PruebaCapas.csproj` compila sin
declarar el paquete por su cuenta.

**Errores típicos:** agregar otros paquetes "de paso"; subir la versión sin
decirlo.

### Fase 39 — Capas de `Usuario` (con login)

```text
Fase 39 (RF132-RF139, ADR D23 y D27). Lee D23 y D27 ANTES de empezar.
Modelo Usuario con ClaveHash [JsonIgnore]. Peticiones UsuarioCrear/Reemplazo/
Actualizar/Login (Clave [MinLength(8)], SIN [JsonIgnore]; en Actualizar
Clave es string?). IRepositorioUsuario con CRUD + ObtenerPorLoginAsync +
ActualizarUltimoAccesoAsync, SIN llamar a BCrypt. ServicioUsuario es el
ÚNICO lugar con BCrypt.HashPassword / BCrypt.Verify; si PATCH no trae clave
no se toca el hash. LoginAsync devuelve null si usuario inexistente, inactivo
o clave mala (sin distinguir). UsuarioController: 6 endpoints + POST login
(null -> 401 con mensaje genérico). Registra la pareja en Program.cs.
```

> **Orden práctico:** `ServicioUsuario` inyecta `IRepositorioRolPorUsuario`
> (para los roles vigentes). Crea primero la **interfaz** (y su
> implementación) de la Fase 40, o el proyecto no compila.

**Terminado cuando:** `POST /api/Usuario` → 200 sin campo de clave, y un
`GET` posterior tampoco lo trae; `login` correcto → 200 con roles; clave
mala → 401; `activo: false` → 401; `PATCH` sin `clave` y luego login con la
clave original → 200; `usuario` o `fk_asistente` duplicados → 500.

**Errores típicos:** poner `[JsonIgnore]` en la petición (el cliente no
podría mandar la clave); hashear en el repositorio; usar `==` entre hashes;
un mensaje 401 distinto por causa.

### Fase 40 — Capas de `RolPorUsuario`

```text
Fase 40 (RF140-RF142, ADR D16 reutilizado de v3). Copia la FORMA de
VinculacionAsistente: llave compuesta (fk_usuario, fk_rol, fecha_ini), sin
Id, sin PUT, sin DELETE. 3 endpoints: GET ?fkUsuario=, POST (abrir), PATCH
/cerrar (llave en el body + fecha_fin). Añade ObtenerVigentesAsync(fkUsuario)
-> List<string> para el login. Cerrar: 404 si no existe, 400 si ya está cerrada.
```

**Terminado cuando:** abrir → 200; `PATCH /cerrar` → 200; repetir → 400;
`/api/RolPorUsuario/1` o un `PUT` → 404 de ASP.NET.

**Errores típicos:** agregar `ActualizarAsync` o un `DELETE` por costumbre.

### Fase 41 — Capas de `EnlaceRegistro`

```text
Fase 41 (RF143-RF148, ADR D22, D24, D25). NO es CRUD: 4 endpoints (GET lista
?fkSesion=, GET/{id}, POST = emitir, PATCH /{id}/revocar). EmitirAsync llama
SELECT * FROM catedras.fn_emitir_enlace_api(...). ObtenerPorIdAsync lee
lower(ventana) AS ventana_desde y upper(ventana) AS ventana_hasta (2
propiedades planas). RevocarAsync: UPDATE ... SET
revocado_en = now() WHERE id_enlace=@Id AND revocado_en IS NULL; el servicio
comprueba existencia (404) y ya revocado (400). EnlaceRegistroEmitir es la
ÚNICA petición; el cliente nunca manda canal/token/ventana/url.
```

**Terminado cuando:** `POST` → 200 con `canal: "QR"` decidido por la BD; un
segundo `POST` en la misma sesión → 200 con otro `id_enlace` y el primero
queda con `revocado_en` no nulo (`RN-14`); `revocar` → 200, repetir → 400;
`fkSesion` inexistente → 500; `PUT`/`DELETE` → 404 de ASP.NET.

**Errores típicos:** intentar mapear el rango `tstzrange` a una propiedad;
tratar `RN-14` como un bug; devolver 404 cuando la sesión no existe (es 500).

### Fase 42 — Capas de `ClaveAcceso` y `RegistroAsistencia`

```text
Fase 42 (RF149-RF158, ADR D24, D25, D26). ClaveAcceso: 4 endpoints (GET lista
?fkAsistente=, GET/{id}, POST /solicitar, POST /validar) con SELECT * FROM
catedras.fn_solicitar_clave_api(@Identificador,@Token,@Ip) y
fn_validar_clave_api(@IdClave,@Clave,@Ip,@UserAgent). ClaveHash [JsonIgnore];
la clave en claro SOLO viaja en la respuesta de solicitar (RNF12). `ip` llega
como string?; el SERVICIO la convierte con IPAddress.TryParse (400 si no es
válida); al leer inet usa host(ip). RegistroAsistencia: 3 endpoints (GET lista
?fkSesion=&fkAsistente=, GET/{id}, POST manual). RegistroAsistenciaCrear: UNA
petición, Origen solo [Required]; el SERVICIO valida {MANUAL, IMPORTADO}
-> ArgumentException (400) ANTES de llamar al repositorio.
```

**Terminado cuando** (con una sesión nueva alrededor de "ahora" y su
enlace emitido): `solicitar` → 200 con `clave` de 6 dígitos; `validar` →
200 con `id_registro`; repetir `validar` → 500; `GET /api/RegistroAsistencia/{id}`
muestra `origen: "QR"` y `fk_vinculacion_snapshot` resuelto; `POST` con
`origen: "QR"` → **400**, con `"MANUAL"` → 200; `ip` inválida → 400; `GET
/api/ClaveAcceso/{id}` no trae `clave_hash`.

**Errores típicos:** `[RegularExpression]` en `Origen` (da 422); calcular
snapshots en C#; capturar el 500 de "clave usada" y devolver otro código;
olvidar que `inet` no mapea a `string` sin `host()`; probar con enlaces
sembrados (vencidos).

### Fase 43 — Prueba de capas ampliada

```text
Fase 43 (criterio 8). Agrega a pruebas/Programa.cs (top-level statements,
sin librerías) repos FALSOS de las 5 entidades. Usuario: la prueba verifica
BCrypt.Verify(clave, hashGuardado) con el hash REAL que calculó el servicio;
login con usuario inexistente / clave mala / activo:false deben dar el mismo
null. Enlace/Clave falsos devuelven un id y una clave fija. Registro con
origen "ENLACE" o "QR" -> ArgumentException y el repositorio falso NO fue
invocado. Reflexión: IRepositorioRolPorUsuario no declara ActualizarAsync.
Cuidado: todas las variables top-level comparten ámbito; usa nombres nuevos.
```

**Terminado cuando:** `docker compose exec api dotnet run --project pruebas`
termina con código 0 y sin fallidas (la v4 cerró en 256 pasadas; anota aquí
y en `8_tasks.md` el total nuevo). Cubre las **29** entidades.

### Fase 44 — Integridad y reglas de negocio contra Docker

```text
Fase 44. No escribas código: dame la secuencia de llamadas HTTP (curl o
PowerShell Invoke-RestMethod) para los 7 pasos de 8_tasks.md, usando los
datos de §7 de guia_ia_v5.md, y qué respuesta exacta esperar en cada uno.
Empieza por fijar la clave de admin.catedras con PATCH /api/Usuario/1.
```

**Terminado cuando:** los 7 resultados coinciden con `5_data_model.md`
(login 200/401/401; rol abrir/cerrar/repetir 400; 2 enlaces y el primero
revocado; solicitar/validar/repetir 500; `origen` 400/200; `DELETE`
asistente con registro → 500; **RF153**: `intentos: 0` tras 4 fallos). Luego
`docker compose down -v && docker compose up -d --build`.

### Fase 45 — Front

```text
Fase 45 (criterios 9-10, §5 de 3_plan.md). Flask + Jinja + Bootstrap, mismo
estilo que las plantillas existentes, sin datos mock. routes/auth.py:
GET/POST /login (POST /api/Usuario/login; 401 -> mensaje genérico; 200 ->
guarda usuario y roles en la sesión de Flask) y /logout. auth_helpers.py:
lee la sesión real y agrega rol_requerido('ADMIN'). routes/admin.py:
before_request en admin_bp (sin sesión -> /login; sin ADMIN -> 403).
navbar: usuario + «Cerrar sesión»; «Administración» solo con ADMIN.
routes/registro.py (ya registrado en app.py): /registro, /registro/<token>,
POST /registro/<token>, POST /registro/<token>/validar — PÚBLICA, sin /admin.
Admin: 5 bloques; EnlaceRegistro con emitir/revocar; ClaveAcceso solo lectura.
Regla 2: FK por <select> o por URL.
```

**Terminado cuando:** `/admin/` sin sesión redirige a `/login`; login con
`admin.catedras` entra y muestra las 5 tarjetas nuevas; `/registro/<token>`
(sin sesión) permite pedir y validar una clave; con `docker compose stop api`
cada pantalla nueva muestra "No se pudo contactar la API".

**Errores típicos:** guardar la clave en la sesión de Flask (solo usuario y
roles); proteger `/admin` pantalla por pantalla en vez de un único
`before_request`; mostrar el 500 de la API crudo en la pantalla pública.

### Fase 46 — Cierre

```text
Fase 46. Con el entorno recién reseteado, acompáñame a correr los 10
criterios de aceptación de 2_spec.md §5 en orden. Para cada uno dime el
comando, el resultado esperado y qué revisar si no coincide. Al final
genera el texto del commit (en español), actualiza los banners «sin
construir» y recuérdame el tag v5. No escribas nada de la v6 todavía
(Artículo 1).
```

**Terminado cuando:** los 10 criterios pasan, el total de la prueba de
capas queda anotado en `8_tasks.md`, todas las casillas están marcadas y
existe el tag `v5`. Al cerrar, actualizar también `README.md`,
`0_mapa_versiones.md`, `guia_ia.md` (estado de la **Regla 3**) y los
comentarios de `docker-compose.yml`.

## 9. Trampas conocidas y diagnóstico

| Síntoma | Causa | Arreglo |
|---|---|---|
| El proyecto no compila al terminar la Fase 39 | `ServicioUsuario` inyecta `IRepositorioRolPorUsuario` (Fase 40) | Crear antes esa interfaz y su implementación |
| `POST /api/RegistroAsistencia` con `origen: "QR"` da 422 | `[RegularExpression]` en la petición | Quitarlo; el servicio valida (D26) → 400 |
| `POST` responde 422 "campo obligatorio" aunque mandaste el dato | Body en camelCase (`fkAsistente`) en vez de snake_case (`fk_asistente`) | Usar snake_case en bodies; camelCase solo en `?query=` |
| `Unable to cast … IPAddress to String` / `inet` | Dapper no convierte `inet` | `host(ip)` en el `SELECT`; la petición trae `string?` y el servicio usa `IPAddress.TryParse` |
| `POST /api/ClaveAcceso/solicitar` → 500 "RN-15" | Enlace fuera de su ventana (los sembrados vencieron en agosto de 2026) | Crear una sesión nueva alrededor de "ahora" y emitir su enlace |
| `validar` → 500 con la clave "bien" | Ya usada (`RN-20`) o pasaron más de 10 minutos | Pedir otra clave |
| Cuatro `validar` malos y `intentos` sigue en 0 | Limite conocido (RF153) | Correcto; no "arreglar" |
| `login` → 401 con la clave "correcta" | Cuenta `activo: false`, o la clave de `admin.catedras` aún no se fijó | `PATCH /api/Usuario/1`; el 401 es igual para todas las causas (D27) |
| Respuesta de `Usuario` trae `clave_hash` | Falta `[JsonIgnore]` en el Modelo | Añadirlo (D23) |
| `InvalidOperationException: JsonPropertyInfo 'ClaveHash' … is marked required but does not specify a setter` | `ClaveHash` es `required` **y** `[JsonIgnore]` | Quitar `required` (`= ""`); System.Text.Json no admite esa combinación |
| Dos `POST /api/EnlaceRegistro` y el primero queda revocado | `RN-14` | Correcto |
| `PUT /api/EnlaceRegistro/1`, `DELETE …`, `GET /api/RolPorUsuario/1` → 404 | Rutas que no existen (D22, D24, D16) | Correcto |
| `DELETE /api/Asistente/1` → 500 | `RESTRICT` desde `registro_asistencia` | Correcto (criterio 7) |
| `login` correcto pero `/admin` da 403 | El usuario no tiene el rol `ADMIN` vigente | Revisar `GET /api/RolPorUsuario?fkUsuario=` |
| Tras `down -v` el login del administrador vuelve a fallar | Se restauró el hash original (clave desconocida) | Repetir el `PATCH /api/Usuario/1` |
| `could not determine data type of parameter` | Falta `::bigint` en un filtro opcional | `@fkSesion::bigint IS NULL OR …` |
| La prueba de capas no compila: "A local variable named 'x' is already defined" | Nombre repetido en top-level | Renombrar |

## 10. Lista de revisión para cualquier cambio de la v5

Pídele a la IA que revise el diff contra esta lista (o revísala tú):

- [ ] Solo nombra tablas de v1-v5 (Art. 1, 5); no expone `parametro`,
      `bitacora`, `lote_*` ni tablas de encuestas.
- [ ] SQL a mano, con prefijo `catedras.` y `@parametros` (Art. 2).
- [ ] `BCrypt` aparece **solo** en `ServicioUsuario` (y en la prueba de
      capas); nunca en controlador ni repositorio (D23, RNF13).
- [ ] `Usuario.ClaveHash` y `ClaveAcceso.ClaveHash` con `[JsonIgnore]`; ninguna
      respuesta trae un hash.
- [ ] `EnlaceRegistro`/`ClaveAcceso`: solo los 4 endpoints de cada una, con
      `SELECT * FROM fn_…`; ni `INSERT` directo ni lógica de ventana/intentos en C#.
- [ ] El cliente nunca manda `canal`, `token`, `ventana`, `url_publica`,
      `fk_clave`, `fk_enlace` ni los *snapshots*.
- [ ] `RolPorUsuario`: 3 endpoints, sin `Id`, sin `PUT`/`DELETE` (D16).
- [ ] `origen` validado en el servicio → 400, sin `[RegularExpression]` (D26).
- [ ] Login: 401 con mensaje genérico idéntico en las 3 causas (D27).
- [ ] Errores de la BD → 500 por el catch de `NpgsqlException`; sin mapeo por
      `SqlState` (D25).
- [ ] `ip` como `string?` en la petición, `IPAddress.TryParse` → 400, `host(ip)`
      al leer.
- [ ] Fechas con hora: `DateTime` UTC en modelos (D21 de v4).
- [ ] La API no autoriza por rol; `/admin` se protege en **un** `before_request`
      del front (D28).
- [ ] Front: sin mock, FK por `<select>`/URL (Regla 2), la clave nunca se guarda
      en la sesión de Flask.
- [ ] Comentarios en español que explican el porqué (Art. 6).
- [ ] `8_tasks.md` actualizado con lo verificado **y dónde**.

## 11. Lo que la v5 le deja a la v6 (no construir todavía)

- `pregunta`, `opcion_pregunta`, `respuesta_encuesta` y `respuesta_item`
  (encuestas respondidas, ligadas a `registro_asistencia`) son de la v6.
- `lote_migracion`, `detalle_migracion`, `bitacora` y `parametro` son de la
  v7; la corrección de un `registro_asistencia` por soporte (RF157) se
  apoyará en `bitacora`.
- La autorización por rol dentro de la API (D28) queda fuera de todo el
  proyecto, salvo que una spec futura la pida.
- La spec de la v6 se escribe **después** del tag `v5` (Artículo 1).
