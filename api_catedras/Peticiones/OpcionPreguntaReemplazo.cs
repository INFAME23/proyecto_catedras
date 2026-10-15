// ==========================================================================
//  Peticiones/OpcionPreguntaReemplazo.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del PUT /api/OpcionPregunta/{idOpcion} (RF170): reemplazo completo.
//  'fk_pregunta' no viaja: la opción no cambia de pregunta (se borra y se
//  crea otra en la pregunta correcta).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/OpcionPregunta/{idOpcion}.</summary>
public class OpcionPreguntaReemplazo
{
    [Required(ErrorMessage = "El campo 'etiqueta' es obligatorio.")]
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'etiqueta' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("etiqueta")]
    public string? Etiqueta { get; set; }

    /// <summary>Opcional: si no llega o es null, el peso numérico queda en NULL.</summary>
    [JsonPropertyName("valor")]
    public short? Valor { get; set; }

    [Required(ErrorMessage = "El campo 'orden' es obligatorio.")]
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }
}
