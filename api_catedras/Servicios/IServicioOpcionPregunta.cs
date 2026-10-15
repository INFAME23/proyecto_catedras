// ==========================================================================
//  Servicios/IServicioOpcionPregunta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de negocio de 'OpcionPregunta'. Mismo molde que la v1.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'OpcionPregunta'.</summary>
public interface IServicioOpcionPregunta
{
    Task<(List<OpcionPregunta> Datos, int Total)> ListarAsync(long? fkPregunta, int? limite);
    Task<OpcionPregunta> ObtenerAsync(long idOpcion);
    Task<long> CrearAsync(OpcionPreguntaCrear peticion);
    Task<int> ReemplazarAsync(long idOpcion, OpcionPreguntaReemplazo peticion);
    Task<int> ActualizarAsync(long idOpcion, OpcionPreguntaActualizar peticion);
    Task<int> EliminarAsync(long idOpcion);
}
