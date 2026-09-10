// ==========================================================================
//  Repositorios/IRepositorioTipoPregunta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.tipo_pregunta'. Mismo patrón que
//  IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.tipo_pregunta'.</summary>
public interface IRepositorioTipoPregunta
{
    /// <summary>Devuelve los tipos de pregunta ordenados por código, hasta 'limite' filas.</summary>
    Task<List<TipoPregunta>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el tipo de pregunta con ese código, o null si no existe.</summary>
    Task<TipoPregunta?> ObtenerPorIdAsync(string idTipoPregunta);

    /// <summary>Inserta un tipo de pregunta nuevo. Código duplicado -> NpgsqlException.</summary>
    Task CrearAsync(TipoPregunta tipoPregunta);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el tipo de
    /// pregunta con ese código. Sirve para PUT y PATCH. Devuelve filas
    /// afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idTipoPregunta, Dictionary<string, object?> datos);

    /// <summary>Borra el tipo de pregunta con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idTipoPregunta);
}
