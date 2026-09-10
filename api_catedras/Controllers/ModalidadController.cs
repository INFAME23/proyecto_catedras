// ==========================================================================
//  Controllers/ModalidadController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'Modalidad'. Mismo patrón que SedeController: recibe la
//  petición, llama al servicio por su interfaz y traduce el resultado o la
//  excepción a un código HTTP.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Modalidad")]
public class ModalidadController : ControllerBase
{
    private readonly IServicioModalidad _servicio;

    public ModalidadController(IServicioModalidad servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/Modalidad?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "modalidad",
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
            return Problema(500, "error_interno", "Error al listar las modalidades.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/Modalidad/{idModalidad}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idModalidad}")]
    public async Task<IActionResult> Obtener(string idModalidad)
    {
        try
        {
            var modalidad = await _servicio.ObtenerAsync(idModalidad);
            return Ok(modalidad);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Modalidad no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la modalidad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/Modalidad   -> 200 { estado, mensaje } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ModalidadCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Modalidad '{peticion.IdModalidad}' creada.",
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
            return Problema(500, "error_interno", "Error al crear la modalidad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/Modalidad/{idModalidad}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idModalidad}")]
    public async Task<IActionResult> Reemplazar(string idModalidad, [FromBody] ModalidadReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idModalidad, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Modalidad no encontrada.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar la modalidad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/Modalidad/{idModalidad} -> 200 { filasAfectadas } | 404 | 400
    // ----------------------------------------------------------------------
    [HttpPatch("{idModalidad}")]
    public async Task<IActionResult> Actualizar(string idModalidad, [FromBody] ModalidadActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idModalidad, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Modalidad no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la modalidad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/Modalidad/{idModalidad} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idModalidad}")]
    public async Task<IActionResult> Eliminar(string idModalidad)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idModalidad);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Modalidad no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la modalidad.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la modalidad.", ex.Message);
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
