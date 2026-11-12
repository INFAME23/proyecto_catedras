// ==========================================================================
//  Peticiones/RestriccionProgramacionEscribir.cs  ·  Proyecto Cátedras · v7.1 · D57
// --------------------------------------------------------------------------
//  Body del POST /api/RestriccionProgramacion (crear) y del PUT .../{id}
//  (reemplazo completo): los dos llevan los mismos campos.
//
//  Reglas de forma aquí (422). Las que cruzan campos —fecha_hasta >= fecha_desde,
//  las dos horas juntas y hora_hasta > hora_desde, días sin repetir— las revisa
//  el servicio (400) y, de último, los CHECK de la tabla.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST y del PUT de /api/RestriccionProgramacion.</summary>
public class RestriccionProgramacionEscribir
{
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "El campo 'nombre' debe tener entre 3 y 100 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "El campo 'fecha_desde' es obligatorio (AAAA-MM-DD).")]
    [JsonPropertyName("fecha_desde")]
    public DateOnly? FechaDesde { get; set; }

    [Required(ErrorMessage = "El campo 'fecha_hasta' es obligatorio (AAAA-MM-DD).")]
    [JsonPropertyName("fecha_hasta")]
    public DateOnly? FechaHasta { get; set; }

    /// <summary>'HH:mm'. Opcional, pero va junto con 'hora_hasta'.</summary>
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "El campo 'hora_desde' debe ser una hora HH:mm (00:00 a 23:59).")]
    [JsonPropertyName("hora_desde")]
    public string? HoraDesde { get; set; }

    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "El campo 'hora_hasta' debe ser una hora HH:mm (00:00 a 23:59).")]
    [JsonPropertyName("hora_hasta")]
    public string? HoraHasta { get; set; }

    /// <summary>Días permitidos, ISO 1 (lunes) a 7 (domingo). Vacío o ausente = todos.</summary>
    [JsonPropertyName("dias_semana")]
    public List<int>? DiasSemana { get; set; }

    /// <summary>Opcional (por defecto true).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
