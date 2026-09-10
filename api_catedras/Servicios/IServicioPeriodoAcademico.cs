// ==========================================================================
//  Servicios/IServicioPeriodoAcademico.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'PeriodoAcademico'. Mismo patrón que
//  IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'PeriodoAcademico'.</summary>
public interface IServicioPeriodoAcademico
{
    /// <summary>Lista periodos. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<PeriodoAcademico> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el periodo por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<PeriodoAcademico> ObtenerAsync(string idPeriodo);

    /// <summary>Crea un periodo. Valida fecha_fin &gt; fecha_ini -> ArgumentException si se viola.</summary>
    Task CrearAsync(PeriodoAcademicoCrear peticion);

    /// <summary>Reemplaza por completo el periodo (PUT). Misma validación de fechas.</summary>
    Task<int> ReemplazarAsync(string idPeriodo, PeriodoAcademicoReemplazo peticion);

    /// <summary>Actualiza parcialmente el periodo (PATCH). Valida fechas
    /// solo si las dos llegan juntas en la misma petición.</summary>
    Task<int> ActualizarAsync(string idPeriodo, PeriodoAcademicoActualizar peticion);

    /// <summary>Elimina el periodo.</summary>
    Task<int> EliminarAsync(string idPeriodo);
}
