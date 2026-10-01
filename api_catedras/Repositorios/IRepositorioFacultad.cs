// ==========================================================================
//  Repositorios/IRepositorioFacultad.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  CONTRATO de acceso a datos de 'catedras.facultad'. El servicio depende
//  de esta interfaz y NO sabe qué motor hay detrás (Artículo 3). Por eso
//  'pruebas/' puede darle un repositorio FALSO en memoria.
//
//  Mismo molde de 5 operaciones que IRepositorioSede (v1).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.facultad'.</summary>
public interface IRepositorioFacultad
{
    /// <summary>Devuelve las facultades ordenadas por código, hasta 'limite' filas.</summary>
    Task<List<Facultad>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve la facultad con ese código, o null si no existe.</summary>
    Task<Facultad?> ObtenerPorIdAsync(string idFacultad);

    /// <summary>Inserta una facultad. Código/nombre duplicado o padre inexistente
    /// -> el motor lanza NpgsqlException (la API responde 500).</summary>
    Task CrearAsync(Facultad facultad);

    /// <summary>Actualiza las columnas de 'datos' (columna -> valor). Sirve para
    /// PUT y PATCH. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(string idFacultad, Dictionary<string, object?> datos);

    /// <summary>Borra la facultad. Devuelve filas eliminadas (0 = no existe).
    /// Hijas y programas que la usaban quedan con su FK en null (SET NULL).</summary>
    Task<int> EliminarAsync(string idFacultad);
}
