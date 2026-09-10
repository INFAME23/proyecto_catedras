// ==========================================================================
//  Peticiones/TipoPreguntaCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/TipoPregunta: CREAR un tipo de pregunta nuevo.
//
//  Los tres booleanos son opcionales aquí (por defecto false, igual que
//  la columna); la regla "EXACTAMENTE una debe ser true" NO es de forma
//  (cruza TRES campos) -> la valida el SERVICIO (3_plan.md §4.8).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/TipoPregunta.</summary>
public class TipoPreguntaCrear
{
    /// <summary>Código del tipo de pregunta. Obligatorio, 1 a 15 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_tipo_pregunta' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'id_tipo_pregunta' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("id_tipo_pregunta")]
    public string? IdTipoPregunta { get; set; }

    /// <summary>Nombre del tipo de pregunta. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Guarda número? Opcional; si no llega, el servicio usa false.</summary>
    [JsonPropertyName("guarda_numero")]
    public bool? GuardaNumero { get; set; }

    /// <summary>¿Guarda texto? Opcional; si no llega, el servicio usa false.</summary>
    [JsonPropertyName("guarda_texto")]
    public bool? GuardaTexto { get; set; }

    /// <summary>¿Guarda opción? Opcional; si no llega, el servicio usa false.</summary>
    [JsonPropertyName("guarda_opcion")]
    public bool? GuardaOpcion { get; set; }
}
