// ==========================================================================
//  Repositorios/RepositorioPonenciaPostgres.cs  ·  Proyecto Cátedras · v4
// --------------------------------------------------------------------------
//  IRepositorioPonencia para PostgreSQL con Dapper.
//
//  Como en VinculacionAsistente (v3), el WHERE que identifica UNA fila usa
//  las TRES columnas de la llave primaria. La diferencia (ADR D19) es el
//  verbo final: aquí es un DELETE, no un UPDATE que "cierra".
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.ponencia' en PostgreSQL mediante Dapper.</summary>
public class RepositorioPonenciaPostgres : IRepositorioPonencia
{
    private readonly string _cadenaConexion;

    public RepositorioPonenciaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Ponencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite)
    {
        // Dos filtros opcionales e independientes: cada uno con el mismo
        // truco '@x::bigint IS NULL OR columna = @x'. Unidos con AND, se
        // pueden usar solos o combinados.
        const string sql = @"
            SELECT fk_sesion, fk_asistente, rol
            FROM catedras.ponencia
            WHERE (@fkSesion::bigint IS NULL OR fk_sesion = @fkSesion)
              AND (@fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente)
            ORDER BY fk_sesion, fk_asistente, rol
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Ponencia>(sql, new { fkSesion, fkAsistente, limite });
        return filas.ToList();
    }

    public async Task<Ponencia?> ObtenerUnoAsync(long fkSesion, long fkAsistente, string rol)
    {
        // La llave COMPLETA en el WHERE: como mucho, una fila.
        const string sql = @"
            SELECT fk_sesion, fk_asistente, rol
            FROM catedras.ponencia
            WHERE fk_sesion = @fkSesion
              AND fk_asistente = @fkAsistente
              AND rol = @rol;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Ponencia>(sql, new { fkSesion, fkAsistente, rol });
    }

    public async Task CrearAsync(Ponencia ponencia)
    {
        // PK duplicada (pk_ponencia) o sesión/asistente inexistentes
        // (fk_ponencia_sesion / fk_ponencia_asistente) -> NpgsqlException.
        const string sql = @"
            INSERT INTO catedras.ponencia (fk_sesion, fk_asistente, rol)
            VALUES (@FkSesion, @FkAsistente, @Rol);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, ponencia);
    }

    public async Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol)
    {
        const string sql = @"
            DELETE FROM catedras.ponencia
            WHERE fk_sesion = @fkSesion
              AND fk_asistente = @fkAsistente
              AND rol = @rol;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { fkSesion, fkAsistente, rol });
    }
}
