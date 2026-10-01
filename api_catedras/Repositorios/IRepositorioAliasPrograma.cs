// ==========================================================================
//  Repositorios/IRepositorioAliasPrograma.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.alias_programa'.
//
//  ⚠ A PROPÓSITO NO HAY 'ActualizarAsync' (3_plan.md §4.4, ADR D12): la
//  tabla es de solo-anexo. Quien lea esta interfaz ve la regla de negocio
//  sin leer la spec: un alias se crea o se borra, nunca se corrige.
//
//  'CrearAsync' devuelve DOS valores que pone la BD en el mismo INSERT
//  (RETURNING id_alias, creado_en), como una TUPLA de C#.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre 'catedras.alias_programa' (solo-anexo).</summary>
public interface IRepositorioAliasPrograma
{
    /// <summary>Alias ordenados por id, hasta 'limite' filas.</summary>
    Task<List<AliasPrograma>> ObtenerTodosAsync(int limite);

    /// <summary>El alias con ese id, o null si no existe.</summary>
    Task<AliasPrograma?> ObtenerPorIdAsync(long idAlias);

    /// <summary>Inserta y devuelve el id y la fecha que generó la BD.
    /// Texto duplicado o programa inexistente -> NpgsqlException (500).</summary>
    Task<(long IdAlias, DateTime CreadoEn)> CrearAsync(AliasPrograma alias);

    /// <summary>Borra. Devuelve filas eliminadas (0 = no existe). Ninguna tabla
    /// referencia a 'alias_programa', así que nunca falla por FK.</summary>
    Task<int> EliminarAsync(long idAlias);
}
