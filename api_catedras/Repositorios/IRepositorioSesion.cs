// ==========================================================================
//  Repositorios/IRepositorioSesion.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.sesion'. Molde completo (como
//  Catedra en la v2), con dos detalles propios:
//   · 'ObtenerTodosAsync' acepta 'fkCatedra': con 5714 sesiones de ejemplo,
//     el calendario de UNA cátedra se filtra en SQL, no en el front.
//   · 'CrearAsync' devuelve una TUPLA con los TRES valores que decide la
//     BD en el mismo INSERT: el id (IDENTITY), el número de reunión (lo
//     calcula el trigger si llegó 0, ADR D20) y la fecha de registro.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.sesion'.</summary>
public interface IRepositorioSesion
{
    /// <summary>Sesiones ordenadas por cátedra y número de reunión, hasta 'limite'.
    /// Con 'fkCatedra', solo las de esa cátedra.</summary>
    Task<List<Sesion>> ObtenerTodosAsync(long? fkCatedra, int limite);

    /// <summary>La sesión con ese id, o null si no existe.</summary>
    Task<Sesion?> ObtenerPorIdAsync(long idSesion);

    /// <summary>Inserta y devuelve (id_sesion, numero_reunion, creado_en) tal como
    /// los dejó la BD. Si 'enlaceInicial' no es null, emite además el primer
    /// enlace de registro en la MISMA transacción (Guía IA, Regla 1:
    /// 'sp_guardar_sesion'). FK inexistente o número repetido -> NpgsqlException (500).</summary>
    Task<(long IdSesion, short NumeroReunion, DateTime CreadoEn)> CrearAsync(
        Sesion sesion, (int? MinutosAntes, int? MinutosDespues, long? FkUsuario)? enlaceInicial);

    /// <summary>Actualiza las columnas de 'datos'. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idSesion, Dictionary<string, object?> datos);

    /// <summary>Borra. ⚠ 'ponencia' y 'enlace_registro' se borran en CASCADA;
    /// 'registro_asistencia' y 'lote_migracion' lo BLOQUEAN (RESTRICT -> 500).</summary>
    Task<int> EliminarAsync(long idSesion);
}
