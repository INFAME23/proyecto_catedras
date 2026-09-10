// ==========================================================================
//  Peticiones/TipoVinculacionReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/TipoVinculacion/{idTipoVinculacion}: reemplazo
//  COMPLETO. Los cuatro campos son obligatorios (el código va en la URL).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/TipoVinculacion/{idTipoVinculacion}.</summary>
public class TipoVinculacionReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Es interno? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'es_interno' es obligatorio (true o false).")]
    [JsonPropertyName("es_interno")]
    public bool? EsInterno { get; set; }

    /// <summary>¿Exige programa? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'exige_programa' es obligatorio (true o false).")]
    [JsonPropertyName("exige_programa")]
    public bool? ExigePrograma { get; set; }

    /// <summary>¿Está activo? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
