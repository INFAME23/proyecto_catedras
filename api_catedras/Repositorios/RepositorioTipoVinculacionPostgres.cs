// ==========================================================================
//  Repositorios/RepositorioTipoVinculacionPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioTipoVinculacion para PostgreSQL con
//  Dapper. SQL a mano, SIEMPRE parametrizado; una conexión por operación.
//  Mismo patrón que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.tipo_vinculacion' en PostgreSQL mediante Dapper.</summary>
public class RepositorioTipoVinculacionPostgres : IRepositorioTipoVinculacion
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "es_interno", "exige_programa", "activo" };

    public RepositorioTipoVinculacionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoVinculacion>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_tipo_vinculacion, nombre, es_interno, exige_programa, activo
            FROM catedras.tipo_vinculacion
            ORDER BY id_tipo_vinculacion
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<TipoVinculacion>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<TipoVinculacion?> ObtenerPorIdAsync(string idTipoVinculacion)
    {
        const string sql = @"
            SELECT id_tipo_vinculacion, nombre, es_interno, exige_programa, activo
            FROM catedras.tipo_vinculacion
            WHERE id_tipo_vinculacion = @idTipoVinculacion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<TipoVinculacion>(sql, new { idTipoVinculacion });
    }

    public async Task CrearAsync(TipoVinculacion tipoVinculacion)
    {
        const string sql = @"
            INSERT INTO catedras.tipo_vinculacion
                (id_tipo_vinculacion, nombre, es_interno, exige_programa, activo)
            VALUES (@IdTipoVinculacion, @Nombre, @EsInterno, @ExigePrograma, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, tipoVinculacion);
    }

    public async Task<int> ActualizarAsync(string idTipoVinculacion, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idTipoVinculacion);

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
            $"UPDATE catedras.tipo_vinculacion SET {string.Join(", ", asignaciones)} " +
            "WHERE id_tipo_vinculacion = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idTipoVinculacion)
    {
        const string sql = "DELETE FROM catedras.tipo_vinculacion WHERE id_tipo_vinculacion = @idTipoVinculacion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el tipo de vinculación tiene asistentes vinculados o registros
        // de asistencia con ese snapshot, las FK correspondientes
        // (ON DELETE RESTRICT) hacen que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idTipoVinculacion });
    }
}
