// ==========================================================================
//  Servicios/IServicioAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'Asistente'. Mismo molde que v1/v2:
//  ArgumentException -> 400 · NoEncontradoExcepcion -> 404.
//  La lista recibe además 'q' (búsqueda) y 'pagina' (ADR D18).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Asistente'.</summary>
public interface IServicioAsistente
{
    Task<(List<Asistente> Datos, int Total)> ListarAsync(string? q, int? limite, int? pagina);
    Task<Asistente> ObtenerAsync(long idAsistente);

    /// <summary>Crea el asistente y sus detalles iniciales. Devuelve el
    /// asistente con el id que generó la BD.</summary>
    Task<Asistente> CrearAsync(AsistenteCrear peticion);

    Task<int> ReemplazarAsync(long idAsistente, AsistenteReemplazo peticion);
    Task<int> ActualizarAsync(long idAsistente, AsistenteActualizar peticion);
    Task<int> EliminarAsync(long idAsistente);
}
