// ==========================================================================
//  Repositorios/RepositorioRestriccionProgramacionPostgres.cs  ·  Cátedras · v7.1 · D57
// --------------------------------------------------------------------------
//  Todo pasa por los procedimientos de 'db/postgres/migraciones/056_restriccion_programacion.sql'.
//  Las horas viajan como texto 'HH:mm' y los días como '1,2,3' (la base los convierte).
//  Npgsql solo escribe 'timestamptz' con desfase 0: ValidarAsync pasa los instantes en UTC.
//  El manejador de DateOnly de Program.cs envía las fechas como timestamp: en el CALL se convierten con '::date'
//  (sin eso PostgreSQL no encuentra el procedimiento, que recibe 'date').
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.restriccion_programacion' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioRestriccionProgramacionPostgres : IRepositorioRestriccionProgramacion
{
    private readonly string _cadenaConexion;

    public RepositorioRestriccionProgramacionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RestriccionProgramacion>> ObtenerTodosAsync(bool soloActivas, int limite)
    {
        const string sql = "CALL catedras.sp_listar_restriccion_programacion(@soloActivas, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { soloActivas, limite });

        return JsonSerializer.Deserialize<List<RestriccionProgramacion>>(json!) ?? [];
    }

    public async Task<RestriccionProgramacion?> ObtenerUnoAsync(long idRestriccion)
    {
        const string sql = "CALL catedras.sp_consultar_restriccion_programacion(@idRestriccion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idRestriccion });

        return json is null ? null : JsonSerializer.Deserialize<RestriccionProgramacion>(json);
    }

    public async Task<RestriccionProgramacion> CrearAsync(string nombre, DateOnly fechaDesde, DateOnly fechaHasta,
                                                          string? horaDesde, string? horaHasta, string? dias, bool activo)
    {
        const string sql = @"
            CALL catedras.sp_crear_restriccion_programacion(
                @nombre, @fechaDesde::date, @fechaHasta::date, @horaDesde, @horaHasta, @dias, @activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { nombre, fechaDesde, fechaHasta, horaDesde, horaHasta, dias, activo });

        return JsonSerializer.Deserialize<RestriccionProgramacion>(json!)!;
    }

    public async Task<RestriccionProgramacion?> ReemplazarAsync(long idRestriccion, string nombre, DateOnly fechaDesde,
        DateOnly fechaHasta, string? horaDesde, string? horaHasta, string? dias, bool activo)
    {
        const string sql = @"
            CALL catedras.sp_actualizar_restriccion_programacion(
                @idRestriccion, @nombre, @fechaDesde::date, @fechaHasta::date, @horaDesde, @horaHasta, @dias, @activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { idRestriccion, nombre, fechaDesde, fechaHasta, horaDesde, horaHasta, dias, activo });

        // NULL = la ventana no existe.
        return json is null ? null : JsonSerializer.Deserialize<RestriccionProgramacion>(json);
    }

    public async Task<int> EliminarAsync(long idRestriccion)
    {
        const string sql = "CALL catedras.sp_eliminar_restriccion_programacion(@idRestriccion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idRestriccion });

        using var documento = JsonDocument.Parse(json!);
        return documento.RootElement.GetProperty("eliminado").GetBoolean() ? 1 : 0;
    }

    public async Task<ValidacionHorario> ValidarAsync(DateTimeOffset inicio, DateTimeOffset fin)
    {
        const string sql = "CALL catedras.sp_validar_horario_programacion(@inicio, @fin, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { inicio = inicio.UtcDateTime, fin = fin.UtcDateTime });

        return JsonSerializer.Deserialize<ValidacionHorario>(json!)!;
    }
}
