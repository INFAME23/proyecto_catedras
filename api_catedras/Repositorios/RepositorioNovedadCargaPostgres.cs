// ==========================================================================
//  Repositorios/RepositorioNovedadCargaPostgres.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioNovedadCarga para PostgreSQL con Dapper. Solo SELECT.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso de solo lectura a 'catedras.novedad_carga' en PostgreSQL.</summary>
public class RepositorioNovedadCargaPostgres : IRepositorioNovedadCarga
{
    private readonly string _cadenaConexion;

    public RepositorioNovedadCargaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<NovedadCarga>> ObtenerTodosAsync(long? fkLoteCarga, int limite)
    {
        const string sql = @"
            SELECT id_novedad, fk_lote_carga, numero_fila, contenido_crudo, motivo
            FROM catedras.novedad_carga
            WHERE @fkLoteCarga::bigint IS NULL OR fk_lote_carga = @fkLoteCarga
            ORDER BY fk_lote_carga, numero_fila
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<NovedadCarga>(sql, new { fkLoteCarga, limite });
        return filas.ToList();
    }

    public async Task<NovedadCarga?> ObtenerPorIdAsync(long idNovedad)
    {
        const string sql = @"
            SELECT id_novedad, fk_lote_carga, numero_fila, contenido_crudo, motivo
            FROM catedras.novedad_carga
            WHERE id_novedad = @idNovedad;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<NovedadCarga>(sql, new { idNovedad });
    }
}
