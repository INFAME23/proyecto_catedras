// ==========================================================================
//  Repositorios/RepositorioTipoVinculacionPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/014_sp_tipo_vinculacion.sql', con el MISMO
//  molde de RepositorioSedePostgres.cs: esta clase solo arma el 'CALL',
//  manda los parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'tipo_vinculacion' SÍ tiene 'activo' -> el eliminar es LÓGICO.
//  'IRepositorioTipoVinculacion', 'ServicioTipoVinculacion',
//  'TipoVinculacionController' y las 'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.tipo_vinculacion' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioTipoVinculacionPostgres : IRepositorioTipoVinculacion
{
    private readonly string _cadenaConexion;

    public RepositorioTipoVinculacionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoVinculacion>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_tipo_vinculacion(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<TipoVinculacion>>(json!) ?? [];
    }

    public async Task<TipoVinculacion?> ObtenerPorIdAsync(string idTipoVinculacion)
    {
        const string sql = "CALL catedras.sp_consultar_tipo_vinculacion(@idTipoVinculacion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idTipoVinculacion });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<TipoVinculacion>(json);
    }

    public async Task CrearAsync(TipoVinculacion tipoVinculacion)
    {
        const string sql = @"
            CALL catedras.sp_crear_tipo_vinculacion(
                @IdTipoVinculacion, @Nombre, @EsInterno, @ExigePrograma, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, tipoVinculacion);
    }

    public async Task<int> ActualizarAsync(string idTipoVinculacion, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_tipo_vinculacion(@idTipoVinculacion, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idTipoVinculacion });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<TipoVinculacion>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        bool esInterno = datos.TryGetValue("es_interno", out var vInterno) ? (bool)vInterno! : actual.EsInterno;
        bool exigePrograma = datos.TryGetValue("exige_programa", out var vExige) ? (bool)vExige! : actual.ExigePrograma;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_tipo_vinculacion(
                @idTipoVinculacion, @nombre, @esInterno, @exigePrograma, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idTipoVinculacion, nombre, esInterno, exigePrograma, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idTipoVinculacion)
    {
        const string sql = "CALL catedras.sp_eliminar_tipo_vinculacion(@idTipoVinculacion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idTipoVinculacion });

        // {"eliminado": true|false}. SÍ tiene 'activo': borrado LÓGICO.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
