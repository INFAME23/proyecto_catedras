// ==========================================================================
//  Repositorios/RepositorioDocumentoAsistentePostgres.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioDocumentoAsistente para PostgreSQL con Dapper. CRUD
//  estándar. Las dos reglas fuertes de la tabla —un solo documento vigente
//  por persona (ux_documento_vigente) y número único por tipo
//  (uq_documento_numero)— las aplica el motor; aquí no se simulan (D9).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.documento_asistente' en PostgreSQL mediante Dapper.</summary>
public class RepositorioDocumentoAsistentePostgres : IRepositorioDocumentoAsistente
{
    private readonly string _cadenaConexion;

    // [ACID · C] Lista blanca del UPDATE. 'fk_asistente' no se cambia (un
    // documento no cambia de dueño); 'id_documento' y 'registrado_en' tampoco.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "fk_tipo_documento", "numero", "vigente" };

    public RepositorioDocumentoAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<DocumentoAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        // '@fkAsistente::bigint IS NULL OR ...': sin filtro -> todas las filas.
        const string sql = @"
            SELECT id_documento, fk_asistente, fk_tipo_documento, numero, vigente, registrado_en
            FROM catedras.documento_asistente
            WHERE @fkAsistente::bigint IS NULL OR fk_asistente = @fkAsistente
            ORDER BY id_documento
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<DocumentoAsistente>(sql, new { fkAsistente, limite });
        return filas.ToList();
    }

    public async Task<DocumentoAsistente?> ObtenerPorIdAsync(long idDocumento)
    {
        const string sql = @"
            SELECT id_documento, fk_asistente, fk_tipo_documento, numero, vigente, registrado_en
            FROM catedras.documento_asistente
            WHERE id_documento = @idDocumento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<DocumentoAsistente>(sql, new { idDocumento });
    }

    public async Task<(long IdDocumento, DateTime RegistradoEn)> CrearAsync(DocumentoAsistente documento)
    {
        // 'id_documento' (IDENTITY) y 'registrado_en' (DEFAULT now()) los
        // genera la BD; RETURNING los devuelve en la misma sentencia.
        const string sql = @"
            INSERT INTO catedras.documento_asistente (fk_asistente, fk_tipo_documento, numero, vigente)
            VALUES (@FkAsistente, @FkTipoDocumento, @Numero, @Vigente)
            RETURNING id_documento, registrado_en;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila con dos columnas -> una tupla (long, DateTime), en orden.
        return await conexion.QuerySingleAsync<(long, DateTime)>(sql, documento);
    }

    public async Task<int> ActualizarAsync(long idDocumento, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idDocumento);

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
            $"UPDATE catedras.documento_asistente SET {string.Join(", ", asignaciones)} " +
            "WHERE id_documento = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idDocumento)
    {
        // Ninguna tabla referencia a documento_asistente: este DELETE nunca
        // falla por integridad referencial (RF105).
        const string sql = "DELETE FROM catedras.documento_asistente WHERE id_documento = @idDocumento;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { idDocumento });
    }
}
