// ==========================================================================
//  Peticiones/EncuestaReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/Encuesta/{idEncuesta}: reemplazo COMPLETO.
//  PUT = todos los campos obligatorios en el body ('id_encuesta' no: va en
//  la URL y de todas formas la BD nunca lo reasigna). 'vigente_hasta' es
//  obligatorio EN EL BODY pero su valor admite null (así se puede "borrar"
//  una fecha de fin explícitamente, igual que 'direccion' en Sede).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Encuesta/{idEncuesta}.</summary>
public class EncuestaReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, 1 a 120 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(120, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 120 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva versión. Obligatoria en el PUT. Debe ser >= 1.</summary>
    [Required(ErrorMessage = "El campo 'version' es obligatorio.")]
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'version' debe ser un entero mayor o igual a 1.")]
    [JsonPropertyName("version")]
    public short? Version { get; set; }

    /// <summary>Nueva fecha de inicio de vigencia. Obligatoria en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'vigente_desde' es obligatorio.")]
    [JsonPropertyName("vigente_desde")]
    public DateOnly? VigenteDesde { get; set; }

    /// <summary>Nueva fecha de fin de vigencia. Admite null (sigue vigente).</summary>
    [JsonPropertyName("vigente_hasta")]
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>¿Está activa? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activa' es obligatorio (true o false).")]
    [JsonPropertyName("activa")]
    public bool? Activa { get; set; }
}
