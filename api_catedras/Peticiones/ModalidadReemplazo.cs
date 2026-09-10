// ==========================================================================
//  Peticiones/ModalidadReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/Modalidad/{idModalidad}: reemplazo COMPLETO.
//  PUT = todos los campos obligatorios (el código va en la URL).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Modalidad/{idModalidad}.</summary>
public class ModalidadReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo canal de difusión. Obligatorio: "QR", "ENLACE" o "AMBOS".</summary>
    [Required(ErrorMessage = "El campo 'canal_difusion' es obligatorio.")]
    [RegularExpression("^(QR|ENLACE|AMBOS)$",
        ErrorMessage = "El campo 'canal_difusion' debe ser 'QR', 'ENLACE' o 'AMBOS'.")]
    [JsonPropertyName("canal_difusion")]
    public string? CanalDifusion { get; set; }

    /// <summary>¿Está activa? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
