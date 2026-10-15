// ==========================================================================
//  Peticiones/OpcionPreguntaCrear.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del POST /api/OpcionPregunta (RF169): agrega una opción a una
//  pregunta que YA existe (a diferencia de 'OpcionInicial', que nace
//  dentro del POST /api/Pregunta). '(fk_pregunta, orden)' duplicado -> 500
//  (uq_opcion_orden).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/OpcionPregunta.</summary>
public class OpcionPreguntaCrear
{
    [Required(ErrorMessage = "El campo 'fk_pregunta' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_pregunta' debe ser mayor que 0.")]
    [JsonPropertyName("fk_pregunta")]
    public long? FkPregunta { get; set; }

    [Required(ErrorMessage = "El campo 'etiqueta' es obligatorio.")]
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'etiqueta' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("etiqueta")]
    public string? Etiqueta { get; set; }

    /// <summary>Peso numérico opcional de la opción.</summary>
    [JsonPropertyName("valor")]
    public short? Valor { get; set; }

    [Required(ErrorMessage = "El campo 'orden' es obligatorio.")]
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }
}
