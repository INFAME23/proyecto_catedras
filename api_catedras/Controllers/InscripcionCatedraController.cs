// ==========================================================================
//  Controllers/InscripcionCatedraController.cs  ·  Cátedras · Subversión v7.1 · D50
// --------------------------------------------------------------------------
//  Inscripción de una persona a una cátedra.
//
//    GET  /api/InscripcionCatedra?fkAsistente=&fkCatedra=&soloActivas=&limite=  -> 200 | 204 | 400
//    POST /api/InscripcionCatedra            { fk_catedra, fk_asistente }        -> 200 | 404 | 422
//    POST /api/InscripcionCatedra/cancelar   { fk_catedra, fk_asistente }        -> 200 | 404 | 422
//
//  Inscribir es idempotente: si ya estaba inscrita responde 200 con estado
//  "ya_inscrita". Esta API no autoriza (ADR D28); el front toma la persona de
//  la sesión y nunca de un campo del formulario.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/InscripcionCatedra")]
public class InscripcionCatedraController : ControllerBase
{
    private readonly IServicioInscripcionCatedra _servicio;

    public InscripcionCatedraController(IServicioInscripcionCatedra servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] long? fkAsistente, [FromQuery] long? fkCatedra,
        [FromQuery] bool? soloActivas, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkAsistente, fkCatedra, soloActivas, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "inscripcion_catedra",
                limite = limite ?? 1000,
                total,
                datos,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al listar las inscripciones.", ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Inscribir([FromBody] InscripcionCatedraAccion peticion)
    {
        try
        {
            bool nueva = await _servicio.InscribirAsync(peticion);
            return Ok(new
            {
                estado = nueva ? "inscrita" : "ya_inscrita",
                mensaje = nueva ? "Inscripción registrada." : "Ya estabas inscrito en esta cátedra.",
            });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "No se pudo inscribir.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al inscribir.", ex.Message);
        }
    }

    [HttpPost("cancelar")]
    public async Task<IActionResult> Cancelar([FromBody] InscripcionCatedraAccion peticion)
    {
        try
        {
            await _servicio.CancelarAsync(peticion);
            return Ok(new { estado = "cancelada", mensaje = "Inscripción cancelada." });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "No hay inscripción activa.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cancelar la inscripción.", ex.Message);
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
