// ==========================================================================
//  Controllers/ConsentimientoDatosController.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Capa HTTP de 'ConsentimientoDatos'. SOLO 3 endpoints: GET lista,
//  GET/{id} y POST (RF112-RF113). NO hay [HttpPut], [HttpPatch] ni
//  [HttpDelete]: Ley 1581, el consentimiento es un hecho legal que se
//  registra, nunca se corrige ni se retira por API. Más estricto que
//  AliasPrograma (v2), que sí tenía DELETE.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/ConsentimientoDatos")]
public class ConsentimientoDatosController : ControllerBase
{
    private readonly IServicioConsentimientoDatos _servicio;

    public ConsentimientoDatosController(IServicioConsentimientoDatos servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF112 · GET /api/ConsentimientoDatos?fkAsistente=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkAsistente, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkAsistente, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "consentimiento_datos",
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
            return Problema(500, "error_interno", "Error al listar los consentimientos.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF112 · GET /api/ConsentimientoDatos/{idConsentimiento}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idConsentimiento:long}")]
    public async Task<IActionResult> Obtener(long idConsentimiento)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idConsentimiento));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Consentimiento no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el consentimiento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF112 · POST /api/ConsentimientoDatos
    //          -> 200 { estado, mensaje, id_consentimiento, aceptado_en } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ConsentimientoDatosCrear peticion)
    {
        try
        {
            var consentimiento = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Consentimiento de la política '{consentimiento.VersionPolitica}' registrado.",
                id_consentimiento = consentimiento.IdConsentimiento,   // lo generó la BD
                aceptado_en = consentimiento.AceptadoEn,               // lo generó la BD
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Misma versión ya aceptada (uq_consentimiento_ver), asistente
            // inexistente (FK) o IP mal escrita (tipo INET).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al registrar el consentimiento.", ex.Message);
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
