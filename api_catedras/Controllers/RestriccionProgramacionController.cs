// ==========================================================================
//  Controllers/RestriccionProgramacionController.cs  ·  Proyecto Cátedras · v7.1 · D57
// --------------------------------------------------------------------------
//  Capa HTTP de las ventanas de programación (los periodos, horas y días en que
//  se pueden programar sesiones):
//     GET    /api/RestriccionProgramacion?soloActivas=&limite=     lista (204 si no hay)
//     GET    /api/RestriccionProgramacion/{id}                     una
//     POST   /api/RestriccionProgramacion                          crea
//     PUT    /api/RestriccionProgramacion/{id}                     reemplaza (también activa o pausa)
//     DELETE /api/RestriccionProgramacion/{id}                     borra
//     GET    /api/RestriccionProgramacion/validar?inicio=&fin=     ¿cabe ese horario? (solo lectura)
//
//  Cuándo se RECHAZA una sesión no lo decide este controlador: lo hace la base de datos
//  (disparador sobre 'sesion'); las respuestas de Sesion y Catedra lo devuelven como 409.
//  La API no autoriza (D28): quién puede escribir aquí lo decide el front (/admin).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/RestriccionProgramacion")]
public class RestriccionProgramacionController : ControllerBase
{
    private readonly IServicioRestriccionProgramacion _servicio;

    public RestriccionProgramacionController(IServicioRestriccionProgramacion servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool soloActivas, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(soloActivas, limite);

            if (total == 0)
                return NoContent();

            return Ok(new { tabla = "restriccion_programacion", limite = limite ?? 500, total, datos });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al listar las restricciones de programación.", ex.Message);
        }
    }

    [HttpGet("{idRestriccion:long}")]
    public async Task<IActionResult> Obtener(long idRestriccion)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idRestriccion));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Restricción no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la restricción.", ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RestriccionProgramacionEscribir peticion)
    {
        try
        {
            var fila = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Restricción {fila.IdRestriccion} creada.",
                id_restriccion = fila.IdRestriccion,
                restriccion = fila,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la restricción.", ex.Message);
        }
    }

    [HttpPut("{idRestriccion:long}")]
    public async Task<IActionResult> Reemplazar(long idRestriccion, [FromBody] RestriccionProgramacionEscribir peticion)
    {
        try
        {
            var fila = await _servicio.ReemplazarAsync(idRestriccion, peticion);
            return Ok(new
            {
                estado = "actualizado",
                mensaje = $"Restricción {idRestriccion} actualizada.",
                restriccion = fila,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Restricción no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la restricción.", ex.Message);
        }
    }

    [HttpDelete("{idRestriccion:long}")]
    public async Task<IActionResult> Eliminar(long idRestriccion)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idRestriccion);
            return Ok(new
            {
                estado = "eliminado",
                mensaje = $"Restricción {idRestriccion} eliminada.",
                filasEliminadas = filas,
            });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Restricción no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la restricción.", ex.Message);
        }
    }

    // GET /api/RestriccionProgramacion/validar?inicio=2026-10-20T14:00:00-05:00&fin=...   -> 200 | 400
    // (el '+' de una zona positiva debe ir como %2B en la URL)
    [HttpGet("validar")]
    public async Task<IActionResult> Validar([FromQuery] DateTimeOffset? inicio, [FromQuery] DateTimeOffset? fin)
    {
        try
        {
            return Ok(await _servicio.ValidarAsync(inicio, fin));
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al validar el horario.", ex.Message);
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
