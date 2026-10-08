// ==========================================================================
//  Servicios/IServicioConsentimientoDatos.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'ConsentimientoDatos': listar, obtener y crear.
//  Sin métodos de actualizar ni eliminar (RF113).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'ConsentimientoDatos'.</summary>
public interface IServicioConsentimientoDatos
{
    Task<(List<ConsentimientoDatos> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite);
    Task<ConsentimientoDatos> ObtenerAsync(long idConsentimiento);
    Task<ConsentimientoDatos> CrearAsync(ConsentimientoDatosCrear peticion);
}
