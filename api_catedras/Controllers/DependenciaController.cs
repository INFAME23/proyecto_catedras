// ==========================================================================
//  Controllers/DependenciaController.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Capa HTTP de 'Dependencia'. Mismo patrón que SedeController (v1): recibe la
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
[Route("api/Dependencia")]
public class DependenciaController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioDependencia _servicio;

    public DependenciaController(IServicioDependencia servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF68 · GET /api/Dependencia?limite=N   -> 200 { tabla, limite, total, datos } | 204
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
                tabla = "dependencia",
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
            return Problema(500, "error_interno", "Error al listar las dependencias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF69 · GET /api/Dependencia/{idDependencia}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idDependencia}")]
    public async Task<IActionResult> Obtener(string idDependencia)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idDependencia));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Dependencia no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la dependencia.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF70 · POST /api/Dependencia   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] DependenciaCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Dependencia '{peticion.IdDependencia}' creada.",
            });
        }
        catch (ArgumentException ex)
        {
            // Autorreferencia: la dependencia se nombró a sí misma como padre.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Duplicado (pk_dependencia / uq_dependencia_nombre) o padre inexistente
            // (FK fk_dependencia_padre): el motor aborta el INSERT completo.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la dependencia.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF71 · PUT /api/Dependencia/{idDependencia}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idDependencia}")]
    public async Task<IActionResult> Reemplazar(string idDependencia, [FromBody] DependenciaReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idDependencia, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Dependencia no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la dependencia.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF72 · PATCH /api/Dependencia/{idDependencia}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idDependencia}")]
    public async Task<IActionResult> Actualizar(string idDependencia, [FromBody] DependenciaActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idDependencia, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Dependencia no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la dependencia.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF73 · DELETE /api/Dependencia/{idDependencia}   -> 200 { filasEliminadas } | 404
    //  No falla por tener hijas ni cátedras: ambas FK son ON DELETE SET NULL.
    // ----------------------------------------------------------------------
    [HttpDelete("{idDependencia}")]
    public async Task<IActionResult> Eliminar(string idDependencia)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idDependencia);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Dependencia no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la dependencia.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la dependencia.", ex.Message);
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
