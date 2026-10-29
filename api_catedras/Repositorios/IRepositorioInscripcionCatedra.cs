// ==========================================================================
//  Repositorios/IRepositorioInscripcionCatedra.cs  ·  Cátedras · Subversión v7.1 · D50
// --------------------------------------------------------------------------
//  Acceso a 'catedras.inscripcion_catedra' (migración 049) por procedimientos.
//  Las reglas (cátedra activa, no duplicar, reactivar) las decide la BD y
//  vuelven como ESTADO de texto, no como excepción.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

public interface IRepositorioInscripcionCatedra
{
    /// <summary>CALL sp_listar_inscripcion_catedra. Filtros opcionales; por defecto solo ACTIVAS.</summary>
    Task<List<InscripcionCatedra>> ObtenerTodosAsync(long? fkAsistente, long? fkCatedra, bool soloActivas, int limite);

    /// <summary>CALL sp_inscribir_catedra. Estado: INSCRITA, YA_INSCRITA,
    /// CATEDRA_NO_DISPONIBLE o PERSONA_NO_DISPONIBLE.</summary>
    Task<string> InscribirAsync(long fkCatedra, long fkAsistente);

    /// <summary>CALL sp_cancelar_inscripcion_catedra. Estado: CANCELADA o NO_INSCRITA.</summary>
    Task<string> CancelarAsync(long fkCatedra, long fkAsistente);
}
