// ==========================================================================
//  Repositorios/IRepositorioTipoDocumento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de datos sobre 'catedras.tipo_documento'. Mismo patrón que
//  IRepositorioSede.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.tipo_documento'.</summary>
public interface IRepositorioTipoDocumento
{
    /// <summary>Devuelve los tipos de documento ordenados por código, hasta 'limite' filas.</summary>
    Task<List<TipoDocumento>> ObtenerTodosAsync(int limite);

    /// <summary>Devuelve el tipo de documento con ese código, o null si no existe.</summary>
    Task<TipoDocumento?> ObtenerPorIdAsync(string idTipoDocumento);

    /// <summary>Inserta un tipo de documento nuevo. Código o nombre duplicado -> NpgsqlException.</summary>
    Task CrearAsync(TipoDocumento tipoDocumento);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' para el tipo de
    /// documento con ese código. Sirve para PUT y PATCH. Devuelve filas
    /// afectadas (0 = no existe).
    /// </summary>
    Task<int> ActualizarAsync(string idTipoDocumento, Dictionary<string, object?> datos);

    /// <summary>Borra el tipo de documento con ese código. Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(string idTipoDocumento);
}
