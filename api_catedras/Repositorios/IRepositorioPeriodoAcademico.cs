// ==========================================================================
//  Repositorios/IRepositorioPeriodoAcademico.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.periodo_academico'. Mismo patrón que
//  IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.periodo_academico'.</summary>
public interface IRepositorioPeriodoAcademico
{
    /// <summary>Devuelve los periodos ordenados por código, hasta 'limite' filas.</summary>
    Task<List<PeriodoAcademico>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el periodo con ese código, o null si no existe.</summary>
    Task<PeriodoAcademico?> ObtenerPorIdAsync(string idPeriodo);

    /// <summary>Inserta un periodo nuevo. Código duplicado -> NpgsqlException.</summary>
    Task CrearAsync(PeriodoAcademico periodo);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el periodo con ese
    /// código. Sirve para PUT y PATCH. Devuelve filas afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idPeriodo, Dictionary<string, object?> datos);

    /// <summary>Borra el periodo con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idPeriodo);
}
