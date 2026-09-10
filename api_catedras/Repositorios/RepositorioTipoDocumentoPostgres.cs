// ==========================================================================
//  Repositorios/RepositorioTipoDocumentoPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioTipoDocumento para PostgreSQL con Dapper.
//  SQL a mano, SIEMPRE parametrizado; una conexión por operación. Mismo
//  patrón que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.tipo_documento' en PostgreSQL mediante Dapper.</summary>
public class RepositorioTipoDocumentoPostgres : IRepositorioTipoDocumento
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "activo" };

    public RepositorioTipoDocumentoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoDocumento>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_tipo_documento, nombre, activo
            FROM catedras.tipo_documento
            ORDER BY id_tipo_documento
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<TipoDocumento>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<TipoDocumento?> ObtenerPorIdAsync(string idTipoDocumento)
    {
        const string sql = @"
            SELECT id_tipo_documento, nombre, activo
            FROM catedras.tipo_documento
            WHERE id_tipo_documento = @idTipoDocumento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<TipoDocumento>(sql, new { idTipoDocumento });
    }

    public async Task CrearAsync(TipoDocumento tipoDocumento)
    {
        const string sql = @"
            INSERT INTO catedras.tipo_documento (id_tipo_documento, nombre, activo)
            VALUES (@IdTipoDocumento, @Nombre, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, tipoDocumento);
    }

    public async Task<int> ActualizarAsync(string idTipoDocumento, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idTipoDocumento);

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
            $"UPDATE catedras.tipo_documento SET {string.Join(", ", asignaciones)} " +
            "WHERE id_tipo_documento = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idTipoDocumento)
    {
        const string sql = "DELETE FROM catedras.tipo_documento WHERE id_tipo_documento = @idTipoDocumento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el tipo de documento tiene documentos de asistente asociados,
        // fk_documento_tipo (ON DELETE RESTRICT) hace que PostgreSQL lance
        // NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idTipoDocumento });
    }
}
