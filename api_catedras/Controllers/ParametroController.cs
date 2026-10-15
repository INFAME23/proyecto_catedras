// ==========================================================================
//  Controllers/ParametroController.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Capa HTTP de 'Parametro' (configuración del sistema). Mismo patrón que
//  SedeController: recibe, delega en el servicio y traduce excepciones a
//  códigos HTTP. No contiene reglas de negocio ni SQL.
//
//    GET    /api/Parametro?limite=N      -> 200 | 204 | 400
//    GET    /api/Parametro/{clave}       -> 200 | 404
//    POST   /api/Parametro               -> 200 | 400 | 422 | 500
//    PUT    /api/Parametro/{clave}       -> 200 | 404 | 422 | 500
//    PATCH  /api/Parametro/{clave}       -> 200 | 400 | 404 | 422 | 500
//    DELETE /api/Parametro/{clave}       -> 200 | 404 | 500
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/Parametro")]
public class ParametroController : ControllerBase
{
    private readonly IServicioParametro _servicio;

    public ParametroController(IServicioParametro servicio)
    {
        _servicio = servicio;
    }

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
                tabla = "parametro",
                limite = limite ?? 1000,   // el efectivo usado
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
            return Problema(500, "error_interno", "Error al listar los parámetros.", ex.Message);
        }
    }

    [HttpGet("{clave}")]
    public async Task<IActionResult> Obtener(string clave)
    {
        try
        {
            var parametro = await _servicio.ObtenerAsync(clave);
            return Ok(parametro);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Parámetro no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el parámetro.", ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ParametroCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);

            return Ok(new
            {
                estado = "creado",
                mensaje = $"Parámetro '{peticion.Clave}' creado.",
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
            return Problema(500, "error_interno", "Error al crear el parámetro.", ex.Message);
        }
    }

    [HttpPut("{clave}")]
    public async Task<IActionResult> Reemplazar(string clave, [FromBody] ParametroReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(clave, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Parámetro no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el parámetro.", ex.Message);
        }
    }

    [HttpPatch("{clave}")]
    public async Task<IActionResult> Actualizar(string clave, [FromBody] ParametroActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(clave, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Parámetro no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el parámetro.", ex.Message);
        }
    }

    [HttpDelete("{clave}")]
    public async Task<IActionResult> Eliminar(string clave)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(clave);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Parámetro no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el parámetro.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el parámetro.", ex.Message);
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
