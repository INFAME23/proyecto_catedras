// ==========================================================================
//  Repositorios/RepositorioQrSesionPostgres.cs  ·  Cátedras · Subversión v7.1 · D53
// --------------------------------------------------------------------------
//  Llama a los tres procedimientos de 'db/postgres/migraciones/051_qr_sesion.sql'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso al QR de cierre de 'catedras.enlace_registro' en PostgreSQL, vía procedimientos.</summary>
public class RepositorioQrSesionPostgres : IRepositorioQrSesion
{
    private readonly string _cadenaConexion;

    public RepositorioQrSesionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<QrSesion?> EmitirAsync(long fkSesion, long? fkUsuario, bool regenerar)
    {
        const string sql = "CALL catedras.sp_emitir_qr_sesion(@fkSesion, @fkUsuario::bigint, @regenerar, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkUsuario, regenerar });

        return LectorJsonSp.Fila<QrSesion>(json);
    }

    public async Task<QrSesion?> ConsultarAsync(long fkSesion)
    {
        const string sql = "CALL catedras.sp_consultar_qr_sesion(@fkSesion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion });

        return LectorJsonSp.Fila<QrSesion>(json);
    }

    public async Task<List<AsistenciaSesion>> AsistenciaAsync(long fkSesion)
    {
        const string sql = "CALL catedras.sp_asistencia_sesion(@fkSesion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion });

        return LectorJsonSp.Lista<AsistenciaSesion>(json);
    }
}
