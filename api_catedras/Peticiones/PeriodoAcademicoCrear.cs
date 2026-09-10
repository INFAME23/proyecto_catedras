// ==========================================================================
//  Peticiones/PeriodoAcademicoCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/PeriodoAcademico: CREAR un periodo nuevo.
//
//  'id_periodo' se valida aquí con [RegularExpression]: es un formato
//  FIJO ("AAAA-N", N=1,2,3), comprobación de FORMA (3_plan.md §4.9), igual
//  que 'canal_difusion' de Modalidad. La coherencia 'fecha_fin > fecha_ini'
//  NO es de forma (cruza dos campos) -> la valida el SERVICIO (§4.8).
//  A diferencia de Encuesta, aquí NINGUNA fecha tiene DEFAULT en la BD:
//  las dos son obligatorias.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/PeriodoAcademico.</summary>
public class PeriodoAcademicoCrear
{
    /// <summary>Código del periodo. Obligatorio, formato "AAAA-N" (N=1,2,3).</summary>
    [Required(ErrorMessage = "El campo 'id_periodo' es obligatorio.")]
    [RegularExpression("^[0-9]{4}-[1-3]$",
        ErrorMessage = "El campo 'id_periodo' debe tener el formato 'AAAA-N' (N entre 1 y 3), por ejemplo '2026-1'.")]
    [JsonPropertyName("id_periodo")]
    public string? IdPeriodo { get; set; }

    /// <summary>Fecha de inicio. Obligatoria (la columna no tiene DEFAULT).</summary>
    [Required(ErrorMessage = "El campo 'fecha_ini' es obligatorio.")]
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }

    /// <summary>Fecha de fin. Obligatoria (la columna no tiene DEFAULT).</summary>
    [Required(ErrorMessage = "El campo 'fecha_fin' es obligatorio.")]
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }

    /// <summary>¿Está activo? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
