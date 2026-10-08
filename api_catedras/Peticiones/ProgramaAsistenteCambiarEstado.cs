// ==========================================================================
//  Peticiones/ProgramaAsistenteCambiarEstado.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Body del PATCH /api/ProgramaAsistente/cambiar-estado (RF110).
//  La fila se identifica con su LLAVE COMPLETA en el body (ADR D16):
//  'fk_asistente', 'fk_programa' y 'fk_periodo'. Más el 'estado' nuevo
//  (obligatorio, lista cerrada).
//
//  Reglas del SERVICIO: llave inexistente -> 404; el estado nuevo es igual
//  al actual -> 400 (no hay nada que cambiar).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/ProgramaAsistente/cambiar-estado.</summary>
public class ProgramaAsistenteCambiarEstado
{
    /// <summary>Llave (1/3): asistente.</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Llave (2/3): programa.</summary>
    [Required(ErrorMessage = "El campo 'fk_programa' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_programa' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_programa")]
    public string? FkPrograma { get; set; }

    /// <summary>Llave (3/3): periodo.</summary>
    [Required(ErrorMessage = "El campo 'fk_periodo' es obligatorio.")]
    [StringLength(6, MinimumLength = 6,
        ErrorMessage = "El campo 'fk_periodo' debe tener exactamente 6 caracteres (p. ej. '2026-1').")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    /// <summary>Estado nuevo. Obligatorio; lista cerrada.</summary>
    [Required(ErrorMessage = "El campo 'estado' es obligatorio.")]
    [RegularExpression("^(ACTIVO|RETIRADO|GRADUADO|APLAZADO)$",
        ErrorMessage = "El campo 'estado' debe ser ACTIVO, RETIRADO, GRADUADO o APLAZADO.")]
    [JsonPropertyName("estado")]
    public string? Estado { get; set; }
}
