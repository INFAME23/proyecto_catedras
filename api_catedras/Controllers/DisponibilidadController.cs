// ==========================================================================
//  Controllers/DisponibilidadController.cs  ·  Cátedras · Subversión v7.1 · D52
// --------------------------------------------------------------------------
//  Consultas de disponibilidad de horario (solo lectura; no es una tabla).
//
//    GET /api/Disponibilidad/conflictos?inicio=&fin=&fkSede=&lugar=&fkAsistente=&excluirSesion=
//        -> 200 { disponible, total, conflictos } | 400
//    GET /api/Disponibilidad/agenda?desde=&hasta=&fkSede=&limite=
//        -> 200 { total, datos } | 400
//
//  Las fechas llevan su zona (p. ej. 2026-10-20T14:00:00-05:00; el '+' de una
//  zona positiva debe ir como %2B en la URL). "Disponible" = ninguna sesión
//  choca por lugar ni por ponente.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/Disponibilidad")]
public class DisponibilidadController : ControllerBase
{
    private readonly IServicioDisponibilidad _servicio;

    public DisponibilidadController(IServicioDisponibilidad servicio)
    {
        _servicio = servicio;
    }

    [HttpGet("conflictos")]
    public async Task<IActionResult> Conflictos(
        [FromQuery] DateTimeOffset? inicio, [FromQuery] DateTimeOffset? fin,
        [FromQuery] string? fkSede, [FromQuery] string? lugar,
        [FromQuery] long? fkAsistente, [FromQuery] long? excluirSesion)
    {
        try
        {
            var conflictos = await _servicio.ConflictosAsync(inicio, fin, fkSede, lugar, fkAsistente, excluirSesion);
            return Ok(new
            {
                disponible = conflictos.Count == 0,
                total = conflictos.Count,
                conflictos,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar la disponibilidad.", ex.Message);
        }
    }

    [HttpGet("agenda")]
    public async Task<IActionResult> Agenda(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta,
        [FromQuery] string? fkSede, [FromQuery] int? limite)
    {
        try
        {
            var datos = await _servicio.AgendaAsync(desde, hasta, fkSede, limite);
            return Ok(new { total = datos.Count, datos });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar la agenda.", ex.Message);
        }
    }

    private ObjectResult Problema(int codigo, string estado, string mensaje, string? detalle)
    {
        return new ObjectResult(new { estado, mensaje, detalle })
        {
            StatusCode = codigo,
        };
    }
}
