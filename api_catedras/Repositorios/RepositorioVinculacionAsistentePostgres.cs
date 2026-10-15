// ==========================================================================
//  Repositorios/RepositorioVinculacionAsistentePostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioVinculacionAsistente para PostgreSQL, vía los procedimientos
//  de '026_sp_vinculacion_asistente.sql'.
//
//  Tabla de LLAVE COMPUESTA (ADR D16): el contrato sigue teniendo SOLO 3
//  operaciones, y no se le agregan más. Cómo encajan en los 5 verbos:
//     ObtenerTodosAsync -> sp_listar_vinculacion_asistente
//     CrearAsync        -> sp_crear_vinculacion_asistente
//     CerrarAsync       -> sp_eliminar_vinculacion_asistente
//  "Eliminar" en esta tabla ES cerrar (poner fecha_fin): la historia de
//  vinculaciones nunca se borra. sp_consultar_ y sp_actualizar_ existen en
//  la BD pero este contrato no los necesita (el servicio busca la llave en
//  la lista del asistente, como en la v3).
//
//  FECHAS: el ManejadorTipoDateOnly (Program.cs) manda cada DateOnly como un
//  DateTime, que Npgsql tipa como 'timestamp'. Un procedimiento con un
//  parámetro 'date' NO acepta 'timestamp' sin conversión explícita (no es
//  un cast implícito), así que cada fecha va con 'CAST(@x AS date)'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.vinculacion_asistente' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioVinculacionAsistentePostgres : IRepositorioVinculacionAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioVinculacionAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<VinculacionAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        const string sql = "CALL catedras.sp_listar_vinculacion_asistente(@fkAsistente, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkAsistente, limite });

        // 'fecha_ini'/'fecha_fin' llegan como "2026-01-20": System.Text.Json
        // las convierte a DateOnly sin ayuda.
        return JsonSerializer.Deserialize<List<VinculacionAsistente>>(json!) ?? [];
    }

    public async Task CrearAsync(VinculacionAsistente vinculacion)
    {
        // Nace abierta ('fecha_fin' NULL). Llave repetida o FK inexistente
        // -> NpgsqlException (500), igual que antes.
        const string sql = @"
            CALL catedras.sp_crear_vinculacion_asistente(
                @FkAsistente, @FkTipoVinculacion, CAST(@FechaIni AS date), NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // El contrato no devuelve nada: el servicio ya tiene la llave completa.
        await conexion.ExecuteAsync(sql, new
        {
            vinculacion.FkAsistente,
            vinculacion.FkTipoVinculacion,
            vinculacion.FechaIni,
        });
    }

    public async Task<int> CerrarAsync(long fkAsistente, string fkTipoVinculacion,
                                       DateOnly fechaIni, DateOnly fechaFin)
    {
        // Cerrar = sp_eliminar_vinculacion_asistente: pone 'fecha_fin' solo
        // si la fila seguía abierta ('AND fecha_fin IS NULL' dentro del
        // procedimiento: una petición simultánea no puede cerrarla dos veces).
        const string sql = @"
            CALL catedras.sp_eliminar_vinculacion_asistente(
                @fkAsistente, @fkTipoVinculacion,
                CAST(@fechaIni AS date), CAST(@fechaFin AS date), NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(
            sql, new { fkAsistente, fkTipoVinculacion, fechaIni, fechaFin });

        // {"eliminado": true|false} -> 1 | 0 (0 = no existe o ya estaba cerrada).
        using var documento = JsonDocument.Parse(json!);
        bool cerrada = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return cerrada ? 1 : 0;
    }
}
