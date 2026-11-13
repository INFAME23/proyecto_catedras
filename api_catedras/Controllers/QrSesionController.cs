// ==========================================================================
//  Controllers/QrSesionController.cs  ·  Cátedras · Subversión v7.1 · D53
// --------------------------------------------------------------------------
//  El QR de cierre de una sesión y su lista de asistencia.
//
//    POST /api/QrSesion            { fk_sesion, fk_usuario?, regenerar? }  -> 200 | 404 | 422
//    GET  /api/QrSesion?fkSesion=ID                                        -> 200 | 204 | 400
//    GET  /api/QrSesion/asistencia?fkSesion=ID                             -> 200 { total, datos } | 400
//
//  El QR es el enlace de registro de la v5 con una ventana que cierra cuando
//  termina la sesión (v7.1 · D55: ya no exige inscripción). Esta
//  API no autoriza (ADR D28): que solo el ponente de la sesión (o un
//  administrador) vea el QR lo garantiza el front.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/QrSesion")]
public class QrSesionController : ControllerBase
{
    private readonly IServicioQrSesion _servicio;

    public QrSesionController(IServicioQrSesion servicio)
    {
        _servicio = servicio;
    }

    [HttpPost]
    public async Task<IActionResult> Emitir([FromBody] QrSesionEmitir peticion)
    {
        try
        {
            return Ok(await _servicio.EmitirAsync(peticion));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sesión no encontrada.", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            // v7.2 D72: regla de negocio de la base de datos. Una sesión presencial cuya sede no tiene
            // coordenadas (o es virtual) no puede generar QR; el mensaje dice qué sede completar.
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al emitir el QR.", ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] long? fkSesion)
    {
        try
        {
            var qr = await _servicio.ConsultarAsync(fkSesion);
            return qr is null ? NoContent() : Ok(qr);
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar el QR.", ex.Message);
        }
    }

    [HttpGet("asistencia")]
    public async Task<IActionResult> Asistencia([FromQuery] long? fkSesion)
    {
        try
        {
            var datos = await _servicio.AsistenciaAsync(fkSesion);
            return Ok(new { total = datos.Count, datos });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar la asistencia.", ex.Message);
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
