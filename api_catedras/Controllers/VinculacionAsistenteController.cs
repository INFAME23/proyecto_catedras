// ==========================================================================
//  Controllers/VinculacionAsistenteController.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Capa HTTP de 'VinculacionAsistente'. SOLO 3 endpoints (ADR D16):
//     GET   /api/VinculacionAsistente?fkAsistente=ID   lista
//     POST  /api/VinculacionAsistente                  abre
//     PATCH /api/VinculacionAsistente/cerrar           cierra (llave en el body)
//
//  NO hay [HttpGet("{id}")], [HttpPut] ni [HttpDelete]: la tabla no tiene
//  columna 'id' y la historia no se reemplaza ni se borra. Una llamada a
//  esas rutas responde 404/405 del propio ASP.NET (la ruta no existe).
//  Primera asimetría de rutas del proyecto: es a propósito, no un olvido.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/VinculacionAsistente")]
public class VinculacionAsistenteController : ControllerBase
{
    private readonly IServicioVinculacionAsistente _servicio;

    public VinculacionAsistenteController(IServicioVinculacionAsistente servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF107 · GET /api/VinculacionAsistente?fkAsistente=ID&limite=N   -> 200 | 204
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
                tabla = "vinculacion_asistente",
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
            return Problema(500, "error_interno", "Error al listar las vinculaciones.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF108 · POST /api/VinculacionAsistente   -> 200 { estado, mensaje, vinculacion } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] VinculacionAsistenteCrear peticion)
    {
        try
        {
            var vinculacion = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Vinculación '{vinculacion.FkTipoVinculacion}' abierta.",
                // Se devuelve la fila completa: el cliente necesita su llave
                // (con la fecha_ini efectiva) para poder cerrarla después.
                vinculacion,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Llave repetida (PK) o asistente/tipo inexistente (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al abrir la vinculación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF109 · PATCH /api/VinculacionAsistente/cerrar   -> 200 | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPatch("cerrar")]
    public async Task<IActionResult> Cerrar([FromBody] VinculacionAsistenteCerrar peticion)
    {
        try
        {
            var vinculacion = await _servicio.CerrarAsync(peticion);
            return Ok(new
            {
                estado = "cerrado",
                mensaje = $"Vinculación '{vinculacion.FkTipoVinculacion}' cerrada.",
                vinculacion,
            });
        }
        catch (ArgumentException ex)
        {
            // Ya estaba cerrada, o fecha_fin anterior a fecha_ini.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Vinculación no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cerrar la vinculación.", ex.Message);
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
