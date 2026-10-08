// ==========================================================================
//  Peticiones/DetalleProgramaInicial.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Un elemento de la lista 'programas' del POST /api/Asistente (Guía IA,
//  Regla 1). Sin 'fk_asistente' (lo fija el SP). Programa o periodo que no
//  existan los rechaza la FK del motor (500) y se revierte TODO el alta.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Matrícula en un programa que nace junto con su asistente.</summary>
public class DetalleProgramaInicial
{
    /// <summary>Código del programa (v2). Obligatorio, 1 a 5 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'fk_programa' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_programa' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_programa")]
    public string? FkPrograma { get; set; }

    /// <summary>Periodo académico, p. ej. '2026-1'. Obligatorio, exactamente 6 caracteres (CHAR(6)).</summary>
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
