// ==========================================================================
//  Controllers/PonenciaController.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Capa HTTP de 'Ponencia'. SOLO 4 endpoints (ADR D19):
//     GET    /api/Ponencia?fkSesion=&fkAsistente=           lista
//     GET    /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}   una, por la llave completa
//     POST   /api/Ponencia                                  asigna
//     PUT    /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}   cambia el rol (RETROFIT criterio 3)
//     DELETE /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}   desasigna
//
//  LA LLAVE COMPUESTA VA EN LA RUTA, en 3 segmentos: la tabla no tiene una
//  sola columna que identifique la fila. A diferencia de
//  VinculacionAsistente (v3), que manda la llave en el BODY de un
//  'PATCH /cerrar', aquí los verbos GET y DELETE ya identifican el recurso
//  por la URL: no hace falta inventar una ruta de acción.
//
//  Hay [HttpPut] solo para el ROL (RETROFIT criterio 3: sp_actualizar_ponencia
//  debía tener llamador); NO hay [HttpPatch] ni rutas
//  de un solo segmento: 'GET /api/Ponencia/3' responde 404 del propio
//  ASP.NET porque esa ruta no existe. Es a propósito, no un olvido.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Ponencia")]
public class PonenciaController : ControllerBase
{
    private readonly IServicioPonencia _servicio;

    public PonenciaController(IServicioPonencia servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF127 · GET /api/Ponencia?fkSesion=ID&fkAsistente=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkSesion, [FromQuery] long? fkAsistente,
                                            [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkSesion, fkAsistente, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "ponencia",
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
            return Problema(500, "error_interno", "Error al listar las ponencias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF128 · GET /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}   -> 200 | 404
    //  ':long' en los dos primeros segmentos: "/api/Ponencia/abc/1/PONENTE"
    //  ni siquiera entra a este método (404 de ASP.NET).
    // ----------------------------------------------------------------------
    [HttpGet("{fkSesion:long}/{fkAsistente:long}/{rol}")]
    public async Task<IActionResult> Obtener(long fkSesion, long fkAsistente, string rol)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(fkSesion, fkAsistente, rol));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Participación no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la participación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF129 · POST /api/Ponencia   -> 200 { estado, mensaje, ponencia } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] PonenciaCrear peticion)
    {
        try
        {
            var ponencia = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Asistente {ponencia.FkAsistente} asignado como {ponencia.Rol} a la sesión {ponencia.FkSesion}.",
                // La fila completa: es la llave que el cliente necesita para
                // consultarla o quitarla después (con el rol efectivo).
                ponencia,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Misma llave exacta (pk_ponencia) o sesión/asistente inexistentes (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al asignar la participación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}   -> 200 { estado, mensaje, ponencia } | 400/422 | 404 | 500
    //  Cambia el ROL (lo único editable: las otras dos columnas son la llave).
    //  Pasa por sp_actualizar_ponencia.
    // ----------------------------------------------------------------------
    [HttpPut("{fkSesion:long}/{fkAsistente:long}/{rol}")]
    public async Task<IActionResult> Reemplazar(long fkSesion, long fkAsistente, string rol,
                                                [FromBody] PonenciaReemplazo peticion)
    {
        try
        {
            var ponencia = await _servicio.ReemplazarAsync(fkSesion, fkAsistente, rol, peticion);
            return Ok(new
            {
                estado = "actualizado",
                mensaje = $"Asistente {fkAsistente} ahora es {ponencia.Rol} en la sesión {fkSesion}.",
                ponencia,
            });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Participación no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Rol nuevo ya ocupado por el mismo asistente en la sesión (pk_ponencia).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cambiar el rol.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF130 · DELETE /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}   -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{fkSesion:long}/{fkAsistente:long}/{rol}")]
    public async Task<IActionResult> Eliminar(long fkSesion, long fkAsistente, string rol)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(fkSesion, fkAsistente, rol);
            return Ok(new
            {
                estado = "eliminado",
                mensaje = $"Asistente {fkAsistente} retirado del rol {rol} en la sesión {fkSesion}.",
                filasEliminadas = filas,
            });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Participación no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al quitar la participación.", ex.Message);
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
