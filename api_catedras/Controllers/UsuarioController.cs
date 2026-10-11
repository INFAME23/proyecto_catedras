// ==========================================================================
//  Controllers/UsuarioController.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Capa HTTP de 'Usuario'. Molde completo de 6 endpoints (como Catedra) más
//  el LOGIN: POST /api/Usuario/login.
//
//  Tres reglas propias de este recurso:
//   · 'clave_hash' NUNCA sale: el modelo lo marca [JsonIgnore] y las
//     confirmaciones del POST solo devuelven id_usuario y creado_en (RF138).
//   · El login responde 401 con un mensaje único para las tres causas
//     (usuario inexistente, inactivo, clave mala), ADR D27.
//   · Esta API no autoriza por rol (ADR D28): el login solo informa los roles
//     vigentes; quien decide qué pantalla ve cada rol es el front.
//  Errores: ArgumentException -> 400 · NoEncontradoExcepcion -> 404 ·
//  NpgsqlException -> 500 (usuario o asistente duplicado, FK inexistente).
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;   // NpgsqlException

[ApiController]
[Route("api/Usuario")]
public class UsuarioController : ControllerBase
{
    // [SOLID · D] El controlador solo conoce la interfaz del servicio.
    private readonly IServicioUsuario _servicio;

    public UsuarioController(IServicioUsuario servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF132 · GET /api/Usuario?limite=N   -> 200 { tabla, limite, total, datos } | 204
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
                tabla = "usuario",
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
            return Problema(500, "error_interno", "Error al listar los usuarios.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF133 · GET /api/Usuario/{idUsuario:long}   -> 200 | 404
    // ----------------------------------------------------------------------
    [HttpGet("{idUsuario:long}")]
    public async Task<IActionResult> Obtener(long idUsuario)
    {
        try
        {
            return Ok(await _servicio.ObtenerAsync(idUsuario));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Usuario no encontrado.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al obtener el usuario.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF134 · POST /api/Usuario   -> 200 { estado, mensaje, id_usuario, creado_en }
    //                                 | 400 | 422 | 500
    //  La respuesta NO incluye la clave ni el hash.
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] UsuarioCrear peticion)
    {
        try
        {
            var (idUsuario, creadoEn) = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Usuario '{peticion.Usuario}' creado.",
                id_usuario = idUsuario,
                creado_en = creadoEn,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Nombre de usuario repetido (uq_usuario_login), asistente que ya
            // tiene usuario (uq_usuario_persona) o asistente inexistente (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al crear el usuario.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF135 · PUT /api/Usuario/{idUsuario:long}   -> 200 { filasAfectadas } | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPut("{idUsuario:long}")]
    public async Task<IActionResult> Reemplazar(long idUsuario, [FromBody] UsuarioReemplazo peticion)
    {
        try
        {
            int filas = await _servicio.ReemplazarAsync(idUsuario, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Usuario no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al reemplazar el usuario.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF136 · PATCH /api/Usuario/{idUsuario:long}   -> 200 { filasAfectadas } | 400 | 404
    //  Si el body trae 'clave', se re-hashea; si no, el hash queda intacto.
    // ----------------------------------------------------------------------
    [HttpPatch("{idUsuario:long}")]
    public async Task<IActionResult> Actualizar(long idUsuario, [FromBody] UsuarioActualizar peticion)
    {
        try
        {
            int filas = await _servicio.ActualizarAsync(idUsuario, peticion);
            return Ok(new { filasAfectadas = filas });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Usuario no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al actualizar el usuario.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF137 · DELETE /api/Usuario/{idUsuario:long}   -> 200 { filasEliminadas } | 404 | 500
    //  ⚠ 'rol_por_usuario' se borra en CASCADA con el usuario.
    // ----------------------------------------------------------------------
    [HttpDelete("{idUsuario:long}")]
    public async Task<IActionResult> Eliminar(long idUsuario)
    {
        try
        {
            int filas = await _servicio.EliminarAsync(idUsuario);
            return Ok(new { filasEliminadas = filas });
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Usuario no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "No se pudo eliminar el usuario.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al eliminar el usuario.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF139 · POST /api/Usuario/login   -> 200 { estado, usuario, roles } | 401 | 422
    //  (ADR D27) Usuario inexistente, inactivo o clave incorrecta -> el MISMO
    //  401 y el MISMO mensaje: no se revela cuál de las tres causas fue.
    //  La ruta literal "login" no choca con "{idUsuario:long}": ese segmento
    //  solo acepta números.
    // ----------------------------------------------------------------------
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UsuarioLogin peticion)
    {
        try
        {
            var resultado = await _servicio.LoginAsync(peticion);

            if (resultado is null)
            {
                return Unauthorized(new
                {
                    estado = "no_autorizado",
                    mensaje = "Usuario o clave incorrectos",
                });
            }

            var (usuario, roles) = resultado.Value;
            return Ok(new
            {
                estado = "autenticado",
                usuario,    // sin clave_hash: lo impide el [JsonIgnore] del modelo
                roles,      // códigos de los roles vigentes, p. ej. ["ADMIN"]
            });
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al validar el acceso.", ex.Message);
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
