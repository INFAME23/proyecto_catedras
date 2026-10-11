// ==========================================================================
//  Repositorios/RepositorioRegistroAsistenciaPostgres.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  IRepositorioRegistroAsistencia para PostgreSQL con Dapper.
//
//  · 'ip' es inet: se lee con host(ip) (ver RepositorioClaveAccesoPostgres).
//  · CrearAsync es un INSERT directo con SOLO fk_sesion, fk_asistente y
//    origen. 'fk_programa_snapshot' y 'fk_vinculacion_snapshot' NO se mandan:
//    el trigger trg_registro_validar los calcula (RN-22, RN-07) y además
//    valida la ventana del enlace, la vinculación vigente y el cupo
//    (RN-15, RN-38). Si algo no cumple, el trigger lanza una excepción y
//    llega una NpgsqlException (500); la API no revalida lo que ya vigila la BD.
//  · RETURNING devuelve la fila ya modificada por el trigger, para que la
//    respuesta incluya las instantáneas sin un SELECT adicional.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.registro_asistencia' en PostgreSQL mediante Dapper.</summary>
public class RepositorioRegistroAsistenciaPostgres : IRepositorioRegistroAsistencia
{
    private readonly string _cadenaConexion;

    private const string Columnas = @"
            id_registro, fk_sesion, fk_asistente, fk_enlace, fk_clave,
            registrado_en, fk_programa_snapshot, fk_vinculacion_snapshot,
            origen, host(ip) AS ip, user_agent";

    public RepositorioRegistroAsistenciaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RegistroAsistencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.registro_asistencia
            WHERE (@fkSesion::bigint IS NULL OR fk_sesion = @fkSesion)
              AND (@fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente)
            ORDER BY id_registro
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<RegistroAsistencia>(sql,
            new { fkSesion, fkAsistente, limite });
        return filas.ToList();
    }

    public async Task<RegistroAsistencia?> ObtenerPorIdAsync(long idRegistro)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.registro_asistencia
            WHERE id_registro = @idRegistro;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<RegistroAsistencia>(sql, new { idRegistro });
    }

    public async Task<RegistroAsistencia> CrearAsync(long fkSesion, long fkAsistente, string origen)
    {
        string sql = $@"
            INSERT INTO catedras.registro_asistencia (fk_sesion, fk_asistente, origen)
            VALUES (@fkSesion, @fkAsistente, @origen)
            RETURNING {Columnas};";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleAsync<RegistroAsistencia>(sql,
            new { fkSesion, fkAsistente, origen });
    }
}
