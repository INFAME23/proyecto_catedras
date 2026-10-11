// ==========================================================================
//  Controllers/RolPorUsuarioController.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Capa HTTP de 'RolPorUsuario'. SOLO 3 endpoints (ADR D16):
//     GET   /api/RolPorUsuario?fkUsuario=ID   lista
//     POST  /api/RolPorUsuario                abre un rol
//     PATCH /api/RolPorUsuario/cerrar         cierra (llave en el body)
//
//  NO hay [HttpGet("{id}")], [HttpPut] ni [HttpDelete]: la tabla no tiene
//  columna 'id' y la historia de roles no se reemplaza ni se borra. Una
//  llamada a esas rutas responde 404/405 del propio ASP.NET.
//  (Esta API no autoriza por rol, ADR D28: aquí solo se administra el dato.)
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/RolPorUsuario")]
public class RolPorUsuarioController : ControllerBase
{
    private readonly IServicioRolPorUsuario _servicio;

    public RolPorUsuarioController(IServicioRolPorUsuario servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  RF140 · GET /api/RolPorUsuario?fkUsuario=ID&limite=N   -> 200 | 204
    // ----------------------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? fkUsuario, [FromQuery] int? limite)
    {
        try
        {
            var (datos, total) = await _servicio.ListarAsync(fkUsuario, limite);

            if (total == 0)
                return NoContent();

            return Ok(new
            {
                tabla = "rol_por_usuario",
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
            return Problema(500, "error_interno", "Error al listar los roles de usuario.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF141 · POST /api/RolPorUsuario   -> 200 { estado, mensaje, rol_por_usuario } | 422 | 500
    // ----------------------------------------------------------------------
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RolPorUsuarioCrear peticion)
    {
        try
        {
            var rol = await _servicio.CrearAsync(peticion);
            return Ok(new
            {
                estado = "creado",
                mensaje = $"Rol '{rol.FkRol}' abierto para el usuario {rol.FkUsuario}.",
                // Se devuelve la fila completa: el cliente necesita su llave
                // (con la fecha_ini efectiva) para poder cerrarla después.
                rol_por_usuario = rol,
            });
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Llave repetida (PK) o usuario/rol inexistente (FK).
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al abrir el rol.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  RF142 · PATCH /api/RolPorUsuario/cerrar   -> 200 | 400 | 404 | 422
    // ----------------------------------------------------------------------
    [HttpPatch("cerrar")]
    public async Task<IActionResult> Cerrar([FromBody] RolPorUsuarioCerrar peticion)
    {
        try
        {
            var rol = await _servicio.CerrarAsync(peticion);
            return Ok(new
            {
                estado = "cerrado",
                mensaje = $"Rol '{rol.FkRol}' cerrado para el usuario {rol.FkUsuario}.",
                rol_por_usuario = rol,
            });
        }
        catch (ArgumentException ex)
        {
            // Ya estaba cerrado, o fecha_fin anterior a fecha_ini.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "no_encontrado", "Rol de usuario no encontrado.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cerrar el rol.", ex.Message);
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
