// ==========================================================================
//  Peticiones/EncuestaCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/Encuesta: CREAR una encuesta nueva.
//
//  A diferencia de Sede, esta petición NO tiene 'id_encuesta': la BD lo
//  genera (GENERATED ALWAYS AS IDENTITY, 3_plan.md §4.7). 'version',
//  'vigente_desde' y 'activa' son opcionales aquí porque la columna tiene
//  DEFAULT en la BD; el servicio aplica el mismo valor por defecto si no
//  llegan. La coherencia 'vigente_hasta >= vigente_desde' NO es de forma
//  (cruza dos campos) -> la valida el SERVICIO (3_plan.md §4.8).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Encuesta.</summary>
public class EncuestaCrear
{
    /// <summary>Nombre del cuestionario. Obligatorio, 1 a 120 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(120, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 120 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Versión. Opcional; si no llega, el servicio usa 1. Debe ser >= 1.</summary>
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'version' debe ser un entero mayor o igual a 1.")]
    [JsonPropertyName("version")]
    public short? Version { get; set; }

    /// <summary>Fecha desde la que rige. Opcional; si no llega, el servicio usa hoy.</summary>
    [JsonPropertyName("vigente_desde")]
    public DateOnly? VigenteDesde { get; set; }

    /// <summary>Fecha hasta la que rige. Opcional; admite ausencia (sigue vigente).</summary>
    [JsonPropertyName("vigente_hasta")]
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>¿Está activa? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("activa")]
    public bool? Activa { get; set; }
}
