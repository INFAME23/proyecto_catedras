// ==========================================================================
//  Repositorios/RepositorioTipoEventoPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/021_sp_tipo_evento.sql', con el MISMO molde de
//  RepositorioSedePostgres.cs: esta clase solo arma el 'CALL', manda los
//  parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'tipo_evento' SÍ tiene 'activo' -> el eliminar es LÓGICO.
//  'IRepositorioTipoEvento', 'ServicioTipoEvento', 'TipoEventoController'
//  y las 'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.tipo_evento' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioTipoEventoPostgres : IRepositorioTipoEvento
{
    private readonly string _cadenaConexion;

    public RepositorioTipoEventoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoEvento>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_tipo_evento(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<TipoEvento>>(json!) ?? [];
    }

    public async Task<TipoEvento?> ObtenerPorIdAsync(string idTipoEvento)
    {
        const string sql = "CALL catedras.sp_consultar_tipo_evento(@idTipoEvento, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idTipoEvento });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<TipoEvento>(json);
    }

    public async Task CrearAsync(TipoEvento tipoEvento)
    {
        const string sql = @"
            CALL catedras.sp_crear_tipo_evento(
                @IdTipoEvento, @Nombre, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, tipoEvento);
    }

    public async Task<int> ActualizarAsync(string idTipoEvento, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_tipo_evento(@idTipoEvento, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idTipoEvento });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<TipoEvento>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_tipo_evento(
                @idTipoEvento, @nombre, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idTipoEvento, nombre, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idTipoEvento)
    {
        const string sql = "CALL catedras.sp_eliminar_tipo_evento(@idTipoEvento, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idTipoEvento });

        // {"eliminado": true|false}. SÍ tiene 'activo': borrado LÓGICO.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
