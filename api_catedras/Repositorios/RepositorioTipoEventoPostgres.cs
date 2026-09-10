// ==========================================================================
//  Repositorios/RepositorioTipoEventoPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioTipoEvento para PostgreSQL con Dapper.
//  SQL a mano, SIEMPRE parametrizado; una conexión por operación. Mismo
//  patrón que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.tipo_evento' en PostgreSQL mediante Dapper.</summary>
public class RepositorioTipoEventoPostgres : IRepositorioTipoEvento
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "activo" };

    public RepositorioTipoEventoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoEvento>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_tipo_evento, nombre, activo
            FROM catedras.tipo_evento
            ORDER BY id_tipo_evento
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<TipoEvento>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<TipoEvento?> ObtenerPorIdAsync(string idTipoEvento)
    {
        const string sql = @"
            SELECT id_tipo_evento, nombre, activo
            FROM catedras.tipo_evento
            WHERE id_tipo_evento = @idTipoEvento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<TipoEvento>(sql, new { idTipoEvento });
    }

    public async Task CrearAsync(TipoEvento tipoEvento)
    {
        const string sql = @"
            INSERT INTO catedras.tipo_evento (id_tipo_evento, nombre, activo)
            VALUES (@IdTipoEvento, @Nombre, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, tipoEvento);
    }

    public async Task<int> ActualizarAsync(string idTipoEvento, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idTipoEvento);

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
            $"UPDATE catedras.tipo_evento SET {string.Join(", ", asignaciones)} " +
            "WHERE id_tipo_evento = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idTipoEvento)
    {
        const string sql = "DELETE FROM catedras.tipo_evento WHERE id_tipo_evento = @idTipoEvento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el tipo de evento tiene cátedras asociadas, fk_catedra_tipo_evento
        // (ON DELETE RESTRICT) hace que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idTipoEvento });
    }
}
