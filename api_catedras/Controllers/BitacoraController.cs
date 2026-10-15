// ==========================================================================
//  Controllers/BitacoraController.cs  ·  Proyecto Cátedras · Versión 7
// --------------------------------------------------------------------------
//  Capa HTTP de 'Bitacora' — SOLO LECTURA (RF190-RF191, ADR D36 y D37-A):
//
//    GET    /api/Bitacora?nombreTabla=T&limite=N   -> 200 | 204 | 400
//    GET    /api/Bitacora/{idBitacora}             -> 200 | 404
//
//  ¿Por qué no hay POST / PUT / PATCH / DELETE? La bitácora es el LOG DE
//  AUDITORÍA (RN-35): sus filas las escribe la propia BD, con la función
//  'fn_trg_bitacora' de los triggers 'trg_bitacora_registro' (sobre
//  registro_asistencia) y 'trg_bitacora_asistente' (sobre asistente). Si la
//  API dejara crear, editar o borrar entradas, cualquiera podría maquillar
//  la auditoría y la tabla dejaría de servir para lo que existe.
//
//  Los procedimientos sp_crear_/sp_actualizar_/sp_eliminar_bitacora siguen
//  en la BD (migración 014, criterio 3 del retrofit) pero NO tienen
//  llamador — mismo criterio que los SP no expuestos de RespuestaEncuesta.
//
//  Como la ruta 'api/Bitacora' existe (con GET), ASP.NET Core responde
//  405 Method Not Allowed a cualquier otro verbo: no hace falta escribir
//  nada para "rechazarlos".
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;   // NoEncontradoExcepcion -> 404
using ApiCatedras.Servicios;     // IServicioBitacora (la interfaz, no la clase)
using Microsoft.AspNetCore.Mvc;  // ControllerBase, [HttpGet], IActionResult...

[ApiController]               // valida el modelo y lee [FromQuery] automáticamente
[Route("api/Bitacora")]       // prefijo de las dos rutas de abajo
public class BitacoraController : ControllerBase
{
    // El controlador solo conoce la INTERFAZ del servicio (Artículo 3):
    // la clase concreta la decide Program.cs.
    private readonly IServicioBitacora _servicio;

    public BitacoraController(IServicioBitacora servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF190 · GET /api/Bitacora?nombreTabla=T&limite=N
    //          -> 200 { tabla, limite, total, datos } | 204 | 400
    //  Sin filtro por usuario (D37-C): 'fk_usuario' es NULL en todas las
    //  filas porque ningún endpoint propaga el usuario a PostgreSQL.
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? nombreTabla, [FromQuery] int? limite)
    {
        try
        {
            // El servicio valida el límite y devuelve las filas y cuántas son.
            var (datos, total) = await _servicio.ListarAsync(nombreTabla, limite);

            if (total == 0)
                return NoContent();   // 204: la consulta fue válida, pero vacía

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
            // 'limite' <= 0 (regla del servicio).
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al listar la bitácora.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF191 · GET /api/Bitacora/{idBitacora}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idBitacora:long}")]   // ':long' -> un id no numérico ni siquiera entra aquí
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

    // Arma el sobre de error del proyecto: { estado, mensaje, detalle }.
    private ObjectResult Problema(int codigo, string estado, string mensaje, string? detalle)
    {
        return new ObjectResult(new { estado, mensaje, detalle })
        {
            StatusCode = codigo,
        };
    }
}
