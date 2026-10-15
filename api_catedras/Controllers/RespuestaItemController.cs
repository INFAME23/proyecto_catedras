// ==========================================================================
//  Controllers/RespuestaItemController.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Capa HTTP de 'RespuestaItem'. SOLO LECTURA (RF176-RF178): 2 endpoints.
//     GET   /api/RespuestaItem?fkRespuesta=ID   lista
//     GET   /api/RespuestaItem/{idItem}         uno
//
//  Sin POST propio (nace solo dentro de POST /api/RespuestaEncuesta), sin
//  PUT/PATCH/DELETE: esas rutas responden 404 del propio ASP.NET — mismo
//  criterio que 'NovedadCarga' en v3.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/RespuestaItem")]
public class RespuestaItemController : ControllerBase
{
    private readonly IServicioRespuestaItem _servicio;

    public RespuestaItemController(IServicioRespuestaItem servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF176 · GET /api/RespuestaItem?fkRespuesta=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkRespuesta, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkRespuesta, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "respuesta_item",
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
            return Problema(500, "error_interno", "Error al listar los ítems de respuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF177 · GET /api/RespuestaItem/{idItem}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idItem:long}")]
    public async Task<IActionResult> Obtener(long idItem)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idItem));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Ítem de respuesta no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el ítem de respuesta.", ex.Message);
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
