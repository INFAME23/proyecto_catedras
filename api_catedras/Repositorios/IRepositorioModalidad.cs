// ==========================================================================
//  Repositorios/IRepositorioModalidad.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.modalidad'. Mismo patrón que
//  IRepositorioSede: el servicio depende de esta interfaz, no del motor.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.modalidad'.</summary>
public interface IRepositorioModalidad
{
    /// <summary>Devuelve las modalidades ordenadas por código, hasta 'limite' filas.</summary>
    Task<List<Modalidad>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve la modalidad con ese código, o null si no existe.</summary>
    Task<Modalidad?> ObtenerPorIdAsync(string idModalidad);

    /// <summary>Inserta una modalidad nueva. Código duplicado -> NpgsqlException.</summary>
    Task CrearAsync(Modalidad modalidad);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para la modalidad con ese
    /// código. Sirve para PUT y PATCH. Devuelve filas afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idModalidad, Dictionary<string, object?> datos);

    /// <summary>Borra la modalidad con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idModalidad);
}
