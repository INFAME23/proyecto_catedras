// ==========================================================================
//  Repositorios/IRepositorioTipoVinculacion.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.tipo_vinculacion'. Mismo patrón que
//  IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.tipo_vinculacion'.</summary>
public interface IRepositorioTipoVinculacion
{
    /// <summary>Devuelve los tipos de vinculación ordenados por código, hasta 'limite' filas.</summary>
    Task<List<TipoVinculacion>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el tipo de vinculación con ese código, o null si no existe.</summary>
    Task<TipoVinculacion?> ObtenerPorIdAsync(string idTipoVinculacion);

    /// <summary>Inserta un tipo de vinculación nuevo. Código o nombre duplicado -> NpgsqlException.</summary>
    Task CrearAsync(TipoVinculacion tipoVinculacion);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el tipo de
    /// vinculación con ese código. Sirve para PUT y PATCH. Devuelve filas
    /// afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idTipoVinculacion, Dictionary<string, object?> datos);

    /// <summary>Borra el tipo de vinculación con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idTipoVinculacion);
}
