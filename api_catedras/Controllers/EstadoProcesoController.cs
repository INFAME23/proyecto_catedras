// ==========================================================================
//  Controllers/EstadoProcesoController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'EstadoProceso'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/EstadoProceso")]
public class EstadoProcesoController : ControllerBase
{
    private readonly IServicioEstadoProceso _servicio;

    public EstadoProcesoController(IServicioEstadoProceso servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/EstadoProceso?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "estado_proceso",
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
            return Problema(500, "error_interno", "Error al listar los estados de proceso.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/EstadoProceso/{idEstado}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idEstado}")]
    public async Task<IActionResult> Obtener(string idEstado)
    {
        try
        {
            var estado = await _servicio.ObtenerAsync(idEstado);
            return Ok(estado);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Estado de proceso no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el estado de proceso.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/EstadoProceso   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] EstadoProcesoCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Estado de proceso '{peticion.IdEstado}' creado.",
            });
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            // Incluye la coherencia es_exitoso/es_final violada.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el estado de proceso.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/EstadoProceso/{idEstado}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idEstado}")]
    public async Task<IActionResult> Reemplazar(string idEstado, [FromBody] EstadoProcesoReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idEstado, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Estado de proceso no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el estado de proceso.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/EstadoProceso/{idEstado} -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idEstado}")]
    public async Task<IActionResult> Actualizar(string idEstado, [FromBody] EstadoProcesoActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idEstado, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            // body vacío o coherencia es_exitoso/es_final violada -> 400
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Estado de proceso no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el estado de proceso.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/EstadoProceso/{idEstado} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idEstado}")]
    public async Task<IActionResult> Eliminar(string idEstado)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idEstado);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Estado de proceso no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el estado de proceso.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el estado de proceso.", ex.Message);
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
