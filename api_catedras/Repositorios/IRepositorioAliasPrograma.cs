// ==========================================================================
//  Repositorios/IRepositorioAliasPrograma.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.alias_programa'.
//
//  RETROFIT criterio 3: la v2 (ADR D12) trataba la tabla como solo-anexo (sin
//  'ActualizarAsync'). Ahora se agrega 'ActualizarAsync' para que
//  'sp_actualizar_alias_programa' tenga llamador y el alias pueda corregirse
//  por PUT/PATCH en vez de borrar y volver a crear.
//
//  'CrearAsync' devuelve DOS valores que pone la BD en el mismo INSERT
//  (RETURNING id_alias, creado_en), como una TUPLA de C#.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre 'catedras.alias_programa'.</summary>
public interface IRepositorioAliasPrograma
{
    /// <summary>Alias ordenados por id, hasta 'limite' filas.</summary>
    Task<List<AliasPrograma>> ObtenerTodosAsync(int limite);

    /// <summary>El alias con ese id, o null si no existe.</summary>
    Task<AliasPrograma?> ObtenerPorIdAsync(long idAlias);

    /// <summary>Inserta y devuelve el id y la fecha que generó la BD.
    /// Texto duplicado o programa inexistente -> NpgsqlException (500).</summary>
    Task<(long IdAlias, DateTime CreadoEn)> CrearAsync(AliasPrograma alias);

    /// <summary>Actualiza las columnas de 'datos' (clave = nombre de columna). Devuelve filas
    /// afectadas (0 = no existe). Texto duplicado o programa inexistente -> NpgsqlException (500).</summary>
    Task<int> ActualizarAsync(long idAlias, Dictionary<string, object?> datos);

    /// <summary>Borra. Devuelve filas eliminadas (0 = no existe). Ninguna tabla
    /// referencia a 'alias_programa', así que nunca falla por FK.</summary>
    Task<int> EliminarAsync(long idAlias);
}
