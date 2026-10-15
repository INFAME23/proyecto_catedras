// ==========================================================================
//  Repositorios/RepositorioEstadoProcesoPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/016_sp_estado_proceso.sql', con el MISMO molde
//  de RepositorioSedePostgres.cs: esta clase solo arma el 'CALL', manda
//  los parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'estado_proceso' NO tiene 'activo' -> el eliminar es un DELETE físico.
//  'IRepositorioEstadoProceso', 'ServicioEstadoProceso',
//  'EstadoProcesoController' y las 'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.estado_proceso' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioEstadoProcesoPostgres : IRepositorioEstadoProceso
{
    private readonly string _cadenaConexion;

    public RepositorioEstadoProcesoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<EstadoProceso>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_estado_proceso(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<EstadoProceso>>(json!) ?? [];
    }

    public async Task<EstadoProceso?> ObtenerPorIdAsync(string idEstado)
    {
        const string sql = "CALL catedras.sp_consultar_estado_proceso(@idEstado, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idEstado });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<EstadoProceso>(json);
    }

    public async Task CrearAsync(EstadoProceso estado)
    {
        const string sql = @"
            CALL catedras.sp_crear_estado_proceso(
                @IdEstado, @Nombre, @EsFinal, @EsExitoso, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida o chk_estado_coherente -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, estado);
    }

    public async Task<int> ActualizarAsync(string idEstado, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_estado_proceso(@idEstado, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idEstado });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<EstadoProceso>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        bool esFinal = datos.TryGetValue("es_final", out var vFinal) ? (bool)vFinal! : actual.EsFinal;
        bool esExitoso = datos.TryGetValue("es_exitoso", out var vExitoso) ? (bool)vExitoso! : actual.EsExitoso;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_estado_proceso(
                @idEstado, @nombre, @esFinal, @esExitoso, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idEstado, nombre, esFinal, esExitoso });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idEstado)
    {
        const string sql = "CALL catedras.sp_eliminar_estado_proceso(@idEstado, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idEstado });

        // {"eliminado": true|false}. Sin 'activo': DELETE físico.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
