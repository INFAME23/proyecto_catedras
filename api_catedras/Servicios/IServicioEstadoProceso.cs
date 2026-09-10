// ==========================================================================
//  Servicios/IServicioEstadoProceso.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'EstadoProceso'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'EstadoProceso'.</summary>
public interface IServicioEstadoProceso
{
    /// <summary>Lista estados. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<EstadoProceso> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el estado por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<EstadoProceso> ObtenerAsync(string idEstado);

    /// <summary>Crea un estado a partir de la petición del POST. Valida la
    /// coherencia es_exitoso/es_final -> ArgumentException si se viola.</summary>
    Task CrearAsync(EstadoProcesoCrear peticion);

    /// <summary>Reemplaza por completo el estado (PUT). Misma validación de coherencia.</summary>
    Task<int> ReemplazarAsync(string idEstado, EstadoProcesoReemplazo peticion);

    /// <summary>Actualiza parcialmente el estado (PATCH). Valida coherencia
    /// solo si ambos campos llegan juntos en la misma petición.</summary>
    Task<int> ActualizarAsync(string idEstado, EstadoProcesoActualizar peticion);

    /// <summary>Elimina el estado.</summary>
    Task<int> EliminarAsync(string idEstado);
}
