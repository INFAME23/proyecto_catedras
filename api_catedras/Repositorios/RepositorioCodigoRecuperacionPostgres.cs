// ==========================================================================
//  Repositorios/RepositorioCodigoRecuperacionPostgres.cs  ·  Cátedras · Subversión v7.1
// --------------------------------------------------------------------------
//  Llama a los dos procedimientos de 'db/postgres/migraciones/047_codigo_recuperacion.sql'.
//  El rechazo de un código malo llega como ESTADO (texto), no como excepción.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.codigo_recuperacion' en PostgreSQL, vía procedimientos.</summary>
public class RepositorioCodigoRecuperacionPostgres : IRepositorioCodigoRecuperacion
{
    private readonly string _cadenaConexion;

    // Forma de la fila que devuelve el CALL (una columna por parámetro INOUT).
    // MatchNamesWithUnderscores: p_id_codigo -> PIdCodigo.
    private sealed class FilaSolicitud
    {
        public long? PIdCodigo { get; set; }
        public string? PCodigo { get; set; }
        public string? PEnviadoA { get; set; }
        public string? PUsuario { get; set; }
    }

    public RepositorioCodigoRecuperacionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<(long IdCodigo, string Codigo, string EnviadoA, string Usuario)?> SolicitarAsync(
        string identificador, string? ip)
    {
        const string sql = @"
            CALL catedras.sp_solicitar_recuperacion(
                @identificador, CAST(@ip AS inet), NULL, NULL, NULL, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var fila = await conexion.QuerySingleAsync<FilaSolicitud>(sql, new { identificador, ip });

        if (fila.PIdCodigo is null || fila.PCodigo is null || fila.PEnviadoA is null || fila.PUsuario is null)
            return null;   // sin cuenta activa

        return (fila.PIdCodigo.Value, fila.PCodigo, fila.PEnviadoA, fila.PUsuario);
    }

    public async Task<string> RestablecerAsync(long idCodigo, string codigo, string claveHash)
    {
        const string sql = "CALL catedras.sp_restablecer_clave(@idCodigo, @codigo, @claveHash, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleAsync<string>(sql, new { idCodigo, codigo, claveHash });
    }
}
