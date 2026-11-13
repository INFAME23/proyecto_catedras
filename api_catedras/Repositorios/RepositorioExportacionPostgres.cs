// ==========================================================================
//  Repositorios/RepositorioExportacionPostgres.cs  ·  Subversión v7.2 · RF-S13 / RF-S14
// --------------------------------------------------------------------------
//  Solo llama a las dos funciones de la migración 066 y convierte su JSON en
//  objetos C#. Nada de SQL de negocio aquí: está en la base de datos, visible
//  y parametrizado.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Consultas de exportación en PostgreSQL.</summary>
public class RepositorioExportacionPostgres : IRepositorioExportacion
{
    private readonly string _cadenaConexion;

    public RepositorioExportacionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ExportacionCatedra>> CatedrasDelPeriodoAsync(string periodo)
    {
        // '::character(6)': el periodo es char(6). La función devuelve 'json'; '::text' lo entrega como cadena.
        const string sql = "SELECT catedras.fn_exportar_catedras_periodo(@periodo::character(6))::text;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { periodo });

        return LectorJsonSp.Lista<ExportacionCatedra>(json);
    }

    public async Task<List<ExportacionCatedraAsistentes>> AsistentesDelPeriodoAsync(string periodo, string? desde, int limite)
    {
        const string sql = @"
            SELECT catedras.fn_exportar_asistentes_periodo(
                       @periodo::character(6), @desde::text, @limite::integer)::text;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { periodo, desde, limite });

        return LectorJsonSp.Lista<ExportacionCatedraAsistentes>(json);
    }
}
