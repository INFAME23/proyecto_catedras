// ==========================================================================
//  Repositorios/IRepositorioRol.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.rol'. Mismo patrón que IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.rol'.</summary>
public interface IRepositorioRol
{
    /// <summary>Devuelve los roles ordenados por código, hasta 'limite' filas.</summary>
    Task<List<Rol>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el rol con ese código, o null si no existe.</summary>
    Task<Rol?> ObtenerPorIdAsync(string idRol);

    /// <summary>Inserta un rol nuevo. Código o nombre duplicado -> NpgsqlException.</summary>
    Task CrearAsync(Rol rol);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el rol con ese
    /// código. Sirve para PUT y PATCH. Devuelve filas afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idRol, Dictionary<string, object?> datos);

    /// <summary>Borra el rol con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idRol);
}
