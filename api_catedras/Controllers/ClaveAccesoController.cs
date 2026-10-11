// ==========================================================================
//  Controllers/ClaveAccesoController.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Capa HTTP de 'ClaveAcceso'. NO es un CRUD (ADR D24): 4 endpoints.
//     GET  /api/ClaveAcceso?fkAsistente=ID     lista (auditoría)
//     GET  /api/ClaveAcceso/{idClave}          una (auditoría)
//     POST /api/ClaveAcceso/solicitar          pide una clave de un solo uso
//     POST /api/ClaveAcceso/validar            la valida y registra la asistencia
//
//  NO hay POST plano, PUT, PATCH ni DELETE (RF152).
//
//  SEGURIDAD (RNF12): la respuesta de "solicitar" es la ÚNICA de toda la API
//  donde un secreto viaja en texto plano, a propósito: no hay servicio de
//  correo en el proyecto. Fuera de ahí la clave no se devuelve ni se registra.
//
//  ERRORES (ADR D25): clave incorrecta, usada, expirada, sin cupo, enlace
//  inválido, identificador sin asistente... son excepciones de negocio de la
//  BD -> NpgsqlException -> SIEMPRE 500 'error_motor'. El texto de la
//  excepción (campo 'detalle') dice cuál fue la causa.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/ClaveAcceso")]
public class ClaveAccesoController : ControllerBase
{
    private readonly IServicioClaveAcceso _servicio;

    public ClaveAccesoController(IServicioClaveAcceso servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF151 · GET /api/ClaveAcceso?fkAsistente=ID&limite=N   -> 200 | 204
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
                tabla = "clave_acceso",
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
            return Problema(500, "error_interno", "Error al listar las claves.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF151 · GET /api/ClaveAcceso/{idClave:long}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idClave:long}")]
    public async Task<IActionResult> Obtener(long idClave)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idClave));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Clave no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la clave.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF149 · POST /api/ClaveAcceso/solicitar
    //          -> 200 { estado, mensaje, id_clave, clave, enviado_a } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost("solicitar")]
    public async Task<IActionResult> Solicitar([FromBody] ClaveAccesoSolicitar peticion)
    {
        try
        {
            var (idClave, clave, enviadoA) = await _servicio.SolicitarAsync(peticion);
            return Ok(new
            {
                estado = "solicitada",
                mensaje = $"Clave enviada a {enviadoA}.",
                id_clave = idClave,
                clave,                  // texto plano: única vez que existe (RNF12)
                enviado_a = enviadoA,
            });
        }
        catch (ArgumentException ex)
        {
            // 'token' que no es uuid, o 'ip' que no es una IP.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Identificador sin asistente (RN-01) o enlace inexistente/revocado/
            // fuera de ventana (RN-15): excepciones de negocio de la BD (D25).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al solicitar la clave.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF150 · POST /api/ClaveAcceso/validar
    //          -> 200 { estado, mensaje, id_registro } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost("validar")]
    public async Task<IActionResult> Validar([FromBody] ClaveAccesoValidar peticion)
    {
        try
        {
            long idRegistro = await _servicio.ValidarAsync(peticion);
            return Ok(new
            {
                estado = "registrado",
                mensaje = "Asistencia registrada.",
                id_registro = idRegistro,
            });
        }
        catch (ArgumentException ex)
        {
            // 'ip' que no es una IP.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Clave incorrecta, ya usada (RN-20), expirada (RN-18), sesión sin
            // cupo (RN-38) o asistente sin vinculación vigente (RN-07/22).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al validar la clave.", ex.Message);
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
