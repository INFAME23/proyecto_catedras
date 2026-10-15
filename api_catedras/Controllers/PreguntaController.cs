// ==========================================================================
//  Controllers/PreguntaController.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Capa HTTP de 'Pregunta'. Molde completo de 6 endpoints, igual patrón
//  que SesionController (v4): recibe la petición ya validada (si no, 422
//  antes de llegar aquí), llama al servicio por su INTERFAZ y traduce
//  resultado/excepción a código HTTP:
//     ArgumentException     -> 400  (límite inválido, rango valor_min/max,
//                                     PATCH vacío)
//     NoEncontradoExcepcion -> 404
//     NpgsqlException        -> 500  ('(fk_encuesta, orden)' duplicado,
//                                      tipo de pregunta inexistente, opción
//                                      con 'orden' repetido, o borrado
//                                      bloqueado por RESTRICT)
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/Pregunta")]
public class PreguntaController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioPregunta _servicio;

    public PreguntaController(IServicioPregunta servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF159 · GET /api/Pregunta?fkEncuesta=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkEncuesta, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkEncuesta, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "pregunta",
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
            return Problema(500, "error_interno", "Error al listar las preguntas.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF160 · GET /api/Pregunta/{idPregunta}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idPregunta:long}")]
    public async Task<IActionResult> Obtener(long idPregunta)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idPregunta));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Pregunta no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF161 · POST /api/Pregunta   -> 200 { estado, mensaje, id_pregunta } | 400 | 422 | 500
    //  Retrofit V6 (Guía IA, Regla 1): el body puede traer 'opciones'
    //  iniciales; si alguna falla (orden repetido), NADA queda creado.
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] PreguntaCrear peticion)
    {
        try
        {
            long idPregunta = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Pregunta creada para la encuesta {peticion.FkEncuesta}.",
                id_pregunta = idPregunta,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // '(fk_encuesta, orden)' duplicado (uq_pregunta_ord), tipo de
            // pregunta inexistente, o un 'orden' de opción repetido
            // (uq_opcion_orden) dentro del mismo CALL: el motor revierte
            // TODO, ni la pregunta queda creada.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF162 · PUT /api/Pregunta/{idPregunta}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idPregunta:long}")]
    public async Task<IActionResult> Reemplazar(long idPregunta, [FromBody] PreguntaReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idPregunta, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Pregunta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF163 · PATCH /api/Pregunta/{idPregunta}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idPregunta:long}")]
    public async Task<IActionResult> Actualizar(long idPregunta, [FromBody] PreguntaActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idPregunta, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Pregunta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF164 · DELETE /api/Pregunta/{idPregunta}   -> 200 { filasEliminadas } | 404
    //  Si la pregunta tiene respuestas de ejemplo (respuesta_item, RESTRICT)
    //  -> 500, y la cascada hacia sus 'opcion_pregunta' no llega a ocurrir.
    // ----------------------------------------------------------------------
    [HttpDelete("{idPregunta:long}")]
    public async Task<IActionResult> Eliminar(long idPregunta)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idPregunta);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Pregunta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // [ACID · A] La sentencia completa se revierte: las opciones siguen ahí.
            return Problema(500, "error_motor", "No se pudo eliminar la pregunta.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la pregunta.", ex.Message);
        }
    }

    // Arma el sobre de error { estado, mensaje, detalle } con el código indicado.
    private ObjectResult Problema(int codigo, string estado, string mensaje, string? detalle)
    {
        return new ObjectResult(new { estado, mensaje, detalle })
        {
            StatusCode = codigo,
        };
    }
}
