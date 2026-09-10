// ==========================================================================
//  Controllers/RolController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'Rol'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Rol")]
public class RolController : ControllerBase
{
    private readonly IServicioRol _servicio;

    public RolController(IServicioRol servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/Rol?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "rol",
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
            return Problema(500, "error_interno", "Error al listar los roles.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/Rol/{idRol}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idRol}")]
    public async Task<IActionResult> Obtener(string idRol)
    {
        try
        {
            var rol = await _servicio.ObtenerAsync(idRol);
            return Ok(rol);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Rol no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el rol.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/Rol   -> 200 { estado, mensaje } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RolCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Rol '{peticion.IdRol}' creado.",
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
            return Problema(500, "error_interno", "Error al crear el rol.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/Rol/{idRol}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idRol}")]
    public async Task<IActionResult> Reemplazar(string idRol, [FromBody] RolReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idRol, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Rol no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el rol.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/Rol/{idRol} -> 200 { filasAfectadas } | 404 | 400
    // ----------------------------------------------------------------------
    [HttpPatch("{idRol}")]
    public async Task<IActionResult> Actualizar(string idRol, [FromBody] RolActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idRol, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Rol no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el rol.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/Rol/{idRol} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idRol}")]
    public async Task<IActionResult> Eliminar(string idRol)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idRol);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Rol no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el rol.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el rol.", ex.Message);
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
