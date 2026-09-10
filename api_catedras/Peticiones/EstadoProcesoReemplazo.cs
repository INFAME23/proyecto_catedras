// ==========================================================================
//  Peticiones/EstadoProcesoReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/EstadoProceso/{idEstado}: reemplazo COMPLETO.
//  PUT = todos los campos obligatorios (el código va en la URL).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/EstadoProceso/{idEstado}.</summary>
public class EstadoProcesoReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, hasta 40 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(40, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 40 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Es final? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'es_final' es obligatorio (true o false).")]
    [JsonPropertyName("es_final")]
    public bool? EsFinal { get; set; }

    /// <summary>¿Fue exitoso? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'es_exitoso' es obligatorio (true o false).")]
    [JsonPropertyName("es_exitoso")]
    public bool? EsExitoso { get; set; }
}
