// ==========================================================================
//  Repositorios/IRepositorioRestriccionProgramacion.cs  ·  Cátedras · v7.1 · D57
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.restriccion_programacion' (las ventanas
//  en que se pueden programar sesiones) y de la consulta «¿cabe este horario?».
//  El rechazo de una sesión fuera de ventana NO pasa por aquí: lo hace el
//  disparador de la base de datos sobre 'sesion'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre 'catedras.restriccion_programacion'.</summary>
public interface IRepositorioRestriccionProgramacion
{
    Task<List<RestriccionProgramacion>> ObtenerTodosAsync(bool soloActivas, int limite);

    /// <summary>La ventana con ese id, o null si no existe.</summary>
    Task<RestriccionProgramacion?> ObtenerUnoAsync(long idRestriccion);

    /// <summary>Inserta y devuelve la fila. 'horaDesde'/'horaHasta' como 'HH:mm'; 'dias' separados por coma ('1,2,3') o null.</summary>
    Task<RestriccionProgramacion> CrearAsync(string nombre, DateOnly fechaDesde, DateOnly fechaHasta,
                                             string? horaDesde, string? horaHasta, string? dias, bool activo);

    /// <summary>Reemplaza todos los campos. null = no existe.</summary>
    Task<RestriccionProgramacion?> ReemplazarAsync(long idRestriccion, string nombre, DateOnly fechaDesde, DateOnly fechaHasta,
                                                   string? horaDesde, string? horaHasta, string? dias, bool activo);

    /// <summary>Borrado físico. Devuelve filas eliminadas (0 = no existe).</summary>
    Task<int> EliminarAsync(long idRestriccion);

    /// <summary>¿Cabe el horario en alguna ventana activa?</summary>
    Task<ValidacionHorario> ValidarAsync(DateTimeOffset inicio, DateTimeOffset fin);
}
