// ==========================================================================
//  Controllers/ProgramaAcademicoController.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Capa HTTP de 'ProgramaAcademico' (la clave de la URL es el 'codigo'). Mismo
//  patrón que SedeController (v1): recibe la
//  petición ya validada (si no, 422 antes de llegar aquí), llama al
//  servicio por su INTERFAZ y traduce resultado/excepción a código HTTP:
//     ArgumentException     -> 400  (límite inválido, PATCH vacío)
//     NoEncontradoExcepcion -> 404
//     NpgsqlException        -> 500  (duplicado, facultad inexistente o
//                                      borrado bloqueado por RESTRICT)
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
[Route("api/ProgramaAcademico")]
public class ProgramaAcademicoController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioProgramaAcademico _servicio;

    public ProgramaAcademicoController(IServicioProgramaAcademico servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF74 · GET /api/ProgramaAcademico?limite=N   -> 200 { tabla, limite, total, datos } | 204
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
                tabla = "programa_academico",
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
            return Problema(500, "error_interno", "Error al listar los programas académicos.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF75 · GET /api/ProgramaAcademico/{codigo}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{codigo}")]
    public async Task<IActionResult> Obtener(string codigo)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(codigo));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Programa académico no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el programa académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF76 · POST /api/ProgramaAcademico   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ProgramaAcademicoCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Programa académico '{peticion.Codigo}' creado.",
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Código duplicado (pk_programa_academico) o 'fk_facultad'
            // inexistente (fk_programa_facultad): el motor aborta el INSERT.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el programa académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF77 · PUT /api/ProgramaAcademico/{codigo}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{codigo}")]
    public async Task<IActionResult> Reemplazar(string codigo, [FromBody] ProgramaAcademicoReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(codigo, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Programa académico no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar el programa académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF78 · PATCH /api/ProgramaAcademico/{codigo}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{codigo}")]
    public async Task<IActionResult> Actualizar(string codigo, [FromBody] ProgramaAcademicoActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(codigo, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Programa académico no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el programa académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF79 · DELETE /api/ProgramaAcademico/{codigo}   -> 200 { filasEliminadas } | 404
    //  Si el programa tiene asistentes (programa_asistente, RESTRICT) -> 500
    //  y NADA se borra: ni el programa ni sus alias (CASCADE no llega a ocurrir).
    // ----------------------------------------------------------------------
    [HttpDelete("{codigo}")]
    public async Task<IActionResult> Eliminar(string codigo)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(codigo);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Programa académico no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // [ACID · A] La sentencia completa se revierte: los alias siguen ahí.
            return Problema(500, "error_motor", "No se pudo eliminar el programa académico.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el programa académico.", ex.Message);
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
