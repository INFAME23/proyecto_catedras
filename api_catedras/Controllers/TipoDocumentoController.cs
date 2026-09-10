// ==========================================================================
//  Controllers/TipoDocumentoController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'TipoDocumento'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/TipoDocumento")]
public class TipoDocumentoController : ControllerBase
{
    private readonly IServicioTipoDocumento _servicio;

    public TipoDocumentoController(IServicioTipoDocumento servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoDocumento?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "tipo_documento",
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
            return Problema(500, "error_interno", "Error al listar los tipos de documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/TipoDocumento/{idTipoDocumento}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idTipoDocumento}")]
    public async Task<IActionResult> Obtener(string idTipoDocumento)
    {
        try
        {
            var tipoDocumento = await _servicio.ObtenerAsync(idTipoDocumento);
            return Ok(tipoDocumento);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de documento no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el tipo de documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/TipoDocumento   -> 200 { estado, mensaje } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] TipoDocumentoCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Tipo de documento '{peticion.IdTipoDocumento}' creado.",
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
            return Problema(500, "error_interno", "Error al crear el tipo de documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/TipoDocumento/{idTipoDocumento}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idTipoDocumento}")]
    public async Task<IActionResult> Reemplazar(string idTipoDocumento, [FromBody] TipoDocumentoReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idTipoDocumento, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de documento no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el tipo de documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/TipoDocumento/{idTipoDocumento} -> 200 { filasAfectadas } | 404 | 400
    // ----------------------------------------------------------------------
    [HttpPatch("{idTipoDocumento}")]
    public async Task<IActionResult> Actualizar(string idTipoDocumento, [FromBody] TipoDocumentoActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idTipoDocumento, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de documento no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el tipo de documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/TipoDocumento/{idTipoDocumento} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idTipoDocumento}")]
    public async Task<IActionResult> Eliminar(string idTipoDocumento)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idTipoDocumento);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Tipo de documento no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el tipo de documento.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el tipo de documento.", ex.Message);
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
