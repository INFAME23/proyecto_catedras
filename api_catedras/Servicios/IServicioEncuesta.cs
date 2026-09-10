// ==========================================================================
//  Servicios/IServicioEncuesta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'Encuesta'.
//
//  'CrearAsync' devuelve el 'id_encuesta' generado por la BD (long), a
//  diferencia de 'ServicioSede.CrearAsync' (que no devuelve nada): el
//  controlador lo necesita para incluirlo en la respuesta del POST
//  (3_plan.md §4.7).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Encuesta'.</summary>
public interface IServicioEncuesta
{
    /// <summary>Lista encuestas. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Encuesta> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve la encuesta por id. No existe -> NoEncontradoExcepcion.</summary>
    Task<Encuesta> ObtenerAsync(long idEncuesta);

    /// <summary>Crea una encuesta y devuelve el id generado por la BD. Valida
    /// vigente_hasta &gt;= vigente_desde -> ArgumentException si se viola.</summary>
    Task<long> CrearAsync(EncuestaCrear peticion);

    /// <summary>Reemplaza por completo la encuesta (PUT). Misma validación de fechas.</summary>
    Task<int> ReemplazarAsync(long idEncuesta, EncuestaReemplazo peticion);

    /// <summary>Actualiza parcialmente la encuesta (PATCH). Valida fechas
    /// solo si las dos llegan juntas en la misma petición.</summary>
    Task<int> ActualizarAsync(long idEncuesta, EncuestaActualizar peticion);

    /// <summary>Elimina la encuesta.</summary>
    Task<int> EliminarAsync(long idEncuesta);
}
