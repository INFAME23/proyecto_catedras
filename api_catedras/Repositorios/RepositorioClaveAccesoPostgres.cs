// ==========================================================================
//  Repositorios/RepositorioClaveAccesoPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar y Consultar pasan por los procedimientos de
//  'db/postgres/migraciones/009_sp_clave_acceso.sql' (molde: sede). El
//  JSON NO trae 'clave_hash' (RNF12), igual que el SELECT de antes, e
//  'ip_solicitud' llega ya como texto ("10.0.0.1", host() en el SP).
//
//  SOLICITAR y VALIDAR NO CAMBIARON: siguen llamando a las funciones de
//  negocio fn_solicitar_clave_api y fn_validar_clave_api (ADR D24). La IP
//  viaja como texto y se convierte con CAST(@ip AS inet). Si la BD rechaza
//  algo (RAISE EXCEPTION) llega una NpgsqlException (ADR D25).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.clave_acceso' en PostgreSQL, vía procedimientos y funciones.</summary>
public class RepositorioClaveAccesoPostgres : IRepositorioClaveAcceso
{
    private readonly string _cadenaConexion;

    // Forma de la fila que devuelve fn_solicitar_clave_api (id_clave, clave, enviado_a).
    private sealed class FilaSolicitud
    {
        public long IdClave { get; set; }
        public string Clave { get; set; } = "";
        public string EnviadoA { get; set; } = "";
    }

    public RepositorioClaveAccesoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ClaveAcceso>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        const string sql = "CALL catedras.sp_listar_clave_acceso(@fkAsistente::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkAsistente, limite });

        return LectorJsonSp.Lista<ClaveAcceso>(json);
    }

    public async Task<ClaveAcceso?> ObtenerPorIdAsync(long idClave)
    {
        const string sql = "CALL catedras.sp_consultar_clave_acceso(@idClave, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idClave });

        return LectorJsonSp.Fila<ClaveAcceso>(json);
    }

    public async Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(
        string identificador, Guid token, string? ip)
    {
        // SIN CAMBIOS: función de negocio del script provisto.
        const string sql = @"
            SELECT * FROM catedras.fn_solicitar_clave_api(
                @identificador, @token, CAST(@ip AS inet));";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var fila = await conexion.QuerySingleAsync<FilaSolicitud>(sql,
            new { identificador, token, ip });
        return (fila.IdClave, fila.Clave, fila.EnviadoA);
    }

    public async Task<long> ValidarAsync(long idClave, string clave, string? ip, string? userAgent)
    {
        // SIN CAMBIOS: función de negocio del script provisto.
        const string sql = @"
            SELECT * FROM catedras.fn_validar_clave_api(
                @idClave, @clave, CAST(@ip AS inet), @userAgent);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteScalarAsync<long>(sql,
            new { idClave, clave, ip, userAgent });
    }
}
