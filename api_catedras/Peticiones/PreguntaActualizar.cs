// ==========================================================================
//  Peticiones/PreguntaActualizar.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del PATCH /api/Pregunta/{idPregunta} (RF163): PARCIAL, todo
//  opcional. Cuerpo vacío -> 400 (lo valida el servicio con
//  'ACamposEnviados().Count == 0', igual criterio que toda la v1-v5).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Pregunta/{idPregunta}. Todo opcional.</summary>
public class PreguntaActualizar
{
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enunciado' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enunciado")]
    public string? Enunciado { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }

    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_pregunta' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_tipo_pregunta")]
    public string? FkTipoPregunta { get; set; }

    [JsonPropertyName("obligatoria")]
    public bool? Obligatoria { get; set; }

    [JsonPropertyName("valor_min")]
    public short? ValorMin { get; set; }

    [JsonPropertyName("valor_max")]
    public short? ValorMax { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Enunciado is not null) datos["enunciado"] = Enunciado;
        if (Orden is not null) datos["orden"] = Orden;
        if (FkTipoPregunta is not null) datos["fk_tipo_pregunta"] = FkTipoPregunta;
        if (Obligatoria is not null) datos["obligatoria"] = Obligatoria;
        if (ValorMin is not null) datos["valor_min"] = ValorMin;
        if (ValorMax is not null) datos["valor_max"] = ValorMax;

        return datos;
    }
}
