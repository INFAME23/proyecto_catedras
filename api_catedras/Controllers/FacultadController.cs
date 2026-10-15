// ==========================================================================
//  Controllers/FacultadController.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Capa HTTP de 'Facultad'. Mismo patrón que SedeController (v1): recibe la
//  petición ya validada (si no, 422 antes de llegar aquí), llama al
//  servicio por su INTERFAZ y traduce resultado/excepción a código HTTP:
//     ArgumentException     -> 400  (incluida la autorreferencia)
//     NoEncontradoExcepcion -> 404
//     NpgsqlException        -> 500  (duplicado o FK violada; mensaje del motor)
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

// [ApiController]: enlaza el body y valida la petición (422 automático).
// [Route]: prefijo común de todos los métodos.
[ApiController]
[Route("api/Facultad")]
public class FacultadController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioFacultad _servicio;

    public FacultadController(IServicioFacultad servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF62 · GET /api/Facultad?limite=N   -> 200 { tabla, limite, total, datos } | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? limite, [FromQuery] bool incluirInactivos = false)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(limite, incluirInactivos);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "facultad",
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
            return Problema(500, "error_interno", "Error al listar las facultades.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF63 · GET /api/Facultad/{idFacultad}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idFacultad}")]
    public async Task<IActionResult> Obtener(string idFacultad)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idFacultad));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Facultad no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la facultad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF64 · POST /api/Facultad   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] FacultadCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Facultad '{peticion.IdFacultad}' creada.",
            });
        }
        catch (ArgumentException ex)
        {
            // Autorreferencia: la facultad se nombró a sí misma como padre.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Duplicado (pk_facultad / uq_facultad_nombre) o padre inexistente
            // (FK fk_facultad_padre): el motor aborta el INSERT completo.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la facultad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF65 · PUT /api/Facultad/{idFacultad}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idFacultad}")]
    public async Task<IActionResult> Reemplazar(string idFacultad, [FromBody] FacultadReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idFacultad, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Facultad no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la facultad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF66 · PATCH /api/Facultad/{idFacultad}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idFacultad}")]
    public async Task<IActionResult> Actualizar(string idFacultad, [FromBody] FacultadActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idFacultad, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Facultad no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la facultad.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF67 · DELETE /api/Facultad/{idFacultad}   -> 200 { filasEliminadas } | 404
    //  No falla por tener hijas ni programas: ambas FK son ON DELETE SET NULL.
    // ----------------------------------------------------------------------
    [HttpDelete("{idFacultad}")]
    public async Task<IActionResult> Eliminar(string idFacultad)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idFacultad);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Facultad no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la facultad.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la facultad.", ex.Message);
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
