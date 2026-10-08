// ==========================================================================
//  Repositorios/IRepositorioVinculacionAsistente.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.vinculacion_asistente'.
//
//  LLAVE COMPUESTA (ADR D16): SOLO 3 operaciones. No hay ObtenerPorIdAsync
//  (no existe una columna 'id'), ni ActualizarAsync, ni EliminarAsync: la
//  historia de vinculaciones se abre y se cierra, nunca se reescribe ni se
//  borra. Que esos métodos NO existan aquí es parte de la especificación.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.vinculacion_asistente'.</summary>
public interface IRepositorioVinculacionAsistente
{
    /// <summary>Vinculaciones, hasta 'limite'. Con 'fkAsistente', solo las de ese asistente.</summary>
    Task<List<VinculacionAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite);

    /// <summary>Inserta (abre) una vinculación. Llave repetida o FK inexistente
    /// -> NpgsqlException (500).</summary>
    Task CrearAsync(VinculacionAsistente vinculacion);

    /// <summary>Pone 'fecha_fin' a la fila con esa llave, solo si seguía abierta.
    /// Devuelve filas afectadas (0 = no existe o ya estaba cerrada).</summary>
    Task<int> CerrarAsync(long fkAsistente, string fkTipoVinculacion,
                          DateOnly fechaIni, DateOnly fechaFin);
}
