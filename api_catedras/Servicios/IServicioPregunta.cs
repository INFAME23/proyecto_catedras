// ==========================================================================
//  Servicios/IServicioPregunta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de negocio de 'Pregunta'. Mismo molde que la v1:
//  ArgumentException -> 400 · NoEncontradoExcepcion -> 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Pregunta'.</summary>
public interface IServicioPregunta
{
    Task<(List<Pregunta> Datos, int Total)> ListarAsync(long? fkEncuesta, int? limite);
    Task<Pregunta> ObtenerAsync(long idPregunta);
    Task<long> CrearAsync(PreguntaCrear peticion);
    Task<int> ReemplazarAsync(long idPregunta, PreguntaReemplazo peticion);
    Task<int> ActualizarAsync(long idPregunta, PreguntaActualizar peticion);
    Task<int> EliminarAsync(long idPregunta);
}
