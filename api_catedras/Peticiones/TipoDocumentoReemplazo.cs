// ==========================================================================
//  Peticiones/TipoDocumentoReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/TipoDocumento/{idTipoDocumento}: reemplazo COMPLETO.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/TipoDocumento/{idTipoDocumento}.</summary>
public class TipoDocumentoReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Está activo? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
