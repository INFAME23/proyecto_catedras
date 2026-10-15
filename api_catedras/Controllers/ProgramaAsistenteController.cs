// ==========================================================================
//  Controllers/ProgramaAsistenteController.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Capa HTTP de 'ProgramaAsistente'. SOLO 3 endpoints (ADR D16):
//     GET   /api/ProgramaAsistente?fkAsistente=ID   lista
//     POST  /api/ProgramaAsistente                  matricula
//     PATCH /api/ProgramaAsistente/cambiar-estado   cambia el estado (llave en el body)
//
//  Sin [HttpGet("{id}")], [HttpPut] ni [HttpDelete]: llave compuesta, sin
//  columna 'id'. Esas rutas responden 404/405 del propio ASP.NET.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/ProgramaAsistente")]
public class ProgramaAsistenteController : ControllerBase
{
    private readonly IServicioProgramaAsistente _servicio;

    public ProgramaAsistenteController(IServicioProgramaAsistente servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF110 · GET /api/ProgramaAsistente?fkAsistente=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkAsistente, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkAsistente, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "programa_asistente",
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
            return Problema(500, "error_interno", "Error al listar las matrículas.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF110 · POST /api/ProgramaAsistente   -> 200 { estado, mensaje, programa } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ProgramaAsistenteCrear peticion)
    {
        try
        {
            var programa = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Matrícula en '{programa.FkPrograma}' ({programa.FkPeriodo}) creada.",
                programa,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Llave repetida (RF111) o programa/periodo/asistente inexistente (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la matrícula.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF110 · PATCH /api/ProgramaAsistente/cambiar-estado   -> 200 | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPatch("cambiar-estado")]
    public async Task<IActionResult> CambiarEstado([FromBody] ProgramaAsistenteCambiarEstado peticion)
    {
        try
        {
            var programa = await _servicio.CambiarEstadoAsync(peticion);
            return Ok(new
            {
                estado = "actualizado",
                mensaje = $"La matrícula quedó en estado '{programa.Estado}'.",
                programa,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Matrícula no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cambiar el estado de la matrícula.", ex.Message);
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
