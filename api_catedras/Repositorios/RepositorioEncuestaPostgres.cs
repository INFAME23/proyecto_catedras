// ==========================================================================
//  Repositorios/RepositorioEncuestaPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioEncuesta para PostgreSQL con Dapper.
//
//  'CrearAsync' es distinto de Sede/Modalidad/EstadoProceso: usa
//  'INSERT ... RETURNING id_encuesta' y lee el resultado con
//  'QuerySingleAsync<long>' (no 'ExecuteAsync', que solo cuenta filas)
//  porque necesita EL VALOR que la BD generó (3_plan.md §4.7).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.encuesta' en PostgreSQL mediante Dapper.</summary>
public class RepositorioEncuestaPostgres : IRepositorioEncuesta
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "version", "vigente_desde", "vigente_hasta", "activa" };

    public RepositorioEncuestaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Encuesta>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
            FROM catedras.encuesta
            ORDER BY id_encuesta
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Encuesta>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<Encuesta?> ObtenerPorIdAsync(long idEncuesta)
    {
        const string sql = @"
            SELECT id_encuesta, nombre, version, vigente_desde, vigente_hasta, activa
            FROM catedras.encuesta
            WHERE id_encuesta = @idEncuesta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Encuesta>(sql, new { idEncuesta });
    }

    public async Task<long> CrearAsync(Encuesta encuesta)
    {
        // 'id_encuesta' NO se manda: la columna es IDENTITY. RETURNING
        // devuelve el valor que la BD acaba de generar.
        const string sql = @"
            INSERT INTO catedras.encuesta (nombre, version, vigente_desde, vigente_hasta, activa)
            VALUES (@Nombre, @Version, @VigenteDesde, @VigenteHasta, @Activa)
            RETURNING id_encuesta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // QuerySingleAsync<long>: exactamente un valor (el id), no un objeto
        // ni un conteo de filas.
        return await conexion.QuerySingleAsync<long>(sql, encuesta);
    }

    public async Task<int> ActualizarAsync(long idEncuesta, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idEncuesta);

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
            $"UPDATE catedras.encuesta SET {string.Join(", ", asignaciones)} " +
            "WHERE id_encuesta = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idEncuesta)
    {
        const string sql = "DELETE FROM catedras.encuesta WHERE id_encuesta = @idEncuesta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { idEncuesta });
    }
}
