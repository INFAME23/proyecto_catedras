// ==========================================================================
//  Repositorios/RepositorioRespuestaItemPostgres.cs  ·  Cátedras · v6
// --------------------------------------------------------------------------
//  IRepositorioRespuestaItem para PostgreSQL con Dapper. Solo lectura: no
//  hay 'CrearAsync' porque la fila nace dentro de 'sp_cerrar_encuesta'
//  (invocado desde 'fn_responder_encuesta_api'), no desde este repositorio.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso (solo lectura) a 'catedras.respuesta_item' en PostgreSQL mediante Dapper.</summary>
public class RepositorioRespuestaItemPostgres : IRepositorioRespuestaItem
{
    private readonly string _cadenaConexion;

    private const string Columnas =
        "id_item, fk_respuesta, fk_pregunta, valor_numerico, valor_texto, fk_opcion";

    public RepositorioRespuestaItemPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RespuestaItem>> ObtenerTodosAsync(long? fkRespuesta, int limite)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.respuesta_item
            WHERE (@fkRespuesta::bigint IS NULL OR fk_respuesta = @fkRespuesta)
            ORDER BY fk_respuesta, fk_pregunta
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<RespuestaItem>(sql, new { fkRespuesta, limite });
        return filas.ToList();
    }

    public async Task<RespuestaItem?> ObtenerPorIdAsync(long idItem)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.respuesta_item
            WHERE id_item = @idItem;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<RespuestaItem>(sql, new { idItem });
    }
}
