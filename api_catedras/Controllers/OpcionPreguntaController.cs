// ==========================================================================
//  Controllers/OpcionPreguntaController.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Capa HTTP de 'OpcionPregunta'. Molde completo de 6 endpoints (D33: a
//  diferencia de 'AliasPrograma'/'ConsentimientoDatos', esta entidad SÍ
//  tiene PUT/PATCH).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/OpcionPregunta")]
public class OpcionPreguntaController : ControllerBase
{
    private readonly IServicioOpcionPregunta _servicio;

    public OpcionPreguntaController(IServicioOpcionPregunta servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF167 · GET /api/OpcionPregunta?fkPregunta=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkPregunta, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkPregunta, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "opcion_pregunta",
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
            return Problema(500, "error_interno", "Error al listar las opciones de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF168 · GET /api/OpcionPregunta/{idOpcion}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idOpcion:long}")]
    public async Task<IActionResult> Obtener(long idOpcion)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idOpcion));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Opción de pregunta no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la opción de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF169 · POST /api/OpcionPregunta   -> 200 { estado, mensaje, id_opcion } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] OpcionPreguntaCrear peticion)
    {
        try
        {
            long idOpcion = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Opción creada para la pregunta {peticion.FkPregunta}.",
                id_opcion = idOpcion,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // '(fk_pregunta, orden)' duplicado (uq_opcion_orden) o
            // 'fk_pregunta' inexistente: el motor rechaza el INSERT.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la opción de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF170 · PUT /api/OpcionPregunta/{idOpcion}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idOpcion:long}")]
    public async Task<IActionResult> Reemplazar(long idOpcion, [FromBody] OpcionPreguntaReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idOpcion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Opción de pregunta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la opción de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF170 · PATCH /api/OpcionPregunta/{idOpcion}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idOpcion:long}")]
    public async Task<IActionResult> Actualizar(long idOpcion, [FromBody] OpcionPreguntaActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idOpcion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Opción de pregunta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la opción de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF171 · DELETE /api/OpcionPregunta/{idOpcion}   -> 200 { filasEliminadas } | 404
    //  Opción ya elegida en alguna respuesta de ejemplo (respuesta_item.fk_opcion,
    //  RESTRICT) -> 500.
    // ----------------------------------------------------------------------
    [HttpDelete("{idOpcion:long}")]
    public async Task<IActionResult> Eliminar(long idOpcion)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idOpcion);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Opción de pregunta no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la opción de pregunta.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la opción de pregunta.", ex.Message);
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
