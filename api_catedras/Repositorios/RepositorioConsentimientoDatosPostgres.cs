// ==========================================================================
//  Repositorios/RepositorioConsentimientoDatosPostgres.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioConsentimientoDatos para PostgreSQL con Dapper. Sin UPDATE
//  ni DELETE.
//
//  Columna 'ip' de tipo INET: Npgsql la entrega como un objeto de red, no
//  como texto. Para que el modelo la reciba como 'string':
//   · al LEER se convierte en el SQL con 'host(ip)', que da solo la
//     dirección como texto ("10.0.0.1"; 'ip::text' daría "10.0.0.1/32").
//   · al ESCRIBIR se convierte también en el SQL: 'CAST(@Ip AS inet)'. Una
//     IP mal escrita hace fallar ese CAST -> NpgsqlException (500).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.consentimiento_datos' en PostgreSQL mediante Dapper.</summary>
public class RepositorioConsentimientoDatosPostgres : IRepositorioConsentimientoDatos
{
    private readonly string _cadenaConexion;

    public RepositorioConsentimientoDatosPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ConsentimientoDatos>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        // 'host(ip) AS ip': la IP como texto, con el mismo nombre de columna
        // para que Dapper la asigne a la propiedad 'Ip'.
        const string sql = @"
            SELECT id_consentimiento, fk_asistente, version_politica, aceptado_en,
                   host(ip) AS ip, canal
            FROM catedras.consentimiento_datos
            WHERE @fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente
            ORDER BY id_consentimiento
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<ConsentimientoDatos>(sql, new { fkAsistente, limite });
        return filas.ToList();
    }

    public async Task<ConsentimientoDatos?> ObtenerPorIdAsync(long idConsentimiento)
    {
        const string sql = @"
            SELECT id_consentimiento, fk_asistente, version_politica, aceptado_en,
                   host(ip) AS ip, canal
            FROM catedras.consentimiento_datos
            WHERE id_consentimiento = @idConsentimiento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<ConsentimientoDatos>(sql, new { idConsentimiento });
    }

    public async Task<(long IdConsentimiento, DateTime AceptadoEn)> CrearAsync(ConsentimientoDatos consentimiento)
    {
        // 'id_consentimiento' (IDENTITY) y 'aceptado_en' (DEFAULT now()) los
        // genera la BD y RETURNING los devuelve en la misma sentencia.
        const string sql = @"
            INSERT INTO catedras.consentimiento_datos (fk_asistente, version_politica, canal, ip)
            VALUES (@FkAsistente, @VersionPolitica, @Canal, CAST(@Ip AS inet))
            RETURNING id_consentimiento, aceptado_en;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleAsync<(long, DateTime)>(sql, consentimiento);
    }
}
