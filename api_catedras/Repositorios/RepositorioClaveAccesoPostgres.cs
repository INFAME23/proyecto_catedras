// ==========================================================================
//  Repositorios/RepositorioClaveAccesoPostgres.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  IRepositorioClaveAcceso para PostgreSQL con Dapper.
//
//  · LEER: 'ip_solicitud' es inet; Dapper no lo convierte, así que se lee con
//    host(ip_solicitud) (da "10.0.0.1"; ::text daría "10.0.0.1/32"). El SELECT
//    NO trae 'clave_hash' (RNF12): la API no tiene por qué cargarlo.
//  · ESCRIBIR: Dapper tampoco sabe enviar un IPAddress. La IP viaja como texto
//    y el SQL la convierte con CAST(@ip AS inet) (mismo recurso que
//    RepositorioConsentimientoDatosPostgres, v3). El servicio ya comprobó que
//    es una IP válida (400), así que el CAST no debería fallar.
//  · SOLICITAR y VALIDAR llaman a FUNCIONES (SELECT * FROM fn_..._api), ADR
//    D24. Si la BD rechaza algo (RAISE EXCEPTION) llega una NpgsqlException y
//    el controlador responde 500 (ADR D25).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.clave_acceso' en PostgreSQL mediante Dapper.</summary>
public class RepositorioClaveAccesoPostgres : IRepositorioClaveAcceso
{
    private readonly string _cadenaConexion;

    // Todas las columnas MENOS 'clave_hash'.
    private const string Columnas = @"
            id_clave, fk_asistente, fk_enlace, enviado_a, es_correo_institucional,
            generado_en, expira_en, usado_en, intentos,
            host(ip_solicitud) AS ip_solicitud";

    // Forma de la fila que devuelve fn_solicitar_clave_api (id_clave, clave, enviado_a).
    // Una clase pequeña y no una tupla: Dapper enlaza por NOMBRE (con
    // MatchNamesWithUnderscores), sin depender del orden de las columnas.
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
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.clave_acceso
            WHERE @fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente
            ORDER BY id_clave
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<ClaveAcceso>(sql, new { fkAsistente, limite });
        return filas.ToList();
    }

    public async Task<ClaveAcceso?> ObtenerPorIdAsync(long idClave)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.clave_acceso
            WHERE id_clave = @idClave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<ClaveAcceso>(sql, new { idClave });
    }

    public async Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(
        string identificador, Guid token, string? ip)
    {
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
        const string sql = @"
            SELECT * FROM catedras.fn_validar_clave_api(
                @idClave, @clave, CAST(@ip AS inet), @userAgent);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteScalarAsync<long>(sql,
            new { idClave, clave, ip, userAgent });
    }
}
