// ==========================================================================
//  Controllers/NovedadCargaController.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Capa HTTP de 'NovedadCarga'. SOLO 2 endpoints de lectura (RF118):
//     GET /api/NovedadCarga?fkLoteCarga=ID   lista (las de un lote)
//     GET /api/NovedadCarga/{id}             una
//  Sin POST: una novedad nace únicamente al cerrar un lote
//  (POST /api/LoteCargaAsistente/{id}/cerrar). Sin PUT/PATCH/DELETE.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/NovedadCarga")]
public class NovedadCargaController : ControllerBase
{
    private readonly IServicioNovedadCarga _servicio;

    public NovedadCargaController(IServicioNovedadCarga servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF118 · GET /api/NovedadCarga?fkLoteCarga=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkLoteCarga, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkLoteCarga, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "novedad_carga",
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
            return Problema(500, "error_interno", "Error al listar las novedades.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF118 · GET /api/NovedadCarga/{idNovedad}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idNovedad:long}")]
    public async Task<IActionResult> Obtener(long idNovedad)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idNovedad));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Novedad no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la novedad.", ex.Message);
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
