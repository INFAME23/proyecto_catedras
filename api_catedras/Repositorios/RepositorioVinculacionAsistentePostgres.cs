// ==========================================================================
//  Repositorios/RepositorioVinculacionAsistentePostgres.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioVinculacionAsistente para PostgreSQL con Dapper.
//
//  Primera tabla del proyecto sin columna 'id': el WHERE del cierre usa
//  las TRES columnas de la llave primaria. Las fechas (DateOnly) viajan
//  gracias al ManejadorTipoDateOnly que se registra en Program.cs.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.vinculacion_asistente' en PostgreSQL mediante Dapper.</summary>
public class RepositorioVinculacionAsistentePostgres : IRepositorioVinculacionAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioVinculacionAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<VinculacionAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        // Orden: por asistente y, dentro de cada uno, la más reciente primero.
        const string sql = @"
            SELECT fk_asistente, fk_tipo_vinculacion, fecha_ini, fecha_fin
            FROM catedras.vinculacion_asistente
            WHERE @fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente
            ORDER BY fk_asistente, fecha_ini DESC, fk_tipo_vinculacion
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<VinculacionAsistente>(sql, new { fkAsistente, limite });
        return filas.ToList();
    }

    public async Task CrearAsync(VinculacionAsistente vinculacion)
    {
        // Una vinculación nace abierta: 'fecha_fin' queda NULL.
        const string sql = @"
            INSERT INTO catedras.vinculacion_asistente (fk_asistente, fk_tipo_vinculacion, fecha_ini)
            VALUES (@FkAsistente, @FkTipoVinculacion, @FechaIni);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, vinculacion);
    }

    public async Task<int> CerrarAsync(long fkAsistente, string fkTipoVinculacion,
                                       DateOnly fechaIni, DateOnly fechaFin)
    {
        // La llave COMPLETA en el WHERE identifica UNA fila. 'AND fecha_fin
        // IS NULL' es la defensa del motor: aunque el servicio ya revisó que
        // siguiera abierta, una petición simultánea no puede cerrarla dos veces.
        const string sql = @"
            UPDATE catedras.vinculacion_asistente
               SET fecha_fin = @fechaFin
             WHERE fk_asistente = @fkAsistente
               AND fk_tipo_vinculacion = @fkTipoVinculacion
               AND fecha_ini = @fechaIni
               AND fecha_fin IS NULL;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { fkAsistente, fkTipoVinculacion, fechaIni, fechaFin });
    }
}
