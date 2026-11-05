// ==========================================================================
//  Controllers/EncuestaController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'Encuesta'. Mismo patrón que SedeController, salvo:
//   · el parámetro de ruta es 'long idEncuesta' (la BD genera el id);
//   · el POST exitoso agrega 'id_encuesta' a la respuesta (3_plan.md §4.7).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Encuesta")]
public class EncuestaController : ControllerBase
{
    private readonly IServicioEncuesta _servicio;

    public EncuestaController(IServicioEncuesta servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/Encuesta?limite=N   -> 200 { tabla, limite, total, datos }
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "encuesta",
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
            return Problema(500, "error_interno", "Error al listar las encuestas.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/Encuesta/{idEncuesta}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idEncuesta:long}")]
    public async Task<IActionResult> Obtener(long idEncuesta)
    {
        try
        {
            var encuesta = await _servicio.ObtenerAsync(idEncuesta);
            return Ok(encuesta);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Encuesta no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la encuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/Encuesta   -> 200 { estado, mensaje, id_encuesta } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] EncuestaCrear peticion)
    {
        try
        {
            long idEncuesta = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Encuesta '{peticion.Nombre}' creada"
                        + ((peticion.Preguntas?.Count ?? 0) > 0 ? $" con {peticion.Preguntas!.Count} preguntas." : "."),
                id_encuesta = idEncuesta,
                // Maestro-detalle (Regla 1): cuántas preguntas nacieron con ella.
                preguntas_creadas = peticion.Preguntas?.Count ?? 0,
            });
        }
        catch (NpgsqlException ex)
        {
            // [ACID · C] (nombre, version) duplicado viola uq_encuesta_ver.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            // Incluye 'vigente_hasta' anterior a 'vigente_desde'.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la encuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/Encuesta/{idEncuesta}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idEncuesta:long}")]
    public async Task<IActionResult> Reemplazar(long idEncuesta, [FromBody] EncuestaReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idEncuesta, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Encuesta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la encuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/Encuesta/{idEncuesta} -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idEncuesta:long}")]
    public async Task<IActionResult> Actualizar(long idEncuesta, [FromBody] EncuestaActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idEncuesta, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            // body vacío o 'vigente_hasta' anterior a 'vigente_desde' -> 400
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Encuesta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la encuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/Encuesta/{idEncuesta} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idEncuesta:long}")]
    public async Task<IActionResult> Eliminar(long idEncuesta)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idEncuesta);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Encuesta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la encuesta.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la encuesta.", ex.Message);
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
