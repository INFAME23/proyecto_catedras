// ==========================================================================
//  Repositorios/IRepositorioProgramaAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.programa_asistente'.
//  Mismo patrón de LLAVE COMPUESTA que VinculacionAsistente (ADR D16):
//  3 operaciones, con 'CambiarEstadoAsync' en lugar de 'CerrarAsync'.
//  Sin ObtenerPorIdAsync, ActualizarAsync ni EliminarAsync.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.programa_asistente'.</summary>
public interface IRepositorioProgramaAsistente
{
    /// <summary>Matrículas, hasta 'limite'. Con 'fkAsistente', solo las de ese asistente.</summary>
    Task<List<ProgramaAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite);

    /// <summary>Inserta una matrícula. Llave repetida o FK inexistente -> NpgsqlException (500).</summary>
    Task CrearAsync(ProgramaAsistente programa);

    /// <summary>Cambia el 'estado' de la fila con esa llave. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> CambiarEstadoAsync(long fkAsistente, string fkPrograma, string fkPeriodo, string estado);
}
