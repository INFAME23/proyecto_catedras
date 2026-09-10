// ==========================================================================
//  Repositorios/IRepositorioTipoEvento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.tipo_evento'. Mismo patrón que
//  IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.tipo_evento'.</summary>
public interface IRepositorioTipoEvento
{
    /// <summary>Devuelve los tipos de evento ordenados por código, hasta 'limite' filas.</summary>
    Task<List<TipoEvento>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el tipo de evento con ese código, o null si no existe.</summary>
    Task<TipoEvento?> ObtenerPorIdAsync(string idTipoEvento);

    /// <summary>Inserta un tipo de evento nuevo. Código duplicado -> NpgsqlException.</summary>
    Task CrearAsync(TipoEvento tipoEvento);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el tipo de evento
    /// con ese código. Sirve para PUT y PATCH. Devuelve filas afectadas
    /// (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idTipoEvento, Dictionary<string, object?> datos);

    /// <summary>Borra el tipo de evento con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idTipoEvento);
}
