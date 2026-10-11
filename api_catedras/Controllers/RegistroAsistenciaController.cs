// ==========================================================================
//  Controllers/RegistroAsistenciaController.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Capa HTTP de 'RegistroAsistencia'. SOLO 3 endpoints:
//     GET  /api/RegistroAsistencia?fkSesion=ID&fkAsistente=ID   lista
//     GET  /api/RegistroAsistencia/{idRegistro}                 uno
//     POST /api/RegistroAsistencia                              alta MANUAL
//
//  NO hay PUT, PATCH ni DELETE (RF157): un registro de asistencia es un hecho
//  auditable; no se edita ni se borra. La vía normal de creación es
//  ClaveAcceso/validar, nunca este POST.
//
//  Errores: ArgumentException -> 400 (origen distinto de MANUAL/IMPORTADO, D26)
//  · NoEncontradoExcepcion -> 404 · NpgsqlException -> 500 (asistente sin
//  vinculación vigente, registro repetido, sin cupo, FK inexistente).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/RegistroAsistencia")]
public class RegistroAsistenciaController : ControllerBase
{
    private readonly IServicioRegistroAsistencia _servicio;

    public RegistroAsistenciaController(IServicioRegistroAsistencia servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF154 · GET /api/RegistroAsistencia?fkSesion=&fkAsistente=&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] long? fkSesion, [FromQuery] long? fkAsistente, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkSesion, fkAsistente, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "registro_asistencia",
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
            return Problema(500, "error_interno", "Error al listar los registros.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF155 · GET /api/RegistroAsistencia/{idRegistro:long}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idRegistro:long}")]
    public async Task<IActionResult> Obtener(long idRegistro)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idRegistro));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Registro no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el registro.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF156 · POST /api/RegistroAsistencia
    //          -> 200 { estado, mensaje, id_registro, registrado_en,
    //                   fk_vinculacion_snapshot, fk_programa_snapshot } | 400 | 422 | 500
    //  Devuelve lo que decidió la BD (hora e instantáneas del trigger).
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RegistroAsistenciaCrear peticion)
    {
        try
        {
            var registro = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Asistencia {registro.Origen} registrada en la sesión {registro.FkSesion}.",
                id_registro = registro.IdRegistro,
                registrado_en = registro.RegistradoEn,
                fk_vinculacion_snapshot = registro.FkVinculacionSnapshot,
                fk_programa_snapshot = registro.FkProgramaSnapshot,
            });
        }
        catch (ArgumentException ex)
        {
            // D26: 'origen' distinto de MANUAL o IMPORTADO. La BD no se tocó.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Asistente sin vinculación vigente (RN-07), sesión sin cupo (RN-38),
            // ya registrado en esa sesión (uq_registro_sesion_asis) o FK inexistente.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el registro.", ex.Message);
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
