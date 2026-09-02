// ==========================================================================
//  Controllers/SedeController.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  La capa HTTP. Su ÚNICO trabajo:
//   1. recibir la petición (los atributos [HttpGet]/[HttpPost]... enrutan),
//   2. llamar al servicio (por la INTERFAZ IServicioSede),
//   3. traducir el resultado o la excepción a un código HTTP y al sobre
//      de respuesta que fija el Artículo 8.
//
//  El controlador NO toca SQL y NO tiene reglas de negocio. Cada método
//  lleva su propio try/catch plano (Artículo 3, sin indirecciones).
//
//  Sobres (Artículo 8):
//    · Lecturas:  { tabla, limite, total, datos }
//    · Escrituras: { estado, mensaje } | { filasAfectadas } | { filasEliminadas }
//    · Errores:   { estado, mensaje, detalle }   (+ { errores:[...] } en 422)
//
//  Traducción de excepciones:
//    ArgumentException     -> 400
//    NoEncontradoExcepcion -> 404
//    NpgsqlException        -> 500 (mensaje del motor en 'detalle')
//    cualquier otra         -> 500
//  (El 422 por body mal formado lo genera el framework antes de entrar aquí,
//   con el formato definido en Program.cs.)
//
// --------------------------------------------------------------------------
//  PRINCIPIOS EN ESTE ARCHIVO:
//   · SOLID · S (Responsabilidad única): SOLO traduce HTTP <-> negocio.
//     Ni SQL ni reglas. Si mañana se agrega gRPC, esta clase no se reusa:
//     se escribe otra fachada que también llama a IServicioSede.
//   · SOLID · D (Inversión de dependencias): depende de la INTERFAZ
//     IServicioSede, jamás de la clase ServicioSede. La instancia entra por
//     el constructor (la pone el contenedor de Program.cs).
//   · SOLID · I (Segregación de interfaces): el controlador solo "ve"
//     IServicioSede (6 métodos cohesionados); no conoce IRepositorioSede.
//   · ACID · C (Consistencia, cara visible): cuando el motor rechaza una
//     escritura por violar una restricción (NpgsqlException: pk_sede,
//     uq_sede_nombre, FK fk_sesion_sede...), aquí se traduce a 500 con el
//     mensaje del motor. La BD quedó intacta.
//   · ACID · C (regla de existencia): 'filasAfectadas == 0' llega como
//     NoEncontradoExcepcion y se traduce a 404.
//
//  FLUJO DE UNA PETICIÓN — PASOS 3 y 8 (los intermedios están en Servicio
//  y Repositorio):
//   [FLUJO 3] El método de acción recibe la 'Peticion' YA validada y llama
//             a _servicio.XxxAsync(...).  <-- entra a la capa de negocio.
//   [FLUJO 8] Vuelve el resultado (o sube una excepción). El try/catch de
//             cada método arma el sobre JSON y fija el código HTTP. Fin.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

// [ApiController]: activa el enlace automático del body y la validación de
// las peticiones (respuesta 422 antes de ejecutar el método).
// [Route("api/Sede")]: prefijo de ruta de TODOS los métodos (Artículo 8).
[ApiController]
[Route("api/Sede")]
public class SedeController : ControllerBase
{
    // [SOLID · D] El controlador depende de la ABSTRACCIÓN IServicioSede,
    // no de la clase concreta. [FLUJO 0->3] El contenedor de DI ya sabe (por
    // Program.cs) qué construir y lo entrega por este constructor.
    private readonly IServicioSede _servicio;

    public SedeController(IServicioSede servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF1 · GET /api/Sede?limite=N   -> 200 { tabla, limite, total, datos }
    //  Tabla vacía -> 204 sin cuerpo.
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? limite)
    {
        try
        {
            // [FLUJO 3] Salta a la capa de negocio por la interfaz.
            var (datos, total) = await _servicio.ListarAsync(limite);

            // Sin filas -> 204 No Content (sin cuerpo).
            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "sede",
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
            return Problema(500, "error_interno", "Error al listar las sedes.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF2 · GET /api/Sede/{idSede}   -> 200 con la sede | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idSede}")]
    public async Task<IActionResult> Obtener(string idSede)
    {
        try
        {
            var sede = await _servicio.ObtenerAsync(idSede);
            return Ok(sede);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sede no encontrada.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener la sede.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF3 · POST /api/Sede   -> 200 { estado, mensaje } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] SedeCrear peticion)
    {
        try
        {
            await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creada",
                mensaje = $"Sede '{peticion.IdSede}' creada.",
            });
        }
        catch (NpgsqlException ex)
        {
            // [ACID · C] id_sede o nombre duplicado (pk_sede / uq_sede_nombre):
            // PostgreSQL aborta el INSERT para NO dejar la tabla inconsistente.
            // [ACID · A] El INSERT completo se deshace (no queda a medias).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear la sede.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF4 · PUT /api/Sede/{idSede}   -> 200 { filasAfectadas } | 404 | 422
    //  PUT = reemplazo completo: falta un campo obligatorio -> 422.
    // ----------------------------------------------------------------------
    [HttpPut("{idSede}")]
    public async Task<IActionResult> Reemplazar(string idSede, [FromBody] SedeReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idSede, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sede no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar la sede.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF5 · PATCH /api/Sede/{idSede} -> 200 { filasAfectadas } | 404 | 400
    //  PATCH = parcial: el mismo body que falla en PUT aquí pasa.
    // ----------------------------------------------------------------------
    [HttpPatch("{idSede}")]
    public async Task<IActionResult> Actualizar(string idSede, [FromBody] SedeActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idSede, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            // body vacío (ningún campo) -> regla de negocio -> 400
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sede no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar la sede.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF6 · DELETE /api/Sede/{idSede} -> 200 { filasEliminadas } | 404
    // ----------------------------------------------------------------------
    [HttpDelete("{idSede}")]
    public async Task<IActionResult> Eliminar(string idSede)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idSede);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Sede no encontrada.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // [ACID · C] FK fk_sesion_sede (ON DELETE RESTRICT): borrar una
            // sede con sesiones dejaría filas "huérfanas" -> el motor lo
            // impide y la integridad referencial se mantiene.
            return Problema(500, "error_motor", "No se pudo eliminar la sede.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar la sede.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  Ayuda privada: arma el sobre de error { estado, mensaje, detalle }
    //  con el código HTTP indicado.
    // ----------------------------------------------------------------------
    private ObjectResult Problema(int codigo, string estado, string mensaje, string? detalle)
    {
        return new ObjectResult(new { estado, mensaje, detalle })
        {
            StatusCode = codigo,
        };
    }
}
