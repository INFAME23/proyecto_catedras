// ==========================================================================
//  Repositorios/RepositorioUsuarioPostgres.cs  ·  Proyecto Cátedras · v5
// --------------------------------------------------------------------------
//  IRepositorioUsuario para PostgreSQL con Dapper. SQL a mano y
//  parametrizado; una conexión por operación.
//
//  Dos detalles propios de esta tabla:
//
//  1) La columna se llama 'usuario' pero la propiedad del modelo es
//     'NombreUsuario'. Dapper (con MatchNamesWithUnderscores) enlaza
//     'nombre_usuario' <-> NombreUsuario, pero NO sabe que 'usuario' es lo
//     mismo. Por eso el SELECT escribe 'usuario AS nombre_usuario'.
//
//  2) Este archivo NO conoce BCrypt (ADR D23, RNF13). Guarda y lee
//     'clave_hash' como una cadena cualquiera. El hash lo calcula
//     ServicioUsuario ANTES de llamar aquí, y lo compara también el servicio.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.usuario' en PostgreSQL mediante Dapper.</summary>
public class RepositorioUsuarioPostgres : IRepositorioUsuario
{
    private readonly string _cadenaConexion;

    // Columnas de lectura (las 7 de la tabla), escritas una sola vez.
    // 'clave_hash' SÍ se lee: el servicio la necesita para el login. Que no
    // salga en el JSON lo garantiza el [JsonIgnore] del modelo (RF138).
    private const string Columnas = @"
            id_usuario, fk_asistente, usuario AS nombre_usuario, clave_hash,
            activo, ultimo_acceso, creado_en";

    // [ACID · C] Lista blanca del UPDATE. NO están: 'id_usuario' (PK),
    // 'creado_en' (la pone la BD) ni 'ultimo_acceso' (tiene su propio método).
    private static readonly HashSet<string> ColumnasActualizables = new()
    {
        "fk_asistente", "usuario", "clave_hash", "activo",
    };

    public RepositorioUsuarioPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Usuario>> ObtenerTodosAsync(int limite)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.usuario
            ORDER BY id_usuario
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Usuario>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<Usuario?> ObtenerPorIdAsync(long idUsuario)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.usuario
            WHERE id_usuario = @idUsuario;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { idUsuario });
    }

    public async Task<Usuario?> ObtenerPorLoginAsync(string usuario)
    {
        // 'uq_usuario_login' garantiza que hay a lo sumo una fila con ese nombre.
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.usuario
            WHERE usuario = @usuario;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Usuario>(sql, new { usuario });
    }

    public async Task<(long IdUsuario, DateTime CreadoEn)> CrearAsync(Usuario usuario)
    {
        // Ni id_usuario ni creado_en se envían: los pone la BD. 'ultimo_acceso'
        // tampoco: un usuario nuevo todavía no ha entrado.
        // Los parámetros salen de las propiedades del modelo (@NombreUsuario...).
        const string sql = @"
            INSERT INTO catedras.usuario
                (fk_asistente, usuario, clave_hash, activo)
            VALUES (@FkAsistente, @NombreUsuario, @ClaveHash, @Activo)
            RETURNING id_usuario, creado_en;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila con dos columnas -> tupla (long, DateTime), en ese orden.
        return await conexion.QuerySingleAsync<(long, DateTime)>(sql, usuario);
    }

    public async Task<int> ActualizarAsync(long idUsuario, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_fila", idUsuario);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        var sql =
            $"UPDATE catedras.usuario SET {string.Join(", ", asignaciones)} " +
            "WHERE id_usuario = @id_fila;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task ActualizarUltimoAccesoAsync(long idUsuario)
    {
        // 'now()' lo evalúa PostgreSQL: la hora es la del servidor de la BD.
        const string sql = @"
            UPDATE catedras.usuario
            SET ultimo_acceso = now()
            WHERE id_usuario = @idUsuario;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, new { idUsuario });
    }

    public async Task<int> EliminarAsync(long idUsuario)
    {
        const string sql = "DELETE FROM catedras.usuario WHERE id_usuario = @idUsuario;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // [ACID · A] Un DELETE es UNA transacción implícita: los roles del
        // usuario (rol_por_usuario, CASCADE) se borran junto con la cuenta, o
        // no se borra nada si algo falla.
        return await conexion.ExecuteAsync(sql, new { idUsuario });
    }
}
