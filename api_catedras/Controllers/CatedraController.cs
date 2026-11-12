// ==========================================================================
//  Controllers/CatedraController.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Capa HTTP de 'Catedra'. Mismo patrón que SedeController (v1), con tres
//  diferencias:
//   · el id es 'long' (lo genera la BD) -> restricción de ruta ':long';
//   · el GET de lista sin '?limite=' devuelve TODAS las cátedras y 'total'
//     es el total real de la tabla (contrato del catálogo público);
//   · el POST responde también 'id_catedra' y 'nombre_asis' calculados por
//     la BD en el mismo INSERT (ADR D11).
//  Errores: ArgumentException -> 400 · NoEncontradoExcepcion -> 404 ·
//  NpgsqlException -> 500 (ASIS duplicado, tipo de evento o dependencia
//  inexistentes).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

// [ApiController]: enlaza el body y valida la petición (422 automático).
// [Route]: prefijo común de todos los métodos.
[ApiController]
[Route("api/Catedra")]
public class CatedraController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioCatedra _servicio;

    public CatedraController(IServicioCatedra servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF84 · GET /api/Catedra?limite=N   -> 200 { tabla, limite, total, datos } | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? limite, [FromQuery] bool incluirInactivos = false)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(limite, incluirInactivos);

            // Sin filas devueltas -> 204 (aquí 'total' es el de la tabla, por
            // eso se mira la lista).
            if (datos.Count == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "catedra",
                limite = limite ?? total,   // sin límite = se devolvieron todas
                total,                      // total REAL de la tabla
                datos,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al listar las cátedras.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF85 · GET /api/Catedra/{idCatedra:long}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idCatedra:long}")]
    public async Task<IActionResult> Obtener(long idCatedra)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idCatedra));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Cátedra no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la cátedra.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF86 · POST /api/Catedra   -> 200 { estado, mensaje } | 400 | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CatedraCrear peticion)
    {
        try
        {
            // Deconstrucción: los dos valores que puso la BD.
            var (idCatedra, nombreAsis) = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Cátedra '{peticion.IdEventoAsis}' creada"
                        + ((peticion.Sesiones?.Count ?? 0) > 0 ? $" con {peticion.Sesiones!.Count} sesiones." : "."),
                id_catedra = idCatedra,
                nombre_asis = nombreAsis,
                // Maestro-detalle (Regla 1): cuántas sesiones nacieron con ella.
                sesiones_creadas = peticion.Sesiones?.Count ?? 0,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            // Regla de negocio de la base de datos (v7.1 D57): el horario está fuera de los periodos en que se pueden programar cátedras.
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            // ASIS duplicado (uq_catedra_asis) o tipo de evento / dependencia
            // inexistentes (fk_catedra_tipo_evento / fk_catedra_dependencia).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la cátedra.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF87 · PUT /api/Catedra/{idCatedra:long}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idCatedra:long}")]
    public async Task<IActionResult> Reemplazar(long idCatedra, [FromBody] CatedraReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idCatedra, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Cátedra no encontrada.", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            // Regla de negocio de la base de datos (v7.1 D57): el horario está fuera de los periodos en que se pueden programar cátedras.
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la cátedra.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF88 · PATCH /api/Catedra/{idCatedra:long}   -> 200 { filasAfectadas } | 400 | 404
    // ----------------------------------------------------------------------
    [HttpPatch("{idCatedra:long}")]
    public async Task<IActionResult> Actualizar(long idCatedra, [FromBody] CatedraActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idCatedra, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Cátedra no encontrada.", ex.Message);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            // Regla de negocio de la base de datos (v7.1 D57): el horario está fuera de los periodos en que se pueden programar cátedras.
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la cátedra.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF89 · DELETE /api/Catedra/{idCatedra:long}   -> 200 { filasEliminadas } | 404
    //  ⚠ 'sesion.fk_catedra' es ON DELETE CASCADE: borrar una cátedra borra
    //  también sus sesiones (v4, ya sembradas). Para pruebas, usar una cátedra
    //  sin sesiones (p. ej. 5502) o una creada por la propia prueba.
    // ----------------------------------------------------------------------
    [HttpDelete("{idCatedra:long}")]
    public async Task<IActionResult> Eliminar(long idCatedra)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idCatedra);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Cátedra no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar la cátedra.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la cátedra.", ex.Message);
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
