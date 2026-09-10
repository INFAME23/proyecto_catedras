// ==========================================================================
//  Peticiones/PeriodoAcademicoReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/PeriodoAcademico/{idPeriodo}: reemplazo COMPLETO.
//  PUT = todos los campos obligatorios (el código va en la URL).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/PeriodoAcademico/{idPeriodo}.</summary>
public class PeriodoAcademicoReemplazo
{
    /// <summary>Nueva fecha de inicio. Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'fecha_ini' es obligatorio.")]
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }

    /// <summary>Nueva fecha de fin. Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'fecha_fin' es obligatorio.")]
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }

    /// <summary>¿Está activo? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
