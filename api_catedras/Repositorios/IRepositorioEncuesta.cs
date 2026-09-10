// ==========================================================================
//  Repositorios/IRepositorioEncuesta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.encuesta'.
//
//  Diferencia clave con IRepositorioSede (3_plan.md §4.7):
//   · El identificador es 'long' (BIGINT), no 'string': lo genera la BD.
//   · 'CrearAsync' DEVUELVE el id generado (Task<long>), no un Task vacío
//     — el repositorio inserta con 'RETURNING id_encuesta'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.encuesta'.</summary>
public interface IRepositorioEncuesta
{
    /// <summary>Devuelve las encuestas ordenadas por id, hasta 'limite' filas.</summary>
    Task<List<Encuesta>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve la encuesta con ese id, o null si no existe.</summary>
    Task<Encuesta?> ObtenerPorIdAsync(long idEncuesta);

    /// <summary>Inserta una encuesta nueva y devuelve el 'id_encuesta' que
    /// generó la BD. (nombre, version) duplicado -> NpgsqlException.</summary>
    Task<long> CrearAsync(Encuesta encuesta);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para la encuesta con ese
    /// id. Sirve para PUT y PATCH. Devuelve filas afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(long idEncuesta, Dictionary<string, object?> datos);

    /// <summary>Borra la encuesta con ese id. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(long idEncuesta);
}
