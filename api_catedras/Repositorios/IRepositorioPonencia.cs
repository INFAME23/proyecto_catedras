// ==========================================================================
//  Repositorios/IRepositorioPonencia.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.ponencia'.
//
//  LLAVE COMPUESTA SIN VIGENCIA (ADR D19): 4 operaciones exactamente.
//   · NO hay 'ObtenerPorIdAsync(id)': no existe una columna 'id'; el GET
//     puntual recibe las TRES columnas de la llave.
//   · 'ActualizarAsync' (RETROFIT criterio 3) SOLO cambia el ROL: las 3
//     columnas son la llave, así que lo único editable es el rol (mover la
//     fila a otra sesión/asistente es eliminar + crear).
//   · SÍ hay 'EliminarAsync' (a diferencia de VinculacionAsistente, ADR
//     D16): aquí no hay historia que proteger, borrar solo corrige quién
//     quedó registrado.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.ponencia'.</summary>
public interface IRepositorioPonencia
{
    /// <summary>Participaciones, hasta 'limite'. Los dos filtros son opcionales y combinables.</summary>
    Task<List<Ponencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite);

    /// <summary>La participación con esa llave exacta, o null si no existe (RF128).</summary>
    Task<Ponencia?> ObtenerUnoAsync(long fkSesion, long fkAsistente, string rol);

    /// <summary>Inserta. Llave repetida o FK inexistente -> NpgsqlException (500).</summary>
    Task CrearAsync(Ponencia ponencia);

    /// <summary>Cambia el rol de la fila con esa llave exacta (sp_actualizar_ponencia).
    /// Devuelve filas afectadas (0 = no existe). Rol nuevo ya ocupado por el mismo
    /// asistente en esa sesión -> NpgsqlException (500).</summary>
    Task<int> ActualizarAsync(long fkSesion, long fkAsistente, string rol, string rolNuevo);

    /// <summary>Borra la fila con esa llave exacta. Devuelve filas eliminadas (0 = no existe).</summary>
    Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol);
}
