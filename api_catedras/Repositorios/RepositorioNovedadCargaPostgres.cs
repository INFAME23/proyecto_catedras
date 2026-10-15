// ==========================================================================
//  Repositorios/RepositorioNovedadCargaPostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioNovedadCarga para PostgreSQL. Solo lectura, ahora vía
//  sp_listar_ y sp_consultar_novedad_carga ('030_sp_novedad_carga.sql').
//  Los otros 3 procedimientos de la tabla existen en la BD pero no se usan
//  aquí: una novedad nace únicamente en sp_guardar_lote_carga (RF118).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso de solo lectura a 'catedras.novedad_carga', vía procedimientos almacenados.</summary>
public class RepositorioNovedadCargaPostgres : IRepositorioNovedadCarga
{
    private readonly string _cadenaConexion;

    public RepositorioNovedadCargaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<NovedadCarga>> ObtenerTodosAsync(long? fkLoteCarga, int limite)
    {
        const string sql = "CALL catedras.sp_listar_novedad_carga(@fkLoteCarga, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkLoteCarga, limite });

        return JsonSerializer.Deserialize<List<NovedadCarga>>(json!) ?? [];
    }

    public async Task<NovedadCarga?> ObtenerPorIdAsync(long idNovedad)
    {
        const string sql = "CALL catedras.sp_consultar_novedad_carga(@idNovedad, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idNovedad });

        return json is null ? null : JsonSerializer.Deserialize<NovedadCarga>(json);
    }
}
