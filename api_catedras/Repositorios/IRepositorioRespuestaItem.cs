// ==========================================================================
//  Repositorios/IRepositorioRespuestaItem.cs  ·  Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.respuesta_item'. SOLO LECTURA
//  (RF176-RF178): nace exclusivamente dentro de "responder"
//  (fn_responder_encuesta_api), así que no hay ningún método de escritura
//  aquí — mismo criterio que 'NovedadCarga' en v3.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos (solo lectura) sobre 'catedras.respuesta_item'.</summary>
public interface IRepositorioRespuestaItem
{
    /// <summary>Ítems de respuesta, filtrados por respuesta si se da, hasta 'limite' filas.</summary>
    Task<List<RespuestaItem>> ObtenerTodosAsync(long? fkRespuesta, int limite);

    Task<RespuestaItem?> ObtenerPorIdAsync(long idItem);
}
