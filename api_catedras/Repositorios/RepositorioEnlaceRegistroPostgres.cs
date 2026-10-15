// ==========================================================================
//  Repositorios/RepositorioEnlaceRegistroPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar, Consultar y Revocar pasan por los procedimientos de
//  'db/postgres/migraciones/035_sp_enlace_registro.sql' (molde: sede).
//
//  EMITIR NO CAMBIÓ: sigue llamando a la función de negocio
//  fn_emitir_enlace_api (ADR D24), que hace CALL a sp_emitir_enlace. Esa
//  función calcula token, url, canal y ventana, y revoca el enlace vigente
//  anterior (RN-14). No se reemplaza.
//
//  REVOCAR antes era un UPDATE suelto aquí; ahora es
//  'sp_revocar_enlace_registro', con la misma guarda 'revocado_en IS NULL'.
//
//  'ventana' (tstzrange) llega ya partida en ventana_desde/ventana_hasta
//  dentro del JSON del procedimiento.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.enlace_registro' en PostgreSQL, vía procedimientos y funciones.</summary>
public class RepositorioEnlaceRegistroPostgres : IRepositorioEnlaceRegistro
{
    private readonly string _cadenaConexion;

    public RepositorioEnlaceRegistroPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<EnlaceRegistro>> ObtenerTodosAsync(long? fkSesion, int limite)
    {
        const string sql = "CALL catedras.sp_listar_enlace_registro(@fkSesion::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, limite });

        return LectorJsonSp.Lista<EnlaceRegistro>(json);
    }

    public async Task<EnlaceRegistro?> ObtenerPorIdAsync(long idEnlace)
    {
        const string sql = "CALL catedras.sp_consultar_enlace_registro(@idEnlace, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idEnlace });

        return LectorJsonSp.Fila<EnlaceRegistro>(json);
    }

    public async Task<long> EmitirAsync(long fkSesion, int? minutosAntes, int? minutosDespues, long? fkUsuario)
    {
        // SIN CAMBIOS: función de negocio del script provisto (ADR D24).
        // Los parámetros opcionales viajan como NULL y la función aplica sus
        // valores por defecto (tabla 'parametro' o 30 minutos).
        const string sql = @"
            SELECT * FROM catedras.fn_emitir_enlace_api(
                @fkSesion, @minutosAntes, @minutosDespues, @fkUsuario);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteScalarAsync<long>(sql,
            new { fkSesion, minutosAntes, minutosDespues, fkUsuario });
    }

    public async Task<int> RevocarAsync(long idEnlace)
    {
        const string sql = "CALL catedras.sp_revocar_enlace_registro(@idEnlace, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idEnlace });

        // NULL = no existe o ya estaba revocado -> "0 filas" (el servicio
        // responde 400/404 igual que antes).
        return json is null ? 0 : 1;
    }
}
