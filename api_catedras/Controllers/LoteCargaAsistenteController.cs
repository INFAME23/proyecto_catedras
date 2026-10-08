// ==========================================================================
//  Controllers/LoteCargaAsistenteController.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Capa HTTP de 'LoteCargaAsistente'. 4 endpoints (RF114-RF117):
//     GET  /api/LoteCargaAsistente               lista
//     GET  /api/LoteCargaAsistente/{id}          uno
//     POST /api/LoteCargaAsistente               abre un lote
//     POST /api/LoteCargaAsistente/{id}/cerrar   cierra (contadores + novedades)
//
//  Sin PUT, PATCH ni DELETE: el lote es la bitácora de una ejecución ya
//  ocurrida. "Cerrar" es POST (no PATCH) porque además de actualizar el
//  lote CREA sus novedades: es una acción, no una edición parcial.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/LoteCargaAsistente")]
public class LoteCargaAsistenteController : ControllerBase
{
    private readonly IServicioLoteCargaAsistente _servicio;

    public LoteCargaAsistenteController(IServicioLoteCargaAsistente servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF114 · GET /api/LoteCargaAsistente?limite=N   -> 200 | 204
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
                tabla = "lote_carga_asistente",
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
            return Problema(500, "error_interno", "Error al listar los lotes de carga.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF114 · GET /api/LoteCargaAsistente/{idLoteCarga}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idLoteCarga:long}")]
    public async Task<IActionResult> Obtener(long idLoteCarga)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idLoteCarga));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Lote de carga no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el lote de carga.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF115 · POST /api/LoteCargaAsistente
    //          -> 200 { estado, mensaje, id_lote_carga, cargado_en } | 422
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] LoteCargaAsistenteCrear peticion)
    {
        try
        {
            var lote = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Lote de carga '{lote.NombreArchivo}' abierto.",
                // El cliente lo usa como 'fk_lote_carga' en cada POST /api/Asistente.
                id_lote_carga = lote.IdLoteCarga,
                cargado_en = lote.CargadoEn,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al abrir el lote de carga.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF116 · POST /api/LoteCargaAsistente/{idLoteCarga}/cerrar
    //          -> 200 { estado, mensaje, lote } | 400 | 404 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost("{idLoteCarga:long}/cerrar")]
    public async Task<IActionResult> Cerrar(long idLoteCarga, [FromBody] LoteCargaAsistenteCerrar peticion)
    {
        try
        {
            var lote = await _servicio.CerrarAsync(idLoteCarga, peticion);
            return Ok(new
            {
                estado = "cerrado",
                mensaje = $"Lote '{lote.IdLoteCarga}' cerrado: {lote.FilasAceptadas} aceptadas, " +
                          $"{lote.FilasRechazadas} rechazadas de {lote.FilasLeidas} leídas.",
                lote,
            });
        }
        catch (ArgumentException ex)
        {
            // Contadores que no cuadran, novedades que faltan o lote ya cerrado.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Lote de carga no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cerrar el lote de carga.", ex.Message);
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
