// ==========================================================================
//  Controllers/EnlaceRegistroController.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Capa HTTP de 'EnlaceRegistro'. NO es un CRUD (ADR D24): 4 endpoints.
//     GET   /api/EnlaceRegistro?fkSesion=ID      lista
//     GET   /api/EnlaceRegistro/{idEnlace}       uno (con la ventana ya calculada)
//     POST  /api/EnlaceRegistro                  EMITE un enlace
//     PATCH /api/EnlaceRegistro/{idEnlace}/revocar   lo revoca
//
//  NO hay PUT, PATCH genérico ni DELETE (RF147): esas rutas responden
//  404/405 del propio ASP.NET.
//  Errores de negocio de la BD (sesión inexistente...) llegan como
//  NpgsqlException -> 500 (ADR D25).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/EnlaceRegistro")]
public class EnlaceRegistroController : ControllerBase
{
    private readonly IServicioEnlaceRegistro _servicio;

    public EnlaceRegistroController(IServicioEnlaceRegistro servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF143 · GET /api/EnlaceRegistro?fkSesion=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkSesion, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkSesion, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "enlace_registro",
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
            return Problema(500, "error_interno", "Error al listar los enlaces.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF144 · GET /api/EnlaceRegistro/{idEnlace:long}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idEnlace:long}")]
    public async Task<IActionResult> Obtener(long idEnlace)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idEnlace));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Enlace no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el enlace.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF145 · POST /api/EnlaceRegistro   -> 200 { estado, mensaje, id_enlace } | 422 | 500
    //  Si la sesión ya tenía un enlace vigente, la BD lo revoca (RN-14). La
    //  respuesta no lo menciona: el efecto se ve con un GET posterior.
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Emitir([FromBody] EnlaceRegistroEmitir peticion)
    {
        try
        {
            long idEnlace = await _servicio.EmitirAsync(peticion);
            return Ok(new
            {
                estado = "emitido",
                mensaje = $"Enlace emitido para la sesión {peticion.FkSesion}.",
                id_enlace = idEnlace,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Sesión inexistente (RAISE de la función), usuario inexistente (FK)
            // o ventana que se superpone con otra de la misma sesión.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al emitir el enlace.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF146 · PATCH /api/EnlaceRegistro/{idEnlace:long}/revocar
    //          -> 200 { estado, mensaje, filasEliminadas } | 400 | 404
    //  Reusa el nombre 'filasEliminadas' de los DELETE (contrato, §3.4) aunque
    //  aquí la fila sigue existiendo: no se inventa un cuarto nombre de campo.
    // ----------------------------------------------------------------------
    [HttpPatch("{idEnlace:long}/revocar")]
    public async Task<IActionResult> Revocar(long idEnlace)
    {
        try
        {
            int filas = await _servicio.RevocarAsync(idEnlace);
            return Ok(new
            {
                estado = "revocado",
                mensaje = $"Enlace {idEnlace} revocado.",
                filasEliminadas = filas,
            });
        }
        catch (ArgumentException ex)
        {
            // Ya estaba revocado.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Enlace no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al revocar el enlace.", ex.Message);
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
