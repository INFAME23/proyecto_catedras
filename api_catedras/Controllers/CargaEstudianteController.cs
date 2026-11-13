// ==========================================================================
//  Controllers/CargaEstudianteController.cs  ·  Subversión v7.2 · RF-S8
// --------------------------------------------------------------------------
//  POST /api/Asistente/cargar-estudiante  -> 200 { id_asistente, accion } | 409 | 422
//
//  Carga UNA fila del archivo de estudiantes (CC, carnet, programa) en el
//  periodo elegido. Idempotente: repetir el carnet actualiza, no duplica
//  ('accion' = CREADO o ACTUALIZADO). El front la llama fila por fila, como a
//  POST /api/Asistente, así cada fila es su propia transacción (carga parcial, D15).
//
//  Vive en su propio controlador (y no dentro de AsistenteController) para no
//  tocar el que ya funciona; la ruta es la misma familia 'api/Asistente'.
//  Un rechazo de la fila (programa o periodo inexistente, CC de otra persona,
//  formato) es un 409 con el texto de la base de datos en 'mensaje': el front
//  lo guarda como motivo de la novedad de esa fila.
// ==========================================================================

namespace ApiCatedras.Controllers;

using ApiCatedras.Peticiones;
using ApiCatedras.Servicios;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("api/Asistente/cargar-estudiante")]
public class CargaEstudianteController : ControllerBase
{
    private readonly IServicioCargaEstudiante _servicio;

    public CargaEstudianteController(IServicioCargaEstudiante servicio)
    {
        _servicio = servicio;
    }

    [HttpPost]
    public async Task<IActionResult> Cargar([FromBody] EstudianteCargar peticion)
    {
        try
        {
            return Ok(await _servicio.CargarAsync(peticion));
        }
        catch (PostgresException ex) when (ex.SqlState is "23505" or "23514" or "23503")
        {
            // Rechazos de negocio de sp_cargar_estudiante (064): CC de otra persona (23505),
            // carnet o CC con formato inválido (23514), programa o periodo inexistente (23503).
            return Problema(409, "conflicto", ex.MessageText, null);
        }
        catch (NpgsqlException ex)
        {
            return Problema(500, "error_motor", "La base de datos rechazó la operación.", ex.Message);
        }
        catch (Exception ex)
        {
            return Problema(500, "error_interno", "Error al cargar el estudiante.", ex.Message);
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
