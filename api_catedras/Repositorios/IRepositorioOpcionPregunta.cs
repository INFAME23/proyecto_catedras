// ==========================================================================
//  Repositorios/IRepositorioOpcionPregunta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.opcion_pregunta'. Molde completo
//  de 5 operaciones (RF167-RF171, D33) — a diferencia de 'AliasPrograma'
//  (v2), esta tabla SÍ tiene Reemplazar y Actualizar: corregir una
//  etiqueta o reordenar una opción es una edición legítima.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.opcion_pregunta'.</summary>
public interface IRepositorioOpcionPregunta
{
    /// <summary>Opciones, filtradas por pregunta si se da, hasta 'limite' filas.</summary>
    Task<List<OpcionPregunta>> ObtenerTodosAsync(long? fkPregunta, int limite);

    Task<OpcionPregunta?> ObtenerPorIdAsync(long idOpcion);

    /// <summary>Inserta la opción. Devuelve el id generado. '(fk_pregunta, orden)'
    /// duplicado o pregunta inexistente -> NpgsqlException (500).</summary>
    Task<long> CrearAsync(OpcionPregunta opcion);

    Task<int> ActualizarAsync(long idOpcion, Dictionary<string, object?> datos);

    /// <summary>Borra. Si la opción ya fue elegida en alguna 'respuesta_item' (FK
    /// RESTRICT) -> NpgsqlException (500).</summary>
    Task<int> EliminarAsync(long idOpcion);
}
