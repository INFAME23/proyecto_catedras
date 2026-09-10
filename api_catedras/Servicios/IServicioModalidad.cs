// ==========================================================================
//  Servicios/IServicioModalidad.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'Modalidad'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Modalidad'.</summary>
public interface IServicioModalidad
{
    /// <summary>Lista modalidades. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Modalidad> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve la modalidad por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<Modalidad> ObtenerAsync(string idModalidad);

    /// <summary>Crea una modalidad a partir de la petición del POST.</summary>
    Task CrearAsync(ModalidadCrear peticion);

    /// <summary>Reemplaza por completo la modalidad (PUT).</summary>
    Task<int> ReemplazarAsync(string idModalidad, ModalidadReemplazo peticion);

    /// <summary>Actualiza parcialmente la modalidad (PATCH).</summary>
    Task<int> ActualizarAsync(string idModalidad, ModalidadActualizar peticion);

    /// <summary>Elimina la modalidad.</summary>
    Task<int> EliminarAsync(string idModalidad);
}
