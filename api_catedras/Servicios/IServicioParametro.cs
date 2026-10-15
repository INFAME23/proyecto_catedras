// ==========================================================================
//  Servicios/IServicioParametro.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'Parametro'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Parametro'.</summary>
public interface IServicioParametro
{
    /// <summary>Lista parámetros. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Parametro> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el parámetro por clave. No existe -> NoEncontradoExcepcion.</summary>
    Task<Parametro> ObtenerAsync(string clave);

    /// <summary>Crea un parámetro a partir de la petición del POST.</summary>
    Task CrearAsync(ParametroCrear peticion);

    /// <summary>Reemplaza por completo el parámetro (PUT).</summary>
    Task<int> ReemplazarAsync(string clave, ParametroReemplazo peticion);

    /// <summary>Actualiza parcialmente el parámetro (PATCH).</summary>
    Task<int> ActualizarAsync(string clave, ParametroActualizar peticion);

    /// <summary>Elimina el parámetro.</summary>
    Task<int> EliminarAsync(string clave);
}
