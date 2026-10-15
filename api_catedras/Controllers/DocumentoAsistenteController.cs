// ==========================================================================
//  Controllers/DocumentoAsistenteController.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Capa HTTP de 'DocumentoAsistente'. Molde completo de 6 endpoints. La
//  lista acepta '?fkAsistente=' para ver los documentos de una persona.
//
//     ArgumentException     -> 400  (límite inválido, PATCH vacío)
//     NoEncontradoExcepcion -> 404
//     NpgsqlException        -> 500  (segundo vigente: ux_documento_vigente;
//                                      número repetido: uq_documento_numero;
//                                      asistente o tipo inexistente: FK)
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/DocumentoAsistente")]
public class DocumentoAsistenteController : ControllerBase
{
    private readonly IServicioDocumentoAsistente _servicio;

    public DocumentoAsistenteController(IServicioDocumentoAsistente servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF102 · GET /api/DocumentoAsistente?fkAsistente=ID&limite=N   -> 200 | 204
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
                tabla = "documento_asistente",
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
            return Problema(500, "error_interno", "Error al listar los documentos.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET /api/DocumentoAsistente/{idDocumento}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idDocumento:long}")]
    public async Task<IActionResult> Obtener(long idDocumento)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idDocumento));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Documento no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF103 · POST /api/DocumentoAsistente
    //          -> 200 { estado, mensaje, id_documento, registrado_en } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] DocumentoAsistenteCrear peticion)
    {
        try
        {
            var documento = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Documento '{documento.FkTipoDocumento} {documento.Numero}' creado.",
                id_documento = documento.IdDocumento,       // lo generó la BD
                registrado_en = documento.RegistradoEn,     // lo generó la BD
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
            return Problema(500, "error_interno", "Error al crear el documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF104 · PUT /api/DocumentoAsistente/{idDocumento}   -> 200 { filasAfectadas } | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idDocumento:long}")]
    public async Task<IActionResult> Reemplazar(long idDocumento, [FromBody] DocumentoAsistenteReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idDocumento, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Documento no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar el documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF104 · PATCH /api/DocumentoAsistente/{idDocumento}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idDocumento:long}")]
    public async Task<IActionResult> Actualizar(long idDocumento, [FromBody] DocumentoAsistenteActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idDocumento, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Documento no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el documento.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF105 · DELETE /api/DocumentoAsistente/{idDocumento}   -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idDocumento:long}")]
    public async Task<IActionResult> Eliminar(long idDocumento)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idDocumento);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Documento no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el documento.", ex.Message);
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
