// ==========================================================================
//  Controllers/AliasProgramaController.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Capa HTTP de 'AliasPrograma': GET lista, GET/{id}, POST, PUT, PATCH y
//  DELETE. PUT y PATCH se agregaron en el RETROFIT criterio 3 (la v2, ADR D12,
//  lo trataba como solo-anexo) para que sp_actualizar_alias_programa tenga
//  llamador.
//
//  El id es 'long' (lo genera la BD); la restricción de ruta '{idAlias:long}'
//  hace que "/api/AliasPrograma/abc" ni siquiera entre a este controlador.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/AliasPrograma")]
public class AliasProgramaController : ControllerBase
{
    private readonly IServicioAliasPrograma _servicio;

    public AliasProgramaController(IServicioAliasPrograma servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF80 · GET /api/AliasPrograma?limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "alias_programa",
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
            return Problema(500, "error_interno", "Error al listar los alias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF81 · GET /api/AliasPrograma/{idAlias}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idAlias:long}")]
    public async Task<IActionResult> Obtener(long idAlias)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idAlias));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Alias no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el alias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF82 · POST /api/AliasPrograma   -> 200 { estado, mensaje, id_alias, creado_en }
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] AliasProgramaCrear peticion)
    {
        try
        {
            var alias = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Alias '{alias.TextoNormalizado}' creado.",
                id_alias = alias.IdAlias,     // lo generó la BD
                creado_en = alias.CreadoEn,   // lo generó la BD
            });
        }
        catch (NpgsqlException ex)
        {
            // Texto duplicado (uq_alias_texto) o programa inexistente (fk_alias_programa).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el alias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PUT /api/AliasPrograma/{idAlias}   -> 200 { filasAfectadas } | 422 | 404 | 500
    // ----------------------------------------------------------------------
    [HttpPut("{idAlias:long}")]
    public async Task<IActionResult> Reemplazar(long idAlias, [FromBody] AliasProgramaReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idAlias, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Alias no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Texto duplicado (uq_alias_texto) o programa inexistente (fk_alias_programa).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar el alias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  PATCH /api/AliasPrograma/{idAlias}   -> 200 { filasAfectadas } | 400 | 404 | 500
    // ----------------------------------------------------------------------
    [HttpPatch("{idAlias:long}")]
    public async Task<IActionResult> Actualizar(long idAlias, [FromBody] AliasProgramaActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idAlias, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Alias no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el alias.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF83 · DELETE /api/AliasPrograma/{idAlias}   -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idAlias:long}")]
    public async Task<IActionResult> Eliminar(long idAlias)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idAlias);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Alias no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el alias.", ex.Message);
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
