// ==========================================================================
//  Servicios/IServicioNovedadCarga.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'NovedadCarga': SOLO lectura (RF118).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;

/// <summary>Reglas de negocio (solo lectura) del recurso 'NovedadCarga'.</summary>
public interface IServicioNovedadCarga
{
    Task<(List<NovedadCarga> Datos, int Total)> ListarAsync(long? fkLoteCarga, int? limite);
    Task<NovedadCarga> ObtenerAsync(long idNovedad);
}
