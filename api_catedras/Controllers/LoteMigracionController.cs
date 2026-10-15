// ==========================================================================
//  Controllers/LoteMigracionController.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Capa HTTP de 'LoteMigracion'. 4 endpoints (RF179-RF185):
//     GET   /api/LoteMigracion?fkSesion=ID   lista
//     GET   /api/LoteMigracion/{id}          uno
//     POST  /api/LoteMigracion               GENERA el lote de una sesión
//     PATCH /api/LoteMigracion/{id}          estado / observaciones
//
//  Sin PUT ni DELETE (RF183, RF185): los datos de la generación son una
//  foto que no se reemplaza, y borrar un lote borraría la evidencia de un
//  intento de migración real. Esas rutas responden 404/405 de ASP.NET.
//  A diferencia de LoteCargaAsistente (v3), SÍ hay PATCH: el estado de un
//  lote de migración avanza con el tiempo según responde el ASIS (ADR D35).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/LoteMigracion")]
public class LoteMigracionController : ControllerBase
{
    private readonly IServicioLoteMigracion _servicio;

    public LoteMigracionController(IServicioLoteMigracion servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF179 · GET /api/LoteMigracion?fkSesion=ID&limite=N   -> 200 | 204
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
                tabla = "lote_migracion",
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
            return Problema(500, "error_interno", "Error al listar los lotes de migración.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF180 · GET /api/LoteMigracion/{idLoteMigracion}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idLoteMigracion:long}")]
    public async Task<IActionResult> Obtener(long idLoteMigracion)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idLoteMigracion));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Lote de migración no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el lote de migración.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF181 · POST /api/LoteMigracion
    //          -> 200 { estado, mensaje, id_lote_migracion, lote } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Generar([FromBody] LoteMigracionGenerar peticion)
    {
        try
        {
            var lote = await _servicio.GenerarAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Lote de migración '{lote.NombreArchivo}' generado con {lote.TotalFilas} filas.",
                // El cliente lo usa como 'fkLoteMigracion' en GET /api/DetalleMigracion.
                id_lote_migracion = lote.IdLoteMigracion,
                lote,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Sesión sin registros migrables (RN-08/RN-28) o inexistente,
            // o usuario inexistente (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al generar el lote de migración.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF182 · PATCH /api/LoteMigracion/{idLoteMigracion}   -> 200 | 400 | 404 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPatch("{idLoteMigracion:long}")]
    public async Task<IActionResult> Actualizar(long idLoteMigracion, [FromBody] LoteMigracionActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idLoteMigracion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Lote de migración no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // 'fk_estado_proceso' que no existe en el catálogo (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el lote de migración.", ex.Message);
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
