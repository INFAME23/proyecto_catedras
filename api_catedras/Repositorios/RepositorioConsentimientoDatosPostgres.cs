// ==========================================================================
//  Repositorios/RepositorioConsentimientoDatosPostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioConsentimientoDatos para PostgreSQL, vía los procedimientos
//  de '028_sp_consentimiento_datos.sql'. SIGUE sin UPDATE ni DELETE.
//
//  ⚠ Ley 1581 (RF113): el contrato tiene 3 operaciones y se queda así.
//  La BD ya tiene sp_actualizar_ y sp_eliminar_consentimiento_datos (para
//  cumplir el criterio 3 al pie de la letra), pero NINGÚN método de esta
//  clase los llama, y no se deben agregar a IRepositorioConsentimientoDatos
//  ni al controlador sin una decisión explícita del equipo: un
//  consentimiento registrado es evidencia legal.
//
//  Columna 'ip' (INET): la conversión a/desde texto se hace ahora DENTRO de
//  los procedimientos (host(ip) al leer; '::inet' al escribir). Una IP mal
//  escrita sigue fallando en el motor -> NpgsqlException (500).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.consentimiento_datos' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioConsentimientoDatosPostgres : IRepositorioConsentimientoDatos
{
    private readonly string _cadenaConexion;

    public RepositorioConsentimientoDatosPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ConsentimientoDatos>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        const string sql = "CALL catedras.sp_listar_consentimiento_datos(@fkAsistente, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkAsistente, limite });

        return JsonSerializer.Deserialize<List<ConsentimientoDatos>>(json!) ?? [];
    }

    public async Task<ConsentimientoDatos?> ObtenerPorIdAsync(long idConsentimiento)
    {
        const string sql = "CALL catedras.sp_consultar_consentimiento_datos(@idConsentimiento, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idConsentimiento });

        return json is null ? null : JsonSerializer.Deserialize<ConsentimientoDatos>(json);
    }

    public async Task<(long IdConsentimiento, DateTime AceptadoEn)> CrearAsync(ConsentimientoDatos consentimiento)
    {
        // 'id_consentimiento' (IDENTITY) y 'aceptado_en' (now()) los pone la
        // BD; el procedimiento devuelve la fila completa y de ahí se toman.
        const string sql = @"
            CALL catedras.sp_crear_consentimiento_datos(
                @FkAsistente, @VersionPolitica, @Canal, @Ip, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            consentimiento.FkAsistente,
            consentimiento.VersionPolitica,
            consentimiento.Canal,
            consentimiento.Ip,
        });

        var guardado = JsonSerializer.Deserialize<ConsentimientoDatos>(json!)!;
        return (guardado.IdConsentimiento, guardado.AceptadoEn);
    }
}
