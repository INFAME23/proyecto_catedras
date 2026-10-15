// ==========================================================================
//  Peticiones/RespuestaEncuestaResponder.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del POST /api/RespuestaEncuesta (RF174). ÚNICA petición de esta
//  entidad: no hay Crear/Reemplazo/Actualizar genéricos (ADR D31/D32) — el
//  único verbo de escritura es "responder", y responder es la única forma
//  de que esta entidad nazca.
//
//  '[MinLength(1)]' en 'Respuestas' es la primera muralla contra una lista
//  vacía, AUNQUE la BD también la rechazaría (R29, dentro del SP) —
//  doble verificación deliberada, mismo criterio que
//  'RegistroAsistenciaCrear.Origen' en v5: más barato fallar en 422 que
//  en 500 cuando el error ya se puede detectar sin tocar la BD.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/RespuestaEncuesta (= "responder la encuesta").</summary>
public class RespuestaEncuestaResponder
{
    /// <summary>Registro de asistencia que responde. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fkRegistro' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fkRegistro' debe ser mayor que 0.")]
    [JsonPropertyName("fkRegistro")]
    public long? FkRegistro { get; set; }

    /// <summary>Una respuesta por pregunta contestada. No puede venir vacía.</summary>
    [Required(ErrorMessage = "El campo 'respuestas' es obligatorio.")]
    [MinLength(1, ErrorMessage = "El campo 'respuestas' no puede venir vacío.")]
    [JsonPropertyName("respuestas")]
    public List<ItemRespuesta>? Respuestas { get; set; }
}
