# Plan técnico — Versión 5: usuarios, login y registro de asistencia

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

C#/ASP.NET Core (.NET 10) + Dapper, sin ORM. **Primer paquete nuevo
desde v1:** `BCrypt.Net-Next` (RNF13 de [2_spec.md](2_spec.md)), que se
usa SOLO desde `ServicioUsuario`. **Sin SP propio nuevo** (a diferencia
de v2/v3): v5 no agrega ningún `CREATE PROCEDURE` — los 3 flujos de
negocio (emitir enlace, solicitar clave, validar clave) llaman
funciones/procedimientos que **ya existen** en `script_bdcatedras.sql`
desde v1.

## 2. Estructura de carpetas

```
api_catedras/
├── Modelos/
│   ├── Usuario.cs             # 7 propiedades; ClaveHash con [JsonIgnore] (§4.2)
│   ├── RolPorUsuario.cs       # 4 propiedades, SIN id (D16, igual que v3)
│   ├── EnlaceRegistro.cs      # 11 propiedades
│   ├── ClaveAcceso.cs         # 11 propiedades; ClaveHash (bytea) con [JsonIgnore]
│   └── RegistroAsistencia.cs  # 11 propiedades
├── Peticiones/
│   ├── Usuario{Crear,Reemplazo,Actualizar}.cs / UsuarioLogin.cs
│   ├── RolPorUsuarioCrear.cs / RolPorUsuarioCerrar.cs      # ⚠️ sin Reemplazo/Actualizar (D16)
│   ├── EnlaceRegistroEmitir.cs                              # ⚠️ ÚNICA — sin Crear/Reemplazo/Actualizar genéricos
│   ├── ClaveAccesoSolicitar.cs / ClaveAccesoValidar.cs       # ⚠️ las ÚNICAS — sin Crear/Reemplazo/Actualizar genéricos
│   └── RegistroAsistenciaCrear.cs                            # ⚠️ ÚNICA (solo origen MANUAL/IMPORTADO)
├── Controllers/
│   ├── UsuarioController.cs            # 6 + [HttpPost("login")]
│   ├── RolPorUsuarioController.cs      # 2: GET lista, POST, PATCH /cerrar (3 en total)
│   ├── EnlaceRegistroController.cs     # GET lista, GET/{id}, POST (=emitir), PATCH /{id}/revocar (4)
│   ├── ClaveAccesoController.cs        # GET lista, GET/{id}, POST /solicitar, POST /validar (4)
│   └── RegistroAsistenciaController.cs # GET lista, GET/{id}, POST (3)
├── Servicios/
│   ├── IServicioUsuario.cs / ServicioUsuario.cs          # BCrypt vive aquí, único lugar (RNF12)
│   ├── IServicioRolPorUsuario.cs / ServicioRolPorUsuario.cs
│   ├── IServicioEnlaceRegistro.cs / ServicioEnlaceRegistro.cs
│   ├── IServicioClaveAcceso.cs / ServicioClaveAcceso.cs
│   └── IServicioRegistroAsistencia.cs / ServicioRegistroAsistencia.cs   # valida origen MANUAL/IMPORTADO (D25)
└── Repositorios/
    ├── IRepositorioUsuario.cs / RepositorioUsuarioPostgres.cs
    ├── IRepositorioRolPorUsuario.cs / RepositorioRolPorUsuarioPostgres.cs     # sin ActualizarAsync
    ├── IRepositorioEnlaceRegistro.cs / RepositorioEnlaceRegistroPostgres.cs   # EmitirAsync = SELECT * FROM fn_emitir_enlace_api(...)
    ├── IRepositorioClaveAcceso.cs / RepositorioClaveAccesoPostgres.cs        # SolicitarAsync/ValidarAsync = SELECT * FROM fn_..._api(...)
    └── IRepositorioRegistroAsistencia.cs / RepositorioRegistroAsistenciaPostgres.cs
```

## 3. Arquitectura en capas

Sin cambios de forma (Artículo 3). Lo nuevo: por primera vez, un
repositorio invoca una **función** SQL (no un procedimiento) que
**devuelve datos** — se llama con `SELECT * FROM`, no con `CALL`
(`CALL` es solo para `PROCEDURE`, que no devuelve filas directamente al
cliente SQL de la misma forma). Ver §4.3.

## 4. Decisiones de diseño clave

### 4.1 BCrypt vive solo en `ServicioUsuario` (ADR D22, Guía IA Regla 3)

```csharp
// ServicioUsuario.cs
public async Task<Usuario> CrearAsync(UsuarioCrear peticion)
{
    var usuario = new Usuario
    {
        FkAsistente = peticion.FkAsistente!.Value,
        NombreUsuario = peticion.Usuario!,
        ClaveHash = BCrypt.Net.BCrypt.HashPassword(peticion.Clave!),   // único punto de hash
        Activo = peticion.Activo ?? true,
    };
    return await _repositorio.CrearAsync(usuario);
}

public async Task<(Usuario Usuario, List<string> Roles)?> LoginAsync(UsuarioLogin peticion)
{
    var usuario = await _repositorio.ObtenerPorLoginAsync(peticion.Usuario!);
    if (usuario is null || !usuario.Activo
        || !BCrypt.Net.BCrypt.Verify(peticion.Clave, usuario.ClaveHash))
        return null;                                   // el Controller lo traduce a 401

    await _repositorio.ActualizarUltimoAccesoAsync(usuario.IdUsuario);
    var roles = await _repositorioRolPorUsuario.ObtenerVigentesAsync(usuario.IdUsuario);
    return (usuario, roles);
}
```

`RepositorioUsuarioPostgres` y `UsuarioController` **nunca** llaman a
`BCrypt`; reciben y devuelven `ClaveHash` como una cadena opaca (y ni
siquiera eso sale en las respuestas — ver §4.2).

### 4.2 `ClaveHash` nunca se serializa (`[JsonIgnore]`)

```csharp
public class Usuario
{
    public long IdUsuario { get; set; }
    public required long FkAsistente { get; set; }
    [JsonPropertyName("usuario")]
    public required string NombreUsuario { get; set; }
    [JsonIgnore]                                    // RF138: nunca sale en una respuesta HTTP
    public required string ClaveHash { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
```

Se eligió `[JsonIgnore]` sobre el Modelo en vez de armar un
`UsuarioRespuesta` aparte: más simple, y ningún otro Modelo del
proyecto necesita dos formas distintas de serializarse — una excepción
puntual de anotación es menos cambio estructural que una nueva familia
de DTO de solo-respuesta en todo el proyecto.

### 4.3 `CALL` para procedimientos, `SELECT * FROM` para funciones (ADR D23)

`EnlaceRegistro` y `ClaveAcceso` no hacen `INSERT` propio: llaman a
funciones (`FUNCTION`, no `PROCEDURE`) que la BD ya expone desde v1 y
que internamente hacen `CALL` al procedimiento real:

```csharp
// RepositorioEnlaceRegistroPostgres.cs
public async Task<long> EmitirAsync(long fkSesion, int? minutosAntes, int? minutosDespues, long? fkUsuario)
{
    const string sql = @"SELECT * FROM catedras.fn_emitir_enlace_api(
        @FkSesion, @MinutosAntes, @MinutosDespues, @FkUsuario);";
    return await conexion.ExecuteScalarAsync<long>(sql,
        new { FkSesion = fkSesion, MinutosAntes = minutosAntes, MinutosDespues = minutosDespues, FkUsuario = fkUsuario });
}
```

```csharp
// RepositorioClaveAccesoPostgres.cs
public async Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(string identificador, Guid token, IPAddress? ip)
{
    const string sql = @"SELECT * FROM catedras.fn_solicitar_clave_api(@Identificador, @Token, @Ip);";
    var fila = await conexion.QuerySingleAsync<(long id_clave, string clave, string enviado_a)>(sql,
        new { Identificador = identificador, Token = token, Ip = ip });
    return (fila.id_clave, fila.clave, fila.enviado_a);
}

public async Task<long> ValidarAsync(long idClave, string clave, IPAddress? ip, string? userAgent)
{
    const string sql = @"SELECT * FROM catedras.fn_validar_clave_api(@IdClave, @Clave, @Ip, @UserAgent);";
    return await conexion.ExecuteScalarAsync<long>(sql,
        new { IdClave = idClave, Clave = clave, Ip = ip, UserAgent = userAgent });
}
```

Es la diferencia mecánica con el patrón `CALL ... CAST(@X AS jsonb)`
de v2/v3 (que son `PROCEDURE`, invocados sin esperar un valor de
retorno directo del motor de esa forma): aquí no hay ningún parámetro
`jsonb` ni ninguna migración nueva — las funciones ya existen, ya
hacen todo el trabajo (incluido su propio `CALL` interno al
`PROCEDURE`), y el repositorio solo las invoca y lee lo que devuelven.

### 4.4 Excepciones de negocio de la BD: todas caen en el mismo 500 (ADR D24)

`sp_solicitar_clave` y `sp_validar_clave_y_registrar` lanzan
`RAISE EXCEPTION ... USING ERRCODE = 'no_data_found'` (identificador
no resuelto) o `'check_violation'` (enlace revocado, clave expirada,
cupo agotado, etc.). En C#, **ambas** llegan como `PostgresException`
— una subclase de `NpgsqlException` — y el proyecto, desde v1, captura
por **tipo** de excepción, no por `SqlState`:

```csharp
catch (ArgumentException ex) { ... }      // 400 — el SERVICIO lo lanzó
catch (NoEncontradoExcepcion ex) { ... }  // 404 — el SERVICIO lo lanzó, tras un GET que no encontró filas
catch (NpgsqlException ex) { ... }        // 500 — CUALQUIER error que venga de la BD, sea CHECK, FK o RAISE EXCEPTION de negocio
catch (Exception ex) { ... }              // 500 — cualquier otra cosa
```

Por eso `fn_resolver_asistente`/`sp_solicitar_clave` devolviendo "no
hay ningún asistente con ese identificador" (semánticamente un "no
encontrado") de todos modos responde **500, no 404**: el 404 de este
proyecto SOLO lo lanza el servicio, a propósito, después de que un
`ObtenerPorIdAsync` propio devuelve `null` — nunca es una traducción
automática de lo que la BD decide llamar "no encontrado" en su propio
vocabulario. **No se agrega un `catch` nuevo para distinguir estos
casos** — sería inventar una regla de mapeo de `SqlState` que el resto
del proyecto no tiene, solo para esta versión.

### 4.5 `RegistroAsistencia` manual: el único caso de revalidación "de más" (ADR D25)

El `CHECK chk_registro_clave` de la BD acepta
`origen IN ('MANUAL', 'IMPORTADO') OR fk_clave IS NOT NULL` — es decir,
un `POST` con `origen: "QR"` y `fk_clave: null` **pasaría** ese
`CHECK` sin problema (el `CHECK` solo exige la combinación contraria).
Dejarlo pasar produciría un registro con `origen: "QR"` pero sin
ninguna clave real detrás — una mentira de auditoría silenciosa, no un
error visible. Por eso `ServicioRegistroAsistencia.CrearAsync` valida
`origen` en una lista cerrada de 2 valores (`MANUAL`, `IMPORTADO`)
ANTES de llamar al repositorio, y es la única vez en el proyecto donde
el servicio reivindica una regla que, mirada estrictamente, la BD ya
"permite" — la razón es que la BD la permite por accidente de forma,
no porque sea correcta de negocio.

## 5. Frontend: login real y autorregistro público

```python
# routes/auth.py (forma, no código final)
# POST /login -> llama POST /api/Usuario/login; si 401, muestra error;
# si 200, guarda en la sesion de Flask (firmada, NO es la API) el usuario y sus roles.
# auth_helpers.py deja de devolver siempre "sin sesión" (v2): ahora lee la sesión de Flask real.

# routes/autorregistro.py — pantalla PÚBLICA, sin pasar por /admin:
# GET /registro/<token>   -> formulario "ingresa tu identificador"
# POST /registro/<token>  -> llama POST /api/ClaveAcceso/solicitar; muestra la clave (RNF11) o la envía simulada
# POST /registro/validar  -> llama POST /api/ClaveAcceso/validar; muestra "asistencia registrada" o el motivo del 500
```

## 6. Docker: sin cambios de infraestructura, sin migraciones nuevas

Sigue siendo `postgres` + `api`, **sin ningún archivo nuevo en**
`db/postgres/migraciones/` (igual que v4): las 5 tablas y las 3
funciones/procedimientos que v5 usa ya estaban en
`script_bdcatedras.sql` desde el montaje de la v1.
