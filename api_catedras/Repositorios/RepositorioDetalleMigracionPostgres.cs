// ==========================================================================
//  Repositorios/RepositorioDetalleMigracionPostgres.cs  ·  Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  IRepositorioDetalleMigracion para PostgreSQL, vía los procedimientos de
//  '032_sp_detalle_migracion.sql' (molde de RepositorioSedePostgres).
//
//  Detalle del molde: la columna 'resultado' choca de nombre con el INOUT
//  'p_resultado' de todos los procedimientos, así que su parámetro en
//  sp_actualizar_detalle_migracion se llama 'p_resultado_migracion'. Aquí
//  se ve como el 2.º argumento del CALL.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.detalle_migracion' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioDetalleMigracionPostgres : IRepositorioDetalleMigracion
{
    private readonly string _cadenaConexion;

    public RepositorioDetalleMigracionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<DetalleMigracion>> ObtenerTodosAsync(long? fkLoteMigracion, int limite)
    {
        const string sql = "CALL catedras.sp_listar_detalle_migracion(@fkLoteMigracion, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkLoteMigracion, limite });

        return JsonSerializer.Deserialize<List<DetalleMigracion>>(json!) ?? [];
    }

    public async Task<DetalleMigracion?> ObtenerPorIdAsync(long idDetalle)
    {
        const string sql = "CALL catedras.sp_consultar_detalle_migracion(@idDetalle, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idDetalle });

        return json is null ? null : JsonSerializer.Deserialize<DetalleMigracion>(json);
    }

    public async Task<int> ActualizarAsync(long idDetalle, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual.
        const string sqlConsultar = "CALL catedras.sp_consultar_detalle_migracion(@idDetalle, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idDetalle });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<DetalleMigracion>(actualJson)!;

        // 2) Mezclar. Solo 'resultado' y 'mensaje' cambian; las columnas
        //    '*_enviado(a)' son copia literal de lo que salió en el archivo.
        string resultado = datos.TryGetValue("resultado", out var vResultado) ? (string)vResultado! : actual.Resultado;
        string? mensaje = datos.TryGetValue("mensaje", out var vMensaje) ? (string?)vMensaje : actual.Mensaje;

        // 3) Conjunto completo. Segundo ACEPTADO del mismo registro en otro
        //    lote -> ux_detmig_aceptado -> NpgsqlException (500, RF188).
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_detalle_migracion(
                @idDetalle, @resultado, @mensaje, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idDetalle, resultado, mensaje });

        return resultadoJson is null ? 0 : 1;
    }
}
