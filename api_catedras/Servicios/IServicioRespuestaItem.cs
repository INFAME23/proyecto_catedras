// ==========================================================================
//  Servicios/IServicioRespuestaItem.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de negocio de 'RespuestaItem'. Solo lectura (RF176-RF178).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;

/// <summary>Reglas de negocio (solo lectura) del recurso 'RespuestaItem'.</summary>
public interface IServicioRespuestaItem
{
    Task<(List<RespuestaItem> Datos, int Total)> ListarAsync(long? fkRespuesta, int? limite);
    Task<RespuestaItem> ObtenerAsync(long idItem);
}
