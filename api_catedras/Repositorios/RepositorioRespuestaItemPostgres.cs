// ==========================================================================
//  Repositorios/RepositorioRespuestaItemPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Solo lectura (igual que antes): Listar y Consultar pasan por los
//  procedimientos de 'db/postgres/migraciones/041_sp_respuesta_item.sql'.
//  Los ítems nacen dentro de sp_responder_encuesta (vía
//  fn_responder_encuesta_api), no desde este repositorio.
//
//  ⚠️ La BD tiene además sp_crear_ / sp_actualizar_ / sp_eliminar_
//  respuesta_item (criterio al pie de la letra), que se NIEGAN si la
//  respuesta madre está cerrada. NO se exponen: hecho auditable (D31/D32)
//  e IRepositorioRespuestaItem no los pide.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso (solo lectura) a 'catedras.respuesta_item' en PostgreSQL, vía procedimientos.</summary>
public class RepositorioRespuestaItemPostgres : IRepositorioRespuestaItem
{
    private readonly string _cadenaConexion;

    public RepositorioRespuestaItemPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RespuestaItem>> ObtenerTodosAsync(long? fkRespuesta, int limite)
    {
        const string sql = "CALL catedras.sp_listar_respuesta_item(@fkRespuesta::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkRespuesta, limite });

        return LectorJsonSp.Lista<RespuestaItem>(json);
    }

    public async Task<RespuestaItem?> ObtenerPorIdAsync(long idItem)
    {
        const string sql = "CALL catedras.sp_consultar_respuesta_item(@idItem, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idItem });

        return LectorJsonSp.Fila<RespuestaItem>(json);
    }
}
