// ==========================================================================
//  Repositorios/IRepositorioParametro.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.parametro'. Mismo patrón que
//  IRepositorioSede: el servicio depende de esta interfaz y no sabe qué
//  motor hay detrás (Artículo 3).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.parametro'.</summary>
public interface IRepositorioParametro
{
    /// <summary>Devuelve los parámetros ordenados por clave, hasta 'limite' filas.</summary>
    Task<List<Parametro>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el parámetro con esa clave, o null si no existe.</summary>
    Task<Parametro?> ObtenerPorIdAsync(string clave);

    /// <summary>Inserta un parámetro nuevo. Si la clave ya existe, el motor
    /// lanza NpgsqlException (la API lo reporta como 500).</summary>
    Task CrearAsync(Parametro parametro);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' (columna -> valor nuevo)
    /// para el parámetro con esa clave. Sirve para PUT y para PATCH.
    /// Devuelve las filas afectadas (0 = no existe ese parámetro).
    /// </summary>
    Task<int> ActualizarAsync(string clave, Dictionary<string, object?> datos);

    /// <summary>Borra el parámetro con esa clave (físico: la tabla no tiene
    /// 'activo'). Devuelve filas eliminadas (0 = no existe).</summary>
    Task<int> EliminarAsync(string clave);
}
