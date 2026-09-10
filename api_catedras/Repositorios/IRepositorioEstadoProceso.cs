// ==========================================================================
//  Repositorios/IRepositorioEstadoProceso.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.estado_proceso'. Mismo patrón que
//  IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.estado_proceso'.</summary>
public interface IRepositorioEstadoProceso
{
    /// <summary>Devuelve los estados ordenados por código, hasta 'limite' filas.</summary>
    Task<List<EstadoProceso>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el estado con ese código, o null si no existe.</summary>
    Task<EstadoProceso?> ObtenerPorIdAsync(string idEstado);

    /// <summary>Inserta un estado nuevo. Código duplicado -> NpgsqlException.</summary>
    Task CrearAsync(EstadoProceso estado);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el estado con ese
    /// código. Sirve para PUT y PATCH. Devuelve filas afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idEstado, Dictionary<string, object?> datos);

    /// <summary>Borra el estado con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idEstado);
}
