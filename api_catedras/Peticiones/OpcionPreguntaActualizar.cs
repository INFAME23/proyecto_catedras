// ==========================================================================
//  Peticiones/OpcionPreguntaActualizar.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del PATCH /api/OpcionPregunta/{idOpcion} (RF170): parcial, todo
//  opcional.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/OpcionPregunta/{idOpcion}. Todo opcional.</summary>
public class OpcionPreguntaActualizar
{
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'etiqueta' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("etiqueta")]
    public string? Etiqueta { get; set; }

    [JsonPropertyName("valor")]
    public short? Valor { get; set; }

    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Etiqueta is not null) datos["etiqueta"] = Etiqueta;
        if (Valor is not null) datos["valor"] = Valor;
        if (Orden is not null) datos["orden"] = Orden;

        return datos;
    }
}
