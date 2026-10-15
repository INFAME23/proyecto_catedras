// ==========================================================================
//  Controllers/RespuestaEncuestaController.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Capa HTTP de 'RespuestaEncuesta'. NO es un CRUD (ADR D31/D32): 3
//  endpoints.
//     GET   /api/RespuestaEncuesta?fkEncuesta=ID   lista
//     GET   /api/RespuestaEncuesta/{idRespuesta}   una
//     POST  /api/RespuestaEncuesta                 RESPONDE (= cierra)
//
//  NO hay PUT, PATCH ni DELETE (RF175): esas rutas responden 404 del
//  propio ASP.NET — una respuesta de encuesta es un hecho auditable.
//  Errores de negocio de la BD (lista vacía, sin encuesta asignada, ya
//  respondido, falta una obligatoria...) llegan como NpgsqlException ->
//  500 (ADR D25, mismo bucket que cualquier otro error de PostgreSQL).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/RespuestaEncuesta")]
public class RespuestaEncuestaController : ControllerBase
{
    private readonly IServicioRespuestaEncuesta _servicio;

    public RespuestaEncuestaController(IServicioRespuestaEncuesta servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF172 · GET /api/RespuestaEncuesta?fkEncuesta=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkEncuesta, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkEncuesta, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "respuesta_encuesta",
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
            return Problema(500, "error_interno", "Error al listar las respuestas de encuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF173 · GET /api/RespuestaEncuesta/{idRespuesta}   -> 200 | 404
    //  'completa' siempre viene en true si la fila existe (D31): el SP solo
    //  deja una fila cuando el cierre tuvo éxito.
    // ----------------------------------------------------------------------
    [HttpGet("{idRespuesta:long}")]
    public async Task<IActionResult> Obtener(long idRespuesta)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idRespuesta));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Respuesta de encuesta no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la respuesta de encuesta.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF174 · POST /api/RespuestaEncuesta   -> 200 { estado, mensaje, idRespuesta } | 422 | 500
    //  Responder y cerrar son UNA sola operación (ADR D31): no existe un
    //  verbo "cerrar" separado. Forma exacta del body en
    //  version6/6_contracts.md §3 ('idRespuesta' en camelCase, tal como lo
    //  fija ese contrato — a diferencia del resto de la API, que usa
    //  snake_case en sus respuestas).
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Responder([FromBody] RespuestaEncuestaResponder peticion)
    {
        try
        {
            long idRespuesta = await _servicio.ResponderAsync(peticion);
            return Ok(new
            {
                estado = "respondida",
                mensaje = $"Encuesta respondida para el registro {peticion.FkRegistro}.",
                idRespuesta,
            });
        }
        catch (NpgsqlException ex)
        {
            // Lista vacía (R29), registro sin 'fk_encuesta' asignado,
            // registro ya respondido (uq_respuesta_registro) o falta una
            // pregunta obligatoria (RN-26, dentro de sp_cerrar_encuesta):
            // el motor revierte TODO el CALL, no queda ni el maestro.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al responder la encuesta.", ex.Message);
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
