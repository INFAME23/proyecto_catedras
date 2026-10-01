// ==========================================================================
//  Repositorios/IRepositorioProgramaAcademico.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.programa_academico'. Mismo molde
//  de 5 operaciones que la v1; la clave es el 'codigo' (texto).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.programa_academico'.</summary>
public interface IRepositorioProgramaAcademico
{
    /// <summary>Programas ordenados por código, hasta 'limite' filas.</summary>
    Task<List<ProgramaAcademico>> ObtenerTodosAsync(int limite);

    /// <summary>El programa con ese código, o null si no existe.</summary>
    Task<ProgramaAcademico?> ObtenerPorIdAsync(string codigo);

    /// <summary>Inserta. Duplicado o facultad inexistente -> NpgsqlException (500).</summary>
    Task CrearAsync(ProgramaAcademico programa);

    /// <summary>Actualiza las columnas de 'datos'. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(string codigo, Dictionary<string, object?> datos);

    /// <summary>Borra. Devuelve filas eliminadas (0 = no existe). Si el programa
    /// tiene filas en 'programa_asistente' (FK RESTRICT) -> NpgsqlException (500).</summary>
    Task<int> EliminarAsync(string codigo);
}
