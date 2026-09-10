// ==========================================================================
//  Repositorios/RepositorioPeriodoAcademicoPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioPeriodoAcademico para PostgreSQL con
//  Dapper. Las columnas DATE ('fecha_ini'/'fecha_fin') se mapean a
//  DateOnly gracias al 'ManejadorTipoDateOnly' ya registrado en
//  Program.cs (el mismo que usa Encuesta) — no hace falta configurar
//  nada más aquí.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.periodo_academico' en PostgreSQL mediante Dapper.</summary>
public class RepositorioPeriodoAcademicoPostgres : IRepositorioPeriodoAcademico
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "fecha_ini", "fecha_fin", "activo" };

    public RepositorioPeriodoAcademicoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<PeriodoAcademico>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_periodo, fecha_ini, fecha_fin, activo
            FROM catedras.periodo_academico
            ORDER BY id_periodo
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<PeriodoAcademico>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<PeriodoAcademico?> ObtenerPorIdAsync(string idPeriodo)
    {
        const string sql = @"
            SELECT id_periodo, fecha_ini, fecha_fin, activo
            FROM catedras.periodo_academico
            WHERE id_periodo = @idPeriodo;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<PeriodoAcademico>(sql, new { idPeriodo });
    }

    public async Task CrearAsync(PeriodoAcademico periodo)
    {
        const string sql = @"
            INSERT INTO catedras.periodo_academico (id_periodo, fecha_ini, fecha_fin, activo)
            VALUES (@IdPeriodo, @FechaIni, @FechaFin, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, periodo);
    }

    public async Task<int> ActualizarAsync(string idPeriodo, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idPeriodo);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        var sql =
            $"UPDATE catedras.periodo_academico SET {string.Join(", ", asignaciones)} " +
            "WHERE id_periodo = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idPeriodo)
    {
        const string sql = "DELETE FROM catedras.periodo_academico WHERE id_periodo = @idPeriodo;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el periodo tiene sesiones o matrículas asociadas,
        // fk_sesion_periodo / fk_progasi_periodo (ON DELETE RESTRICT)
        // hacen que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idPeriodo });
    }
}
