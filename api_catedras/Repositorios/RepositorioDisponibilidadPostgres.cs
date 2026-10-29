// ==========================================================================
//  Repositorios/RepositorioDisponibilidadPostgres.cs  ·  Cátedras · Subversión v7.1 · D52
// --------------------------------------------------------------------------
//  Llama a los dos procedimientos de 'db/postgres/migraciones/050_disponibilidad_sesiones.sql'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Consultas de disponibilidad sobre 'catedras.sesion' en PostgreSQL, vía procedimientos.</summary>
public class RepositorioDisponibilidadPostgres : IRepositorioDisponibilidad
{
    private readonly string _cadenaConexion;

    public RepositorioDisponibilidadPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<SesionOcupada>> ConflictosAsync(
        DateTimeOffset inicio, DateTimeOffset fin, string? fkSede, string? lugar,
        long? fkAsistente, long? excluirSesion)
    {
        const string sql = @"
            CALL catedras.sp_sesiones_en_conflicto(
                @inicio, @fin, @fkSede, @lugar, @fkAsistente::bigint, @excluirSesion::bigint, NULL);";

        // Npgsql solo escribe un timestamptz con desfase 0: la hora (con su zona) se pasa a UTC.
        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { inicio = inicio.UtcDateTime, fin = fin.UtcDateTime, fkSede, lugar, fkAsistente, excluirSesion });

        return LectorJsonSp.Lista<SesionOcupada>(json);
    }

    public async Task<List<SesionOcupada>> AgendaAsync(DateTimeOffset desde, DateTimeOffset hasta, string? fkSede, int limite)
    {
        const string sql = "CALL catedras.sp_agenda_sesiones(@desde, @hasta, @fkSede, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { desde = desde.UtcDateTime, hasta = hasta.UtcDateTime, fkSede, limite });

        return LectorJsonSp.Lista<SesionOcupada>(json);
    }
}
