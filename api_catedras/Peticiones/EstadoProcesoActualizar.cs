// ==========================================================================
//  Peticiones/EstadoProcesoActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/EstadoProceso/{idEstado}: actualización PARCIAL.
//  Todo opcional; el body vacío es regla de negocio (400) y la decide el
//  SERVICIO. La coherencia es_exitoso/es_final también la valida el
//  servicio, SOLO cuando ambos campos llegan juntos aquí (3_plan.md §4.8).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/EstadoProceso/{idEstado}. Todo opcional.</summary>
public class EstadoProcesoActualizar
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 40 caracteres.</summary>
    [StringLength(40, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 40 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo valor de "es final". Si llega: true o false.</summary>
    [JsonPropertyName("es_final")]
    public bool? EsFinal { get; set; }

    /// <summary>Nuevo valor de "es exitoso". Si llega: true o false.</summary>
    [JsonPropertyName("es_exitoso")]
    public bool? EsExitoso { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que
    /// llegaron (no null). Lista blanca de columnas.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (EsFinal is not null) datos["es_final"] = EsFinal;
        if (EsExitoso is not null) datos["es_exitoso"] = EsExitoso;

        return datos;
    }
}
