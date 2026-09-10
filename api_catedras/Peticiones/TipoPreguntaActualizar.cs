// ==========================================================================
//  Peticiones/TipoPreguntaActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/TipoPregunta/{idTipoPregunta}: actualización
//  PARCIAL. Todo opcional; el body vacío es regla de negocio (400) y la
//  decide el SERVICIO. La coherencia de los tres booleanos también la
//  valida el servicio, SOLO cuando los TRES llegan juntos aquí
//  (3_plan.md §4.8).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/TipoPregunta/{idTipoPregunta}. Todo opcional.</summary>
public class TipoPreguntaActualizar
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 60 caracteres.</summary>
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo valor de "guarda_numero". Si llega: true o false.</summary>
    [JsonPropertyName("guarda_numero")]
    public bool? GuardaNumero { get; set; }

    /// <summary>Nuevo valor de "guarda_texto". Si llega: true o false.</summary>
    [JsonPropertyName("guarda_texto")]
    public bool? GuardaTexto { get; set; }

    /// <summary>Nuevo valor de "guarda_opcion". Si llega: true o false.</summary>
    [JsonPropertyName("guarda_opcion")]
    public bool? GuardaOpcion { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que
    /// llegaron (no null). Lista blanca de columnas.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (GuardaNumero is not null) datos["guarda_numero"] = GuardaNumero;
        if (GuardaTexto is not null) datos["guarda_texto"] = GuardaTexto;
        if (GuardaOpcion is not null) datos["guarda_opcion"] = GuardaOpcion;

        return datos;
    }
}
