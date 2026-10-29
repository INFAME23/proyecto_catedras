// ==========================================================================
//  Repositorios/IRepositorioDisponibilidad.cs  ·  Cátedras · Subversión v7.1 · D52
// --------------------------------------------------------------------------
//  Consultas de disponibilidad de horario (migración 050). Solo lectura.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

public interface IRepositorioDisponibilidad
{
    /// <summary>CALL sp_sesiones_en_conflicto: sesiones que se traslapan con [inicio, fin)
    /// y chocan por LUGAR o PONENTE (ver 050_disponibilidad_sesiones.sql).</summary>
    Task<List<SesionOcupada>> ConflictosAsync(
        DateTimeOffset inicio, DateTimeOffset fin, string? fkSede, string? lugar,
        long? fkAsistente, long? excluirSesion);

    /// <summary>CALL sp_agenda_sesiones: sesiones que tocan [desde, hasta), de una sede o de todas.</summary>
    Task<List<SesionOcupada>> AgendaAsync(DateTimeOffset desde, DateTimeOffset hasta, string? fkSede, int limite);
}
