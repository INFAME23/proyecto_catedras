// ==========================================================================
//  Controllers/AsistenteController.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Capa HTTP de 'Asistente'. Molde completo de 6 endpoints, con dos
//  novedades:
//   · GET lista acepta '?q=' (búsqueda en SQL) y '?pagina=' (ADR D18): con
//     14.811 filas, el front nunca descarga la tabla completa.
//   · POST acepta detalles iniciales (Guía IA, Regla 1) y responde con el
//     'id_asistente' que generó la BD.
//
//     ArgumentException     -> 400  (id_asis/es_externo, sin correo, límite)
//     NoEncontradoExcepcion -> 404
//     NpgsqlException        -> 500  (duplicado, FK, CHECK, RESTRICT)
//
//  '{idAsistente:long}' = restricción de ruta: "/api/Asistente/abc" ni
//  siquiera entra a este controlador (404 del propio ASP.NET).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Asistente")]
public class AsistenteController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioAsistente _servicio;

    public AsistenteController(IServicioAsistente servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF95 · GET /api/Asistente?q=texto&limite=N&pagina=P
    //         -> 200 { tabla, limite, pagina, total, datos } | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? q, [FromQuery] int? limite, [FromQuery] int? pagina)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(q, limite, pagina);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "asistente",
                limite = limite ?? 1000,
                pagina = pagina ?? 1,
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
            return Problema(500, "error_interno", "Error al listar los asistentes.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF96 · GET /api/Asistente/{idAsistente}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idAsistente:long}")]
    public async Task<IActionResult> Obtener(long idAsistente)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idAsistente));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Asistente no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el asistente.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF97 · POST /api/Asistente   -> 200 { estado, mensaje, id_asistente } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] AsistenteCrear peticion)
    {
        try
        {
            var asistente = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Asistente '{asistente.Nombres} {asistente.Apellidos}' creado.",
                id_asistente = asistente.IdAsistente,   // lo generó la BD
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // id_asis o correo duplicado, FK inexistente en un detalle,
            // segundo documento vigente... El SP revirtió TODO el alta.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el asistente.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF98 · PUT /api/Asistente/{idAsistente}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idAsistente:long}")]
    public async Task<IActionResult> Reemplazar(long idAsistente, [FromBody] AsistenteReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idAsistente, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Asistente no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar el asistente.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF99 · PATCH /api/Asistente/{idAsistente}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idAsistente:long}")]
    public async Task<IActionResult> Actualizar(long idAsistente, [FromBody] AsistenteActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idAsistente, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Asistente no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el asistente.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF100 · DELETE /api/Asistente/{idAsistente}   -> 200 { filasEliminadas } | 404 | 500
    //  Sus 4 detalles se borran con él (CASCADE). Si tiene usuario, ponencia
    //  o registro de asistencia (RESTRICT) -> 500 y NADA se borra.
    // ----------------------------------------------------------------------
    [HttpDelete("{idAsistente:long}")]
    public async Task<IActionResult> Eliminar(long idAsistente)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idAsistente);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Asistente no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // [ACID · A] La sentencia completa se revierte: los detalles siguen ahí.
            return Problema(500, "error_motor", "No se pudo eliminar el asistente.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el asistente.", ex.Message);
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
