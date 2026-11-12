// ==========================================================================
//  Controllers/SolicitudPonenteController.cs  ·  Proyecto Cátedras · v7.1 · D56
// --------------------------------------------------------------------------
//  Capa HTTP de la bandeja de ponentes. 4 endpoints:
//     GET    /api/SolicitudPonente?estado=&limite=          lista (pendientes primero)
//     GET    /api/SolicitudPonente/{idSolicitud}            una
//     POST   /api/SolicitudPonente                          el ponente envía su postulación
//     PATCH  /api/SolicitudPonente/{idSolicitud}/resolver   el ADMIN aprueba o rechaza
//
//  No hay PUT ni DELETE: una solicitud no se edita ni se borra, se decide.
//  La API no autoriza (D28): quién puede llamar a 'resolver' lo decide el front
//  (solo /admin). 'resuelto_por' llega en el body para dejar la huella.
//
//  Códigos: ArgumentException -> 400 · NoEncontradoExcepcion -> 404 ·
//  PostgresException P0001 (regla de negocio de la base de datos: ya hay una
//  pendiente igual, ya estaba resuelta) -> 409 · otro NpgsqlException -> 500 (D25).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/SolicitudPonente")]
public class SolicitudPonenteController : ControllerBase
{
    private readonly IServicioSolicitudPonente _servicio;

    public SolicitudPonenteController(IServicioSolicitudPonente servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET /api/SolicitudPonente?estado=PENDIENTE&limite=N   -> 200 { tabla, limite, total, datos } | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? estado, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(estado, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "solicitud_ponente",
                limite = limite ?? 200,
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
            return Problema(500, "error_interno", "Error al listar las solicitudes de ponente.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/SolicitudPonente/{idSolicitud}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idSolicitud:long}")]
    public async Task<IActionResult> Obtener(long idSolicitud)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idSolicitud));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Solicitud no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la solicitud.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST /api/SolicitudPonente   -> 200 { estado, mensaje, id_solicitud, solicitud } | 400 | 409 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SolicitudPonenteCrear peticion)
    {
        try
        {
            var solicitud = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Solicitud {solicitud.IdSolicitud} recibida: queda pendiente de la aprobación del administrador.",
                id_solicitud = solicitud.IdSolicitud,
                solicitud,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            // Tipo de documento inexistente (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al registrar la solicitud.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/SolicitudPonente/{idSolicitud}/resolver   -> 200 { estado, mensaje, solicitud } | 400 | 404 | 409 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPatch("{idSolicitud:long}/resolver")]
    public async Task<IActionResult> Resolver(long idSolicitud, [FromBody] SolicitudPonenteResolver peticion)
    {
        try
        {
            var solicitud = await _servicio.ResolverAsync(idSolicitud, peticion);
            return Ok(new
            {
                estado = solicitud.Estado == "APROBADA" ? "aprobada" : "rechazada",
                mensaje = solicitud.Estado == "APROBADA"
                    ? $"Solicitud {idSolicitud} aprobada: {solicitud.NombreCompleto} quedó registrado como ponente (asistente {solicitud.FkAsistente})"
                      + (solicitud.FkCatedra is not null ? $" y se creó la cátedra {solicitud.FkCatedra} con su sesión." : ".")
                    : $"Solicitud {idSolicitud} rechazada.",
                solicitud,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Solicitud no encontrada.", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al resolver la solicitud.", ex.Message);
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
