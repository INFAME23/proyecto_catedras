// ==========================================================================
//  Controllers/BitacoraController.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Capa HTTP de 'Bitacora':
//
//    GET    /api/Bitacora?nombreTabla=T&limite=N   -> 200 | 204 | 400
//    GET    /api/Bitacora/{idBitacora}             -> 200 | 404
//    POST   /api/Bitacora                          -> 200 { estado, mensaje, bitacora } | 422 | 500
//    PUT    /api/Bitacora/{idBitacora}             -> 200 { filasAfectadas } | 422 | 404 | 500
//    PATCH  /api/Bitacora/{idBitacora}             -> 200 { filasAfectadas } | 400 | 404 | 500
//    DELETE /api/Bitacora/{idBitacora}             -> 200 { filasEliminadas } | 404
//
//  ⚠️ La bitácora es el LOG DE AUDITORÍA (RN-35): sus filas las escriben los
//  triggers (trg_bitacora_asistente, trg_bitacora_registro). POST / PUT /
//  PATCH / DELETE existen a pedido del equipo para que los cinco
//  procedimientos tengan llamador; dejarlos abiertos permite reescribir o
//  borrar la auditoría. 'ocurrido_en' y 'usuario_bd' nunca los acepta el
//  cliente. Conviene protegerlos con autorización antes de producción.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Bitacora")]
public class BitacoraController : ControllerBase
{
    private readonly IServicioBitacora _servicio;

    public BitacoraController(IServicioBitacora servicio)
    {
        _servicio = servicio;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? nombreTabla, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(nombreTabla, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "bitacora",
                limite = limite ?? 1000,   // el efectivo usado
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
            return Problema(500, "error_interno", "Error al listar la bitácora.", ex.Message);
        }
    }

    [HttpGet("{idBitacora:long}")]
    public async Task<IActionResult> Obtener(long idBitacora)
    {
        try
        {
            var entrada = await _servicio.ObtenerAsync(idBitacora);
            return Ok(entrada);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Entrada de bitácora no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la entrada de bitácora.", ex.Message);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] BitacoraCrear peticion)
    {
        try
        {
            var entrada = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Entrada {entrada.IdBitacora} registrada ({entrada.Operacion} en '{entrada.NombreTabla}').",
                bitacora = entrada,   // con id, ocurrido_en y usuario_bd que puso la BD
            });
        }
        catch (NpgsqlException ex)
        {
            // Operación inválida (chk_bitacora_operacion) o usuario inexistente (fk_bitacora_usuario).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al registrar la entrada de bitácora.", ex.Message);
        }
    }

    [HttpPut("{idBitacora:long}")]
    public async Task<IActionResult> Reemplazar(long idBitacora, [FromBody] BitacoraReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idBitacora, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Entrada de bitácora no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la entrada de bitácora.", ex.Message);
        }
    }

    [HttpPatch("{idBitacora:long}")]
    public async Task<IActionResult> Actualizar(long idBitacora, [FromBody] BitacoraActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idBitacora, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Entrada de bitácora no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la entrada de bitácora.", ex.Message);
        }
    }

    [HttpDelete("{idBitacora:long}")]
    public async Task<IActionResult> Eliminar(long idBitacora)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idBitacora);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Entrada de bitácora no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la entrada de bitácora.", ex.Message);
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
