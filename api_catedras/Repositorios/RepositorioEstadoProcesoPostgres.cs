// ==========================================================================
//  Repositorios/RepositorioEstadoProcesoPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioEstadoProceso para PostgreSQL con Dapper.
//  SQL a mano, SIEMPRE parametrizado; una conexión por operación. Mismo
//  patrón que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.estado_proceso' en PostgreSQL mediante Dapper.</summary>
public class RepositorioEstadoProcesoPostgres : IRepositorioEstadoProceso
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "es_final", "es_exitoso" };

    public RepositorioEstadoProcesoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<EstadoProceso>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_estado, nombre, es_final, es_exitoso
            FROM catedras.estado_proceso
            ORDER BY id_estado
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<EstadoProceso>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<EstadoProceso?> ObtenerPorIdAsync(string idEstado)
    {
        const string sql = @"
            SELECT id_estado, nombre, es_final, es_exitoso
            FROM catedras.estado_proceso
            WHERE id_estado = @idEstado;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<EstadoProceso>(sql, new { idEstado });
    }

    public async Task CrearAsync(EstadoProceso estado)
    {
        const string sql = @"
            INSERT INTO catedras.estado_proceso (id_estado, nombre, es_final, es_exitoso)
            VALUES (@IdEstado, @Nombre, @EsFinal, @EsExitoso);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, estado);
    }

    public async Task<int> ActualizarAsync(string idEstado, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idEstado);

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
            $"UPDATE catedras.estado_proceso SET {string.Join(", ", asignaciones)} " +
            "WHERE id_estado = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idEstado)
    {
        const string sql = "DELETE FROM catedras.estado_proceso WHERE id_estado = @idEstado;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el estado tiene lotes de migración asociados, fk_lotemig_estado
        // (ON DELETE RESTRICT) hace que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idEstado });
    }
}
