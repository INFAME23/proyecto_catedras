// ==========================================================================
//  Controllers/SesionController.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Capa HTTP de 'Sesion'. Molde completo de 6 endpoints (como Catedra, v2),
//  con dos diferencias:
//   · el GET de lista acepta '?fkCatedra=' para pedir el calendario de UNA
//     cátedra sin descargar las 5714 sesiones de ejemplo (RF119);
//   · el POST responde también 'numero_reunion' y 'creado_en' tal como los
//     decidió la BD en el mismo INSERT (ADR D20).
//  Errores: ArgumentException -> 400 (incluido "fin <= inicio", RF125) ·
//  NoEncontradoExcepcion -> 404 · NpgsqlException -> 500 (FK inexistente,
//  número de reunión repetido, o un DELETE bloqueado por RESTRICT).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/Sesion")]
public class SesionController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioSesion _servicio;

    public SesionController(IServicioSesion servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF119 · GET /api/Sesion?fkCatedra=ID&limite=N   -> 200 { tabla, limite, total, datos } | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkCatedra, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkCatedra, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "sesion",
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
            return Problema(500, "error_interno", "Error al listar las sesiones.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF120 · GET /api/Sesion/{idSesion:long}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idSesion:long}")]
    public async Task<IActionResult> Obtener(long idSesion)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idSesion));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sesión no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la sesión.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF121 · POST /api/Sesion   -> 200 { estado, mensaje, id_sesion, numero_reunion, creado_en }
    //                                | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SesionCrear peticion)
    {
        try
        {
            // Deconstrucción: los tres valores que puso la BD.
            var (idSesion, numeroReunion, creadoEn) = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Sesión {numeroReunion} de la cátedra {peticion.FkCatedra} creada.",
                id_sesion = idSesion,
                numero_reunion = numeroReunion,   // calculado por el trigger si no se envió (ADR D20)
                creado_en = creadoEn,
            });
        }
        catch (ArgumentException ex)
        {
            // RF125: 'fin' no es posterior a 'inicio'. La BD no se tocó.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // FK inexistente (cátedra, modalidad, sede, periodo, encuesta) o
            // un 'numero_reunion' explícito ya usado en esa cátedra (uq_sesion_reunion).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la sesión.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF122 · PUT /api/Sesion/{idSesion:long}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idSesion:long}")]
    public async Task<IActionResult> Reemplazar(long idSesion, [FromBody] SesionReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idSesion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sesión no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la sesión.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF123 · PATCH /api/Sesion/{idSesion:long}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idSesion:long}")]
    public async Task<IActionResult> Actualizar(long idSesion, [FromBody] SesionActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idSesion, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sesión no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la sesión.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF124 · DELETE /api/Sesion/{idSesion:long}   -> 200 { filasEliminadas } | 404 | 500
    //  ⚠ 'ponencia' y 'enlace_registro' (v5) se borran en CASCADA, sin aviso.
    //  ⚠ 'registro_asistencia' (v5) y 'lote_migracion' (v7) son RESTRICT: si
    //    la sesión tiene alguna de esas filas -> 500 y NO se borra nada.
    //    Para pruebas: id_sesion 1-5 (sin filas de v5/v7) o una sesión creada
    //    por la propia prueba. La 5712 es la que SÍ está bloqueada.
    // ----------------------------------------------------------------------
    [HttpDelete("{idSesion:long}")]
    public async Task<IActionResult> Eliminar(long idSesion)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idSesion);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sesión no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la sesión.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la sesión.", ex.Message);
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
