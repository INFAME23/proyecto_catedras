// ==========================================================================
//  Peticiones/PreguntaReemplazo.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del PUT /api/Pregunta/{idPregunta} (RF162): REEMPLAZO COMPLETO de
//  las columnas PROPIAS de la pregunta. NO toca sus opciones (esas se
//  administran aparte, por 'OpcionPregunta' — D33). 'fk_encuesta' no
//  viaja: no se cambia una pregunta de encuesta, se borra y se crea otra.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Pregunta/{idPregunta}.</summary>
public class PreguntaReemplazo
{
    [Required(ErrorMessage = "El campo 'enunciado' es obligatorio.")]
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enunciado' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enunciado")]
    public string? Enunciado { get; set; }

    [Required(ErrorMessage = "El campo 'orden' es obligatorio.")]
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }

    [Required(ErrorMessage = "El campo 'fk_tipo_pregunta' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_pregunta' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_tipo_pregunta")]
    public string? FkTipoPregunta { get; set; }

    /// <summary>Obligatorio en el PUT (true o false, no queda librado al DEFAULT de la BD).</summary>
    [Required(ErrorMessage = "El campo 'obligatoria' es obligatorio (true o false).")]
    [JsonPropertyName("obligatoria")]
    public bool? Obligatoria { get; set; }

    /// <summary>Opcional; si llega, 'valor_max' también debe llegar (servicio, 400).</summary>
    [JsonPropertyName("valor_min")]
    public short? ValorMin { get; set; }

    /// <summary>Opcional; si llega, debe ser mayor que 'valor_min' (servicio, 400).</summary>
    [JsonPropertyName("valor_max")]
    public short? ValorMax { get; set; }
}
