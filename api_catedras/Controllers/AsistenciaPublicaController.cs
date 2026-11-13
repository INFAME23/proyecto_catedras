// ==========================================================================
//  Controllers/AsistenciaPublicaController.cs  ·  Subversión v7.2 · RF-S9 / RF-S10
// --------------------------------------------------------------------------
//  Registro público de asistencia tras escanear el QR: sin cuenta y SIN la
//  clave de 6 dígitos por correo (D60). No tiene tabla propia (como QrSesion).
//
//    GET  /api/AsistenciaPublica/enlace/{token}   -> 200 datos de la página | 404
//    GET  /api/AsistenciaPublica/ubicacion        -> 200 aviso temprano | 400 | 404 | 409
//    POST /api/AsistenciaPublica/estudiante       -> 201 | 400 | 404 | 409 | 422
//    POST /api/AsistenciaPublica/docente          -> 201 | 400 | 404 | 409 | 422
//    POST /api/AsistenciaPublica/externo          -> 201 | 400 | 404 | 409 | 422
//
//  Códigos de los registros (el 'estado' viene de la base de datos, migración 063):
//    OK                       -> 201
//    ENLACE_NO_EXISTE         -> 404
//    DATOS_INVALIDOS          -> 400
//    cualquier otro estado    -> 409 (rechazo de negocio: ventana, ubicación, duplicado...)
//  En los rechazos el body es { estado, mensaje, detalle, distancia_metros, radio_metros }:
//  'estado' es el código para que el front decida, 'mensaje' el texto para la persona.
//
//  La API sigue abierta (D28): estos endpoints son públicos a propósito.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/AsistenciaPublica")]
public class AsistenciaPublicaController : ControllerBase
{
    private readonly IServicioAsistenciaPublica _servicio;

    public AsistenciaPublicaController(IServicioAsistenciaPublica servicio)
    {
        _servicio = servicio;
    }

    // ----------------------------------------------------------------------
    //  GET enlace/{token}: lo que necesita la página pública
    // ----------------------------------------------------------------------
    [HttpGet("enlace/{token:guid}")]
    public async Task<IActionResult> Enlace(Guid token)
    {
        try
        {
            return Ok(await _servicio.ConsultarEnlaceAsync(token));
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "ENLACE_NO_EXISTE", ex.Message, null);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al consultar el enlace.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  GET ubicacion: aviso temprano (¿estás dentro del radio de la sede?)
    // ----------------------------------------------------------------------
    [HttpGet("ubicacion")]
    public async Task<IActionResult> Ubicacion(
        [FromQuery] Guid? token, [FromQuery] decimal? latitud, [FromQuery] decimal? longitud)
    {
        try
        {
            var resultado = await _servicio.VerificarUbicacionAsync(token, latitud, longitud);

            // Estar FUERA del radio no es un error de la petición: es justo lo que el aviso quiere decir (200).
            // Solo un enlace que ya no sirve o una sede sin coordenadas son conflictos (409).
            return resultado.Estado is "ENLACE_REVOCADO" or "FUERA_DE_VENTANA" or "SEDE_SIN_COORDENADAS"
                ? Rechazo(resultado.Estado, resultado.Mensaje, resultado.DistanciaMetros, resultado.RadioMetros)
                : Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return Problema(400, "peticion_invalida", "Parámetro de consulta inválido.", ex.Message);
        }
        catch (NoEncontradoExcepcion ex)
        {
            return Problema(404, "ENLACE_NO_EXISTE", ex.Message, null);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al verificar la ubicación.", ex.Message);
        }
    }

    // ----------------------------------------------------------------------
    //  POST estudiante / docente / externo
    // ----------------------------------------------------------------------
    [HttpPost("estudiante")]
    public Task<IActionResult> RegistrarEstudiante([FromBody] AsistenciaEstudianteCrear peticion) =>
        Registrar(() => _servicio.RegistrarEstudianteAsync(peticion), "estudiante");

    [HttpPost("docente")]
    public Task<IActionResult> RegistrarDocente([FromBody] AsistenciaDocenteCrear peticion) =>
        Registrar(() => _servicio.RegistrarDocenteAsync(peticion), "docente");

    [HttpPost("externo")]
    public Task<IActionResult> RegistrarExterno([FromBody] AsistenciaExternoCrear peticion) =>
        Registrar(() => _servicio.RegistrarExternoAsync(peticion), "externo");

    // Los tres POST se resuelven igual: llamar al servicio y traducir el estado a HTTP.
    private async Task<IActionResult> Registrar(Func<Task<ResultadoRegistroPublico>> accion, string quien)
    {
        try
        {
            var resultado = await accion();

            return resultado.Estado switch
            {
                "OK" => StatusCode(201, new
                {
                    estado = "OK",
                    mensaje = resultado.Mensaje,
                    id_registro = resultado.IdRegistro,
                    distancia_metros = resultado.DistanciaMetros,
                    radio_metros = resultado.RadioMetros,
                }),
                "ENLACE_NO_EXISTE" => Problema(404, resultado.Estado, resultado.Mensaje, null),
                "DATOS_INVALIDOS" => Problema(400, resultado.Estado, resultado.Mensaje, null),
                _ => Rechazo(resultado.Estado, resultado.Mensaje, resultado.DistanciaMetros, resultado.RadioMetros),
            };
        }
        catch (ArgumentException ex)
        {
            // Por ejemplo, una IP mal formada.
            return Problema(400, "peticion_invalida", "Petición inválida.", ex.Message);
        }
        catch (NpgsqlException ex)
        {
            // Lo inesperado (FK, CHECK...): los casos de negocio llegan como 'estado', no como excepción.
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", $"Error al registrar la asistencia del {quien}.", ex.Message);
        }
    }

    // Rechazo de negocio: 409 con el estado como código y la distancia/radio si se calcularon.
    private ObjectResult Rechazo(string estado, string? mensaje, decimal? distanciaMetros, int? radioMetros)
    {
        return new ObjectResult(new
        {
            estado,
            mensaje,
            detalle = (string?)null,
            distancia_metros = distanciaMetros,
            radio_metros = radioMetros,
        })
        {
            StatusCode = 409,
        };
    }

    private ObjectResult Problema(int codigo, string estado, string? mensaje, string? detalle)
    {
        return new ObjectResult(new { estado, mensaje, detalle })
        {
            StatusCode = codigo,
        };
    }
}
