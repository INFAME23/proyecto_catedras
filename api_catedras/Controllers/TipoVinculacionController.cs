// ==========================================================================
//  Controllers/TipoVinculacionController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'TipoVinculacion'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/TipoVinculacion")]
public class TipoVinculacionController : ControllerBase
{
    private readonly IServicioTipoVinculacion _servicio;

    public TipoVinculacionController(IServicioTipoVinculacion servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoVinculacion?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "tipo_vinculacion",
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
            return Problema(500, "error_interno", "Error al listar los tipos de vinculación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoVinculacion/{idTipoVinculacion}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idTipoVinculacion}")]
    public async Task<IActionResult> Obtener(string idTipoVinculacion)
    {
        try
        {
            var tipoVinculacion = await _servicio.ObtenerAsync(idTipoVinculacion);
            return Ok(tipoVinculacion);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de vinculación no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el tipo de vinculación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/TipoVinculacion   -> 200 { estado, mensaje } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] TipoVinculacionCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Tipo de vinculación '{peticion.IdTipoVinculacion}' creado.",
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
            return Problema(500, "error_interno", "Error al crear el tipo de vinculación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/TipoVinculacion/{idTipoVinculacion}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idTipoVinculacion}")]
    public async Task<IActionResult> Reemplazar(string idTipoVinculacion, [FromBody] TipoVinculacionReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idTipoVinculacion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de vinculación no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el tipo de vinculación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/TipoVinculacion/{idTipoVinculacion} -> 200 { filasAfectadas } | 404 | 400
    // ----------------------------------------------------------------------
    [HttpPatch("{idTipoVinculacion}")]
    public async Task<IActionResult> Actualizar(string idTipoVinculacion, [FromBody] TipoVinculacionActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idTipoVinculacion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de vinculación no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el tipo de vinculación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/TipoVinculacion/{idTipoVinculacion} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idTipoVinculacion}")]
    public async Task<IActionResult> Eliminar(string idTipoVinculacion)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idTipoVinculacion);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de vinculación no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el tipo de vinculación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el tipo de vinculación.", ex.Message);
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
