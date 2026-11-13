// ==========================================================================
//  Controllers/ExportacionController.cs  ·  Subversión v7.2 · RF-S13 / RF-S14
// --------------------------------------------------------------------------
//  Consultas de SOLO LECTURA para la exportación de fin de periodo (sin tabla
//  propia, como Disponibilidad y QrSesion). El front arma los CSV y el ZIP.
//
//    GET /api/Exportacion/catedras?periodo=2026-2
//        -> 200 { periodo, total, datos:[{id_catedra, titulo, activo, estudiantes, docentes, externos, otros}] }
//        -> 204 si el periodo no tiene cátedras | 400
//    GET /api/Exportacion/asistentes?periodo=2026-2&desde=&limite=100
//        -> 200 { periodo, total, siguiente, datos:[{id_catedra, titulo, asistentes:[...]}] }
//           'siguiente' es el 'desde' de la próxima página (null cuando ya no hay más)
//        -> 400
//
//  El front exige rol ADMIN en /admin/exportar; la API sigue abierta (D28).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/Exportacion")]
public class ExportacionController : ControllerBase
{
    private readonly IServicioExportacion _servicio;

    public ExportacionController(IServicioExportacion servicio)
    {
        _servicio = servicio;
    }

    [HttpGet("catedras")]
    public async Task<IActionResult> Catedras([FromQuery] string? periodo)
    {
        try
        {
            var datos = await _servicio.CatedrasAsync(periodo);

            if (datos.Count == 0)
                return NoContent();

            return Ok(new { periodo = periodo!.Trim(), total = datos.Count, datos });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar las cátedras del periodo.", ex.Message);
        }
    }

    [HttpGet("asistentes")]
    public async Task<IActionResult> Asistentes(
        [FromQuery] string? periodo, [FromQuery] string? desde, [FromQuery] int? limite)
    {
        try
        {
            var (datos, siguiente) = await _servicio.AsistentesAsync(periodo, desde, limite);

            // Aquí no hay 204: una página vacía es la respuesta normal al llegar al final.
            return Ok(new { periodo = periodo!.Trim(), total = datos.Count, siguiente, datos });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar los asistentes del periodo.", ex.Message);
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
