// ==========================================================================
//  Repositorios/RepositorioProgramaAsistentePostgres.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioProgramaAsistente para PostgreSQL con Dapper. La llave
//  primaria son 3 columnas (asistente, programa, periodo): el UPDATE del
//  cambio de estado las usa todas en el WHERE.
//
//  'fk_periodo' es CHAR(6) en la BD: PostgreSQL rellena con espacios a la
//  derecha si el valor es más corto. Los periodos tienen siempre 6
//  caracteres ('2026-1'), y la petición lo exige, así que no hay relleno.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.programa_asistente' en PostgreSQL mediante Dapper.</summary>
public class RepositorioProgramaAsistentePostgres : IRepositorioProgramaAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioProgramaAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ProgramaAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        const string sql = @"
            SELECT fk_asistente, fk_programa, fk_periodo, estado
            FROM catedras.programa_asistente
            WHERE @fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente
            ORDER BY fk_asistente, fk_periodo DESC, fk_programa
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<ProgramaAsistente>(sql, new { fkAsistente, limite });
        return filas.ToList();
    }

    public async Task CrearAsync(ProgramaAsistente programa)
    {
        const string sql = @"
            INSERT INTO catedras.programa_asistente (fk_asistente, fk_programa, fk_periodo, estado)
            VALUES (@FkAsistente, @FkPrograma, @FkPeriodo, @Estado);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, programa);
    }

    public async Task<int> CambiarEstadoAsync(long fkAsistente, string fkPrograma, string fkPeriodo, string estado)
    {
        // La llave COMPLETA en el WHERE identifica UNA fila. Un estado fuera
        // de la lista lo rechazaría el CHECK de la BD (la petición ya lo filtra).
        const string sql = @"
            UPDATE catedras.programa_asistente
               SET estado = @estado
             WHERE fk_asistente = @fkAsistente
               AND fk_programa = @fkPrograma
               AND fk_periodo = @fkPeriodo;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { fkAsistente, fkPrograma, fkPeriodo, estado });
    }
}
