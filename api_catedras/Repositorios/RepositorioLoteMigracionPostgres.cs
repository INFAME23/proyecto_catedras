// ==========================================================================
//  Repositorios/RepositorioLoteMigracionPostgres.cs  ·  Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  IRepositorioLoteMigracion para PostgreSQL. Nace YA con el molde del
//  retrofit (RepositorioSedePostgres): cada método es un 'CALL' a un
//  procedimiento de '031_sp_lote_migracion.sql' y la lectura del JSON que
//  devuelve.
//
//  CrearAsync -> sp_crear_lote_migracion, que a su vez llama al
//  sp_generar_lote_migracion que ya traía el script provisto (ADR D34): ahí
//  viven el cálculo del nombre del archivo, el conteo de filas migrables,
//  las filas de detalle y el cambio de la sesión a 'MIGRADA'. Esta clase no
//  replica nada de eso.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.lote_migracion' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioLoteMigracionPostgres : IRepositorioLoteMigracion
{
    private readonly string _cadenaConexion;

    public RepositorioLoteMigracionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<LoteMigracion>> ObtenerTodosAsync(long? fkSesion, int limite)
    {
        const string sql = "CALL catedras.sp_listar_lote_migracion(@fkSesion, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, limite });

        return JsonSerializer.Deserialize<List<LoteMigracion>>(json!) ?? [];
    }

    public async Task<LoteMigracion?> ObtenerPorIdAsync(long idLoteMigracion)
    {
        const string sql = "CALL catedras.sp_consultar_lote_migracion(@idLoteMigracion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idLoteMigracion });

        return json is null ? null : JsonSerializer.Deserialize<LoteMigracion>(json);
    }

    public async Task<LoteMigracion> CrearAsync(long fkSesion, long? fkUsuario)
    {
        const string sql = "CALL catedras.sp_crear_lote_migracion(@fkSesion, @fkUsuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Sesión sin registros migrables (RN-08/RN-28) -> el RAISE EXCEPTION
        // de sp_generar_lote_migracion sube como NpgsqlException y el CALL se
        // revierte entero: ni lote, ni detalles, ni sesión 'MIGRADA'.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkUsuario });

        return JsonSerializer.Deserialize<LoteMigracion>(json!)!;
    }

    public async Task<int> ActualizarAsync(long idLoteMigracion, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual (sp_actualizar reemplaza las 3 columnas que cambian
        //    con el tiempo: instancia_proceso_asis, fk_estado_proceso, observaciones).
        const string sqlConsultar = "CALL catedras.sp_consultar_lote_migracion(@idLoteMigracion, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idLoteMigracion });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<LoteMigracion>(actualJson)!;

        // 2) Mezclar lo que llegó sobre lo guardado.
        string? instanciaProcesoAsis = datos.TryGetValue("instancia_proceso_asis", out var vInstancia) ? (string?)vInstancia : actual.InstanciaProcesoAsis;
        string fkEstadoProceso = datos.TryGetValue("fk_estado_proceso", out var vEstado) ? (string)vEstado! : actual.FkEstadoProceso;
        string? observaciones = datos.TryGetValue("observaciones", out var vObs) ? (string?)vObs : actual.Observaciones;

        // 3) Conjunto completo. Estado inexistente -> FK fk_lotemig_estado -> 500.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_lote_migracion(
                @idLoteMigracion, @instanciaProcesoAsis, @fkEstadoProceso, @observaciones, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idLoteMigracion, instanciaProcesoAsis, fkEstadoProceso, observaciones });

        return resultadoJson is null ? 0 : 1;
    }
}
