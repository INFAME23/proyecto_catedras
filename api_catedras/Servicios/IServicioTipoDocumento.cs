// ==========================================================================
//  Servicios/IServicioTipoDocumento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'TipoDocumento'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'TipoDocumento'.</summary>
public interface IServicioTipoDocumento
{
    /// <summary>Lista tipos de documento. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<TipoDocumento> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el tipo de documento por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<TipoDocumento> ObtenerAsync(string idTipoDocumento);

    /// <summary>Crea un tipo de documento a partir de la petición del POST.</summary>
    Task CrearAsync(TipoDocumentoCrear peticion);

    /// <summary>Reemplaza por completo el tipo de documento (PUT).</summary>
    Task<int> ReemplazarAsync(string idTipoDocumento, TipoDocumentoReemplazo peticion);

    /// <summary>Actualiza parcialmente el tipo de documento (PATCH).</summary>
    Task<int> ActualizarAsync(string idTipoDocumento, TipoDocumentoActualizar peticion);

    /// <summary>Elimina el tipo de documento.</summary>
    Task<int> EliminarAsync(string idTipoDocumento);
}
