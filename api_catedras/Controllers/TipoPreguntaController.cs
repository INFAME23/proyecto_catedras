// ==========================================================================
//  Controllers/TipoPreguntaController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'TipoPregunta'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/TipoPregunta")]
public class TipoPreguntaController : ControllerBase
{
    private readonly IServicioTipoPregunta _servicio;

    public TipoPreguntaController(IServicioTipoPregunta servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoPregunta?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "tipo_pregunta",
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
            return Problema(500, "error_interno", "Error al listar los tipos de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoPregunta/{idTipoPregunta}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idTipoPregunta}")]
    public async Task<IActionResult> Obtener(string idTipoPregunta)
    {
        try
        {
            var tipoPregunta = await _servicio.ObtenerAsync(idTipoPregunta);
            return Ok(tipoPregunta);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de pregunta no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el tipo de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/TipoPregunta   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] TipoPreguntaCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Tipo de pregunta '{peticion.IdTipoPregunta}' creado.",
            });
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            // Incluye la coherencia guarda_numero/guarda_texto/guarda_opcion violada.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el tipo de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/TipoPregunta/{idTipoPregunta}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idTipoPregunta}")]
    public async Task<IActionResult> Reemplazar(string idTipoPregunta, [FromBody] TipoPreguntaReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idTipoPregunta, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de pregunta no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el tipo de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/TipoPregunta/{idTipoPregunta} -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idTipoPregunta}")]
    public async Task<IActionResult> Actualizar(string idTipoPregunta, [FromBody] TipoPreguntaActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idTipoPregunta, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            // body vacío o coherencia de los 3 campos violada -> 400
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de pregunta no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el tipo de pregunta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/TipoPregunta/{idTipoPregunta} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idTipoPregunta}")]
    public async Task<IActionResult> Eliminar(string idTipoPregunta)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idTipoPregunta);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de pregunta no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el tipo de pregunta.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el tipo de pregunta.", ex.Message);
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
