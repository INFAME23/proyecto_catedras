// ==========================================================================
//  Repositorios/IRepositorioDependencia.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  CONTRATO de acceso a datos de 'catedras.dependencia'. El servicio depende
//  de esta interfaz y NO sabe qué motor hay detrás (Artículo 3). Por eso
//  'pruebas/' puede darle un repositorio FALSO en memoria.
//
//  Mismo molde de 5 operaciones que IRepositorioSede (v1).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.dependencia'.</summary>
public interface IRepositorioDependencia
{
    /// <summary>Devuelve las dependencias ordenadas por código, hasta 'limite' filas.</summary>
    Task<List<Dependencia>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve la dependencia con ese código, o null si no existe.</summary>
    Task<Dependencia?> ObtenerPorIdAsync(string idDependencia);

    /// <summary>Inserta una dependencia. Código/nombre duplicado o padre inexistente
    /// -> el motor lanza NpgsqlException (la API responde 500).</summary>
    Task CrearAsync(Dependencia dependencia);

    /// <summary>Actualiza las columnas de 'datos' (columna -> valor). Sirve para
    /// PUT y PATCH. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(string idDependencia, Dictionary<string, object?> datos);

    /// <summary>Borra la dependencia. Devuelve filas eliminadas (0 = no existe).
    /// Hijas y cátedras que la usaban quedan con su FK en null (SET NULL).</summary>
    Task<int> EliminarAsync(string idDependencia);
}
