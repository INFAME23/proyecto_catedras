// ==========================================================================
//  Repositorios/RepositorioInscripcionCatedraPostgres.cs  ·  Cátedras · Subversión v7.1 · D50
// --------------------------------------------------------------------------
//  Llama a los tres procedimientos de 'db/postgres/migraciones/049_inscripcion_catedra.sql'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.inscripcion_catedra' en PostgreSQL, vía procedimientos.</summary>
public class RepositorioInscripcionCatedraPostgres : IRepositorioInscripcionCatedra
{
    private readonly string _cadenaConexion;

    public RepositorioInscripcionCatedraPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<InscripcionCatedra>> ObtenerTodosAsync(
        long? fkAsistente, long? fkCatedra, bool soloActivas, int limite)
    {
        const string sql = @"
            CALL catedras.sp_listar_inscripcion_catedra(
                @fkAsistente::bigint, @fkCatedra::bigint, @soloActivas, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { fkAsistente, fkCatedra, soloActivas, limite });

        return LectorJsonSp.Lista<InscripcionCatedra>(json);
    }

    public async Task<string> InscribirAsync(long fkCatedra, long fkAsistente)
    {
        const string sql = "CALL catedras.sp_inscribir_catedra(@fkCatedra, @fkAsistente, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleAsync<string>(sql, new { fkCatedra, fkAsistente });
    }

    public async Task<string> CancelarAsync(long fkCatedra, long fkAsistente)
    {
        const string sql = "CALL catedras.sp_cancelar_inscripcion_catedra(@fkCatedra, @fkAsistente, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleAsync<string>(sql, new { fkCatedra, fkAsistente });
    }
}
