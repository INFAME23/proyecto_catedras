// ==========================================================================
//  Repositorios/IRepositorioAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.asistente'. Molde de 5
//  operaciones, con dos novedades:
//   · ObtenerTodosAsync recibe un texto de búsqueda 'q' y un desplazamiento
//     (paginación): el filtro corre en SQL, no en memoria (ADR D18).
//   · CrearAsync recibe el asistente Y sus 4 detalles iniciales, y devuelve
//     el id que generó la BD (Guía IA, Regla 1: un solo CALL al SP).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.asistente'.</summary>
public interface IRepositorioAsistente
{
    /// <summary>Asistentes ordenados por id, hasta 'limite' filas, saltando
    /// 'desplazamiento'. Si 'q' trae texto, solo los que lo contienen en
    /// nombres, apellidos o id_asis (sin distinguir mayúsculas).</summary>
    Task<List<Asistente>> ObtenerTodosAsync(string? q, int limite, int desplazamiento);

    /// <summary>El asistente con ese id, o null si no existe.</summary>
    Task<Asistente?> ObtenerPorIdAsync(long idAsistente);

    /// <summary>Inserta el asistente y sus detalles iniciales (0 o más de cada
    /// uno) en UNA sola transacción, con el SP 'sp_guardar_asistente'.
    /// Devuelve el id generado. Cualquier fallo -> NpgsqlException (500) y
    /// NO queda nada guardado.</summary>
    Task<long> CrearAsync(
        Asistente asistente,
        IReadOnlyList<DocumentoAsistente> documentos,
        IReadOnlyList<VinculacionAsistente> vinculaciones,
        IReadOnlyList<ProgramaAsistente> programas,
        ConsentimientoDatos? consentimiento);

    /// <summary>Actualiza las columnas de 'datos'. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idAsistente, Dictionary<string, object?> datos);

    /// <summary>Borra. Devuelve filas eliminadas (0 = no existe). Si el asistente
    /// tiene 'usuario', 'ponencia' o 'registro_asistencia' (RESTRICT) ->
    /// NpgsqlException (500) y no se borra nada.</summary>
    Task<int> EliminarAsync(long idAsistente);
}
