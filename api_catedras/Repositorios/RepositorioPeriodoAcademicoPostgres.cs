// ==========================================================================
//  Repositorios/RepositorioPeriodoAcademicoPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/009_sp_periodo_academico.sql', con el MISMO
//  molde de RepositorioSedePostgres.cs: esta clase solo arma el 'CALL',
//  manda los parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'periodo_academico' SÍ tiene 'activo' -> el eliminar es LÓGICO.
//  'IRepositorioPeriodoAcademico', 'ServicioPeriodoAcademico',
//  'PeriodoAcademicoController' y las 'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.periodo_academico' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioPeriodoAcademicoPostgres : IRepositorioPeriodoAcademico
{
    private readonly string _cadenaConexion;

    public RepositorioPeriodoAcademicoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<PeriodoAcademico>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_periodo_academico(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<PeriodoAcademico>>(json!) ?? [];
    }

    public async Task<PeriodoAcademico?> ObtenerPorIdAsync(string idPeriodo)
    {
        const string sql = "CALL catedras.sp_consultar_periodo_academico(@idPeriodo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idPeriodo });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<PeriodoAcademico>(json);
    }

    public async Task CrearAsync(PeriodoAcademico periodo)
    {
        // '::date' explícito: Dapper no conoce DateOnly y lo manda como
        // 'timestamp'. Un INSERT lo convertía solo, pero un CALL resuelve el
        // procedimiento por TIPOS y 'timestamp -> date' no es implícito:
        // sin el cast, PostgreSQL responde 42883 (procedure does not exist).
        const string sql = @"
            CALL catedras.sp_crear_periodo_academico(
                @IdPeriodo, @FechaIni::date, @FechaFin::date, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida, formato o rango inválido -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, periodo);
    }

    public async Task<int> ActualizarAsync(string idPeriodo, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_periodo_academico(@idPeriodo, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idPeriodo });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<PeriodoAcademico>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        //    Un PATCH que solo mueve 'fecha_fin' se cruza aquí con la
        //    'fecha_ini' guardada; chk_periodo_fechas sigue vigilando el rango.
        DateOnly fechaIni = datos.TryGetValue("fecha_ini", out var vIni) ? (DateOnly)vIni! : actual.FechaIni;
        DateOnly fechaFin = datos.TryGetValue("fecha_fin", out var vFin) ? (DateOnly)vFin! : actual.FechaFin;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_periodo_academico(
                @idPeriodo, @fechaIni::date, @fechaFin::date, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idPeriodo, fechaIni, fechaFin, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idPeriodo)
    {
        const string sql = "CALL catedras.sp_eliminar_periodo_academico(@idPeriodo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idPeriodo });

        // {"eliminado": true|false}. SÍ tiene 'activo': borrado LÓGICO.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
