// ==========================================================================
//  Repositorios/IRepositorioPregunta.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.pregunta'. Molde completo de 5
//  operaciones, igual que la v1; la clave es 'id_pregunta' (bigint).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.pregunta'.</summary>
public interface IRepositorioPregunta
{
    /// <summary>Preguntas, filtradas por encuesta si se da, hasta 'limite' filas.</summary>
    Task<List<Pregunta>> ObtenerTodosAsync(long? fkEncuesta, int limite);

    /// <summary>La pregunta con ese id, o null si no existe.</summary>
    Task<Pregunta?> ObtenerPorIdAsync(long idPregunta);

    /// <summary>Inserta la pregunta y sus opciones iniciales (0 o más) en UNA sola
    /// transacción, invocando el SP 'sp_guardar_pregunta' (Guía IA, Regla 1).
    /// Devuelve el id generado. '(fk_encuesta, orden)' duplicado, tipo de
    /// pregunta inexistente o una opción con 'orden' repetido ->
    /// NpgsqlException (500) y NO queda nada guardado.</summary>
    Task<long> CrearAsync(Pregunta pregunta, IReadOnlyList<OpcionPregunta> opcionesIniciales);

    /// <summary>Actualiza las columnas de 'datos'. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idPregunta, Dictionary<string, object?> datos);

    /// <summary>Borra. Devuelve filas eliminadas (0 = no existe). Si la pregunta tiene
    /// filas en 'respuesta_item' (FK RESTRICT) -> NpgsqlException (500), y la
    /// cascada hacia sus 'opcion_pregunta' no llega a ejecutarse.</summary>
    Task<int> EliminarAsync(long idPregunta);
}
