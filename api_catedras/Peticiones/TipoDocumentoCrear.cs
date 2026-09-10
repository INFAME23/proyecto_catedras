// ==========================================================================
//  Peticiones/TipoDocumentoCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/TipoDocumento: CREAR un tipo de documento nuevo.
//  Mismo patrón que ModalidadCrear (sin la lista cerrada de canal).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/TipoDocumento.</summary>
public class TipoDocumentoCrear
{
    /// <summary>Código del tipo de documento. Obligatorio, 1 a 5 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_tipo_documento' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'id_tipo_documento' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("id_tipo_documento")]
    public string? IdTipoDocumento { get; set; }

    /// <summary>Nombre del tipo de documento. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Está activo? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
