// ==========================================================================
//  Controllers/PeriodoAcademicoController.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Capa HTTP de 'PeriodoAcademico'. Mismo patrón que SedeController.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/PeriodoAcademico")]
public class PeriodoAcademicoController : ControllerBase
{
    private readonly IServicioPeriodoAcademico _servicio;

    public PeriodoAcademicoController(IServicioPeriodoAcademico servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/PeriodoAcademico?limite=N   -> 200 { tabla, limite, total, datos }
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
                tabla = "periodo_academico",
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
            return Problema(500, "error_interno", "Error al listar los periodos académicos.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/PeriodoAcademico/{idPeriodo}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idPeriodo}")]
    public async Task<IActionResult> Obtener(string idPeriodo)
    {
        try
        {
            var periodo = await _servicio.ObtenerAsync(idPeriodo);
            return Ok(periodo);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Periodo académico no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el periodo académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/PeriodoAcademico   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] PeriodoAcademicoCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Periodo académico '{peticion.IdPeriodo}' creado.",
            });
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            // Incluye 'fecha_fin' anterior o igual a 'fecha_ini'.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el periodo académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/PeriodoAcademico/{idPeriodo}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idPeriodo}")]
    public async Task<IActionResult> Reemplazar(string idPeriodo, [FromBody] PeriodoAcademicoReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idPeriodo, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Periodo académico no encontrado.", ex.Message);
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
            return Problema(500, "error_interno", "Error al reemplazar el periodo académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/PeriodoAcademico/{idPeriodo} -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idPeriodo}")]
    public async Task<IActionResult> Actualizar(string idPeriodo, [FromBody] PeriodoAcademicoActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idPeriodo, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            // body vacío o 'fecha_fin' <= 'fecha_ini' -> 400
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Periodo académico no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el periodo académico.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  DELETE /api/PeriodoAcademico/{idPeriodo} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idPeriodo}")]
    public async Task<IActionResult> Eliminar(string idPeriodo)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idPeriodo);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Periodo académico no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el periodo académico.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el periodo académico.", ex.Message);
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
