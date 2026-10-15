// ==========================================================================
//  Repositorios/RepositorioRegistroAsistenciaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 3 métodos de IRepositorioRegistroAsistencia pasan por los
//  procedimientos de 'db/postgres/migraciones/037_sp_registro_asistencia.sql'
//  (molde: sede).
//
//  · CREAR manda SOLO fk_sesion, fk_asistente y origen. El trigger
//    trg_registro_validar calcula las instantáneas y valida ventana,
//    vinculación y cupo (RN-15, RN-22, RN-07, RN-38); si algo no cumple
//    llega una NpgsqlException. El procedimiento devuelve la fila DESPUÉS
//    del INSERT (ya con las instantáneas), igual que el RETURNING de antes.
//  · 'ip' llega ya como texto (host() en el SP).
//
//  La BD tiene además sp_actualizar_ / sp_eliminar_registro_asistencia
//  (criterio al pie de la letra), con guardas que protegen el HECHO. No se
//  exponen: IRepositorioRegistroAsistencia no los pide y la regla del
//  retrofit es no tocar la interfaz (ver AJUSTES_RETROFIT_SP_LOTE_FASE5.md).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.registro_asistencia' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioRegistroAsistenciaPostgres : IRepositorioRegistroAsistencia
{
    private readonly string _cadenaConexion;

    public RepositorioRegistroAsistenciaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RegistroAsistencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite)
    {
        const string sql = @"
            CALL catedras.sp_listar_registro_asistencia(
                @fkSesion::bigint, @fkAsistente::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { fkSesion, fkAsistente, limite });

        return LectorJsonSp.Lista<RegistroAsistencia>(json);
    }

    public async Task<RegistroAsistencia?> ObtenerPorIdAsync(long idRegistro)
    {
        const string sql = "CALL catedras.sp_consultar_registro_asistencia(@idRegistro, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idRegistro });

        return LectorJsonSp.Fila<RegistroAsistencia>(json);
    }

    public async Task<RegistroAsistencia> CrearAsync(long fkSesion, long fkAsistente, string origen)
    {
        const string sql = @"
            CALL catedras.sp_crear_registro_asistencia(
                @fkSesion, @fkAsistente, @origen, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { fkSesion, fkAsistente, origen });

        // Si el INSERT falló, ya saltó la excepción: aquí siempre hay fila.
        return LectorJsonSp.Fila<RegistroAsistencia>(json)!;
    }
}
