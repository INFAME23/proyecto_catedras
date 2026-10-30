// ==========================================================================
//  Repositorios/RepositorioUsuarioPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Todos los métodos pasan por los procedimientos de
//  'db/postgres/migraciones/033_sp_usuario.sql'. Esta clase ya no escribe
//  SELECT/INSERT/UPDATE/DELETE: arma el 'CALL', manda los parámetros y
//  convierte el JSON de 'p_resultado' en objetos C# (molde: sede).
//
//  LO QUE NO CAMBIÓ: IRepositorioUsuario, ServicioUsuario, UsuarioController
//  y las Peticiones siguen igual. En particular, ESTE ARCHIVO SIGUE SIN
//  CONOCER BCrypt (ADR D23, RNF13): recibe 'ClaveHash' / "clave_hash" ya
//  calculado por ServicioUsuario y lo pasa tal cual. Nunca decide si una
//  clave se hashea.
//
//  'clave_hash' Y EL JSON:
//   · sp_listar_ / sp_consultar_ / sp_crear_ / sp_actualizar_ NO devuelven
//     'clave_hash' (esos JSON pueden terminar sirviendo una respuesta).
//   · Solo 'sp_consultar_usuario_por_login' lo trae, porque el login lo
//     necesita para BCrypt.Verify. Como el modelo marca ClaveHash con
//     [JsonIgnore] (para que JAMÁS salga en una respuesta), JsonSerializer
//     tampoco lo LEE: aquí se saca del JSON aparte y se asigna a mano.
//
//  ELIMINAR AHORA ES LÓGICO (activo = false), porque la tabla tiene
//  'activo'. Antes era un DELETE con CASCADE a rol_por_usuario.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.usuario' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioUsuarioPostgres : IRepositorioUsuario
{
    private readonly string _cadenaConexion;

    public RepositorioUsuarioPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Usuario>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_usuario(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        return LectorJsonSp.Lista<Usuario>(json);
    }

    public async Task<Usuario?> ObtenerPorIdAsync(long idUsuario)
    {
        const string sql = "CALL catedras.sp_consultar_usuario(@idUsuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idUsuario });

        // Sin clave_hash: ClaveHash queda "" (nadie fuera del login lo usa).
        return LectorJsonSp.Fila<Usuario>(json);
    }

    public async Task<Usuario?> ObtenerPorLoginAsync(string usuario)
    {
        const string sql = "CALL catedras.sp_consultar_usuario_por_login(@usuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { usuario });

        if (json is null)
            return null;

        var encontrado = LectorJsonSp.Fila<Usuario>(json)!;

        // [JsonIgnore] impide que JsonSerializer llene ClaveHash: se lee
        // aparte. Solo vive en memoria para el BCrypt.Verify del servicio.
        encontrado.ClaveHash = LectorJsonSp.Texto(json, "clave_hash") ?? "";
        return encontrado;
    }

    public async Task<(long IdUsuario, DateTime CreadoEn)> CrearAsync(Usuario usuario)
    {
        // El hash ya viene calculado en usuario.ClaveHash (ServicioUsuario).
        const string sql = @"
            CALL catedras.sp_crear_usuario(
                @FkAsistente, @NombreUsuario, @ClaveHash, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Login o persona repetidos (uq_usuario_login, uq_usuario_persona) ->
        // NpgsqlException, igual que antes.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            usuario.FkAsistente,
            usuario.NombreUsuario,
            usuario.ClaveHash,
            usuario.Activo,
        });

        // id_usuario y creado_en los pone la BD; vienen en la fila devuelta.
        var creado = LectorJsonSp.Fila<Usuario>(json)!;
        return (creado.IdUsuario, creado.CreadoEn);
    }

    public async Task<int> ActualizarAsync(long idUsuario, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (sin clave_hash).
        const string sqlConsultar = "CALL catedras.sp_consultar_usuario(@idUsuario, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idUsuario });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = LectorJsonSp.Fila<Usuario>(actualJson)!;

        // 2) Mezclar. Las claves del diccionario son las de siempre
        //    (fk_asistente, usuario, clave_hash, activo).
        long fkAsistente = datos.TryGetValue("fk_asistente", out var vAsistente) ? (long)vAsistente! : actual.FkAsistente;
        string nombreUsuario = datos.TryGetValue("usuario", out var vUsuario) ? (string)vUsuario! : actual.NombreUsuario;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // La clave NO se mezcla con la actual (el hash guardado nunca sale
        // de la BD): si llegó "clave_hash" (ya hasheado por el servicio) se
        // manda; si no llegó, va NULL y el procedimiento conserva el hash
        // que tenía (COALESCE). Este repositorio no hashea nada.
        string? claveHash = datos.TryGetValue("clave_hash", out var vClave) ? (string?)vClave : null;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_usuario(
                @idUsuario, @fkAsistente, @nombreUsuario, @claveHash, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idUsuario, fkAsistente, nombreUsuario, claveHash, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task ActualizarUltimoAccesoAsync(long idUsuario)
    {
        const string sql = "CALL catedras.sp_registrar_acceso_usuario(@idUsuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, new { idUsuario });
    }

    // Forma de la fila de sp_buscar_estudiante_sin_cuenta.
    private sealed class FilaCandidato
    {
        [System.Text.Json.Serialization.JsonPropertyName("id_asistente")]
        public long IdAsistente { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("id_asis")]
        public string IdAsis { get; set; } = "";
    }

    public async Task<(long IdAsistente, string IdAsis)?> BuscarEstudianteSinCuentaAsync(string correo)
    {
        const string sql = "CALL catedras.sp_buscar_estudiante_sin_cuenta(@correo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { correo });

        var fila = LectorJsonSp.Fila<FilaCandidato>(json);
        return fila is null ? null : (fila.IdAsistente, fila.IdAsis);
    }

    public async Task<Usuario?> ProvisionarCuentaEstudianteAsync(string correo, string claveHash)
    {
        const string sql = "CALL catedras.sp_provisionar_cuenta_estudiante(@correo, @claveHash, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { correo, claveHash });

        var creado = LectorJsonSp.Fila<Usuario>(json);
        if (creado is not null)
            creado.ClaveHash = claveHash;       // solo vive en memoria; no sale en ninguna respuesta ([JsonIgnore])
        return creado;
    }

    public async Task FijarDebeCambiarClaveAsync(long idUsuario, bool valor)
    {
        const string sql = "CALL catedras.sp_fijar_debe_cambiar_clave(@idUsuario, @valor);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, new { idUsuario, valor });
    }

    public async Task<int> EliminarAsync(long idUsuario)
    {
        const string sql = "CALL catedras.sp_eliminar_usuario(@idUsuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idUsuario });

        // {"eliminado": true|false}. LÓGICO: activo=false; sus roles y su
        // historial se conservan, y el login ya rechaza cuentas inactivas.
        return LectorJsonSp.Bandera(json, "eliminado") ? 1 : 0;
    }
}
