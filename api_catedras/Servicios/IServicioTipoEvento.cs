// ==========================================================================
//  Servicios/IServicioTipoEvento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'TipoEvento'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'TipoEvento'.</summary>
public interface IServicioTipoEvento
{
    /// <summary>Lista tipos de evento. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<TipoEvento> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el tipo de evento por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<TipoEvento> ObtenerAsync(string idTipoEvento);

    /// <summary>Crea un tipo de evento a partir de la petición del POST.</summary>
    Task CrearAsync(TipoEventoCrear peticion);

    /// <summary>Reemplaza por completo el tipo de evento (PUT).</summary>
    Task<int> ReemplazarAsync(string idTipoEvento, TipoEventoReemplazo peticion);

    /// <summary>Actualiza parcialmente el tipo de evento (PATCH).</summary>
    Task<int> ActualizarAsync(string idTipoEvento, TipoEventoActualizar peticion);

    /// <summary>Elimina el tipo de evento.</summary>
    Task<int> EliminarAsync(string idTipoEvento);
}
