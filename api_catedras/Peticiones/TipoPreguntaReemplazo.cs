// ==========================================================================
//  Peticiones/TipoPreguntaReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/TipoPregunta/{idTipoPregunta}: reemplazo COMPLETO.
//  Los tres booleanos son obligatorios aquí -- mismo criterio que
//  Sede.EsVirtual: PUT = reemplazo completo, aunque la columna tenga
//  DEFAULT (2_spec.md, RF53).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/TipoPregunta/{idTipoPregunta}.</summary>
public class TipoPreguntaReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Guarda número? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'guarda_numero' es obligatorio (true o false).")]
    [JsonPropertyName("guarda_numero")]
    public bool? GuardaNumero { get; set; }

    /// <summary>¿Guarda texto? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'guarda_texto' es obligatorio (true o false).")]
    [JsonPropertyName("guarda_texto")]
    public bool? GuardaTexto { get; set; }

    /// <summary>¿Guarda opción? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'guarda_opcion' es obligatorio (true o false).")]
    [JsonPropertyName("guarda_opcion")]
    public bool? GuardaOpcion { get; set; }
}
