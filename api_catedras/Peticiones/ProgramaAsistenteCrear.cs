// ==========================================================================
//  Peticiones/ProgramaAsistenteCrear.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del POST /api/ProgramaAsistente (RF110): matricula a un asistente
//  en un programa, para un periodo. La misma llave exacta (asistente +
//  programa + periodo) ya existente -> 500 (PK duplicada, D9 / RF111).
//
//  LLAVE COMPUESTA (ADR D16): sin peticiones 'Reemplazo' ni 'Actualizar';
//  lo único que cambia después es el 'estado'
//  (ProgramaAsistenteCambiarEstado).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/ProgramaAsistente.</summary>
public class ProgramaAsistenteCrear
{
    /// <summary>Asistente. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Código del programa (v2). Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_programa' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_programa' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_programa")]
    public string? FkPrograma { get; set; }

    /// <summary>Periodo, p. ej. '2026-1'. Obligatorio, exactamente 6 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'fk_periodo' es obligatorio.")]
    [StringLength(6, MinimumLength = 6,
        ErrorMessage = "El campo 'fk_periodo' debe tener exactamente 6 caracteres (p. ej. '2026-1').")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    /// <summary>Estado. Opcional (por defecto ACTIVO); lista cerrada.</summary>
    [RegularExpression("^(ACTIVO|RETIRADO|GRADUADO|APLAZADO)$",
        ErrorMessage = "El campo 'estado' debe ser ACTIVO, RETIRADO, GRADUADO o APLAZADO.")]
    [JsonPropertyName("estado")]
    public string? Estado { get; set; }
}
