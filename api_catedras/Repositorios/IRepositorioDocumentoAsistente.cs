// ==========================================================================
//  Repositorios/IRepositorioDocumentoAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.documento_asistente'. Molde
//  completo de 5 operaciones (a diferencia de AliasPrograma en v2). La
//  lista acepta un filtro opcional por asistente.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.documento_asistente'.</summary>
public interface IRepositorioDocumentoAsistente
{
    /// <summary>Documentos ordenados por id, hasta 'limite'. Con 'fkAsistente',
    /// solo los de ese asistente.</summary>
    Task<List<DocumentoAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite);

    /// <summary>El documento con ese id, o null si no existe.</summary>
    Task<DocumentoAsistente?> ObtenerPorIdAsync(long idDocumento);

    /// <summary>Inserta y devuelve lo que generó la BD (id y fecha de registro).
    /// Segundo vigente o número repetido -> NpgsqlException (500).</summary>
    Task<(long IdDocumento, DateTime RegistradoEn)> CrearAsync(DocumentoAsistente documento);

    /// <summary>Actualiza las columnas de 'datos'. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idDocumento, Dictionary<string, object?> datos);

    /// <summary>Borra. Devuelve filas eliminadas (0 = no existe).</summary>
    Task<int> EliminarAsync(long idDocumento);
}
