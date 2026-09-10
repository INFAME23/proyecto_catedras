// ==========================================================================
//  Controllers/TipoEventoController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'TipoEvento'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/TipoEvento")]
public class TipoEventoController : ControllerBase
{
    private readonly IServicioTipoEvento _servicio;

    public TipoEventoController(IServicioTipoEvento servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoEvento?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "tipo_evento",
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
            return Problema(500, "error_interno", "Error al listar los tipos de evento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoEvento/{idTipoEvento}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idTipoEvento}")]
    public async Task<IActionResult> Obtener(string idTipoEvento)
    {
        try
        {
            var tipoEvento = await _servicio.ObtenerAsync(idTipoEvento);
            return Ok(tipoEvento);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de evento no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el tipo de evento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/TipoEvento   -> 200 { estado, mensaje } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] TipoEventoCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Tipo de evento '{peticion.IdTipoEvento}' creado.",
            });
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
            return Problema(500, "error_interno", "Error al crear el tipo de evento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/TipoEvento/{idTipoEvento}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idTipoEvento}")]
    public async Task<IActionResult> Reemplazar(string idTipoEvento, [FromBody] TipoEventoReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idTipoEvento, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de evento no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el tipo de evento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/TipoEvento/{idTipoEvento} -> 200 { filasAfectadas } | 404 | 400
    // ----------------------------------------------------------------------
    [HttpPatch("{idTipoEvento}")]
    public async Task<IActionResult> Actualizar(string idTipoEvento, [FromBody] TipoEventoActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idTipoEvento, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de evento no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el tipo de evento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/TipoEvento/{idTipoEvento} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idTipoEvento}")]
    public async Task<IActionResult> Eliminar(string idTipoEvento)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idTipoEvento);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de evento no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el tipo de evento.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el tipo de evento.", ex.Message);
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
