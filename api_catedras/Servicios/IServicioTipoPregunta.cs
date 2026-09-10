// ==========================================================================
//  Servicios/IServicioTipoPregunta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'TipoPregunta'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'TipoPregunta'.</summary>
public interface IServicioTipoPregunta
{
    /// <summary>Lista tipos de pregunta. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<TipoPregunta> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el tipo de pregunta por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<TipoPregunta> ObtenerAsync(string idTipoPregunta);

    /// <summary>Crea un tipo de pregunta. Valida que EXACTAMENTE una de
    /// guarda_numero/guarda_texto/guarda_opcion sea true -> ArgumentException si no.</summary>
    Task CrearAsync(TipoPreguntaCrear peticion);

    /// <summary>Reemplaza por completo el tipo de pregunta (PUT). Misma validación.</summary>
    Task<int> ReemplazarAsync(string idTipoPregunta, TipoPreguntaReemplazo peticion);

    /// <summary>Actualiza parcialmente el tipo de pregunta (PATCH). Valida
    /// la coherencia solo si los TRES campos llegan juntos.</summary>
    Task<int> ActualizarAsync(string idTipoPregunta, TipoPreguntaActualizar peticion);

    /// <summary>Elimina el tipo de pregunta.</summary>
    Task<int> EliminarAsync(string idTipoPregunta);
}
