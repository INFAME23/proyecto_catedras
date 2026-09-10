// ==========================================================================
//  Servicios/IServicioTipoVinculacion.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'TipoVinculacion'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'TipoVinculacion'.</summary>
public interface IServicioTipoVinculacion
{
    /// <summary>Lista tipos de vinculación. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<TipoVinculacion> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el tipo de vinculación por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<TipoVinculacion> ObtenerAsync(string idTipoVinculacion);

    /// <summary>Crea un tipo de vinculación a partir de la petición del POST.</summary>
    Task CrearAsync(TipoVinculacionCrear peticion);

    /// <summary>Reemplaza por completo el tipo de vinculación (PUT).</summary>
    Task<int> ReemplazarAsync(string idTipoVinculacion, TipoVinculacionReemplazo peticion);

    /// <summary>Actualiza parcialmente el tipo de vinculación (PATCH).</summary>
    Task<int> ActualizarAsync(string idTipoVinculacion, TipoVinculacionActualizar peticion);

    /// <summary>Elimina el tipo de vinculación.</summary>
    Task<int> EliminarAsync(string idTipoVinculacion);
}
