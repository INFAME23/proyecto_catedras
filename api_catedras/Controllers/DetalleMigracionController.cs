// ==========================================================================
//  Controllers/DetalleMigracionController.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Capa HTTP de 'DetalleMigracion'. 3 endpoints (RF186-RF189):
//     GET   /api/DetalleMigracion?fkLoteMigracion=ID   lista (las de un lote)
//     GET   /api/DetalleMigracion/{id}                 una
//     PATCH /api/DetalleMigracion/{id}                 resultado / mensaje
//  Sin POST: un detalle nace solo al generar su lote (POST
//  /api/LoteMigracion), igual que NovedadCarga (v3). Sin PUT ni DELETE.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/DetalleMigracion")]
public class DetalleMigracionController : ControllerBase
{
    private readonly IServicioDetalleMigracion _servicio;

    public DetalleMigracionController(IServicioDetalleMigracion servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF186 · GET /api/DetalleMigracion?fkLoteMigracion=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkLoteMigracion, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkLoteMigracion, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "detalle_migracion",
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
            return Problema(500, "error_interno", "Error al listar los detalles de migración.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF187 · GET /api/DetalleMigracion/{idDetalle}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idDetalle:long}")]
    public async Task<IActionResult> Obtener(long idDetalle)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idDetalle));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Detalle de migración no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el detalle de migración.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF188 · PATCH /api/DetalleMigracion/{idDetalle}   -> 200 | 400 | 404 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPatch("{idDetalle:long}")]
    public async Task<IActionResult> Actualizar(long idDetalle, [FromBody] DetalleMigracionActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idDetalle, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Detalle de migración no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Segundo ACEPTADO del mismo registro en otro lote (ux_detmig_aceptado).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el detalle de migración.", ex.Message);
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
