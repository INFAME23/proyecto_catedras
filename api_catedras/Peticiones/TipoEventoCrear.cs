// ==========================================================================
//  Peticiones/TipoEventoCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/TipoEvento: CREAR un tipo de evento nuevo.
//
//  'id_tipo_evento' es CHAR(4) en la BD: longitud FIJA de 4 caracteres,
//  no "hasta 4" como los VARCHAR de las demás tablas. Por eso
//  [StringLength(4, MinimumLength = 4)] en vez de MinimumLength = 1
//  (3_plan.md, nota de diseño de TipoEvento en 2_spec.md).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/TipoEvento.</summary>
public class TipoEventoCrear
{
    /// <summary>Código del tipo de evento. Obligatorio, EXACTAMENTE 4 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_tipo_evento' es obligatorio.")]
    [StringLength(4, MinimumLength = 4,
        ErrorMessage = "El campo 'id_tipo_evento' debe tener exactamente 4 caracteres.")]
    [JsonPropertyName("id_tipo_evento")]
    public string? IdTipoEvento { get; set; }

    /// <summary>Nombre del tipo de evento. Obligatorio, hasta 80 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(80, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 80 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Está activo? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
