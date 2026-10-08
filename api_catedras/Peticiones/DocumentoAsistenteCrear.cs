// ==========================================================================
//  Peticiones/DocumentoAsistenteCrear.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del POST /api/DocumentoAsistente (RF103): agrega un documento a un
//  asistente que YA existe (por eso aquí sí va 'fk_asistente').
//
//  Si el asistente ya tiene un documento vigente y este también llega
//  'vigente: true' -> 500 (ux_documento_vigente, lo vigila la BD — D9).
//  Flujo para "cambiar" el vigente: PATCH el anterior a 'vigente: false' y
//  luego POST el nuevo.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/DocumentoAsistente.</summary>
public class DocumentoAsistenteCrear
{
    /// <summary>Asistente dueño. Obligatorio (si no existe, la FK lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Tipo de documento (v1). Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_documento' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_documento' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_tipo_documento")]
    public string? FkTipoDocumento { get; set; }

    /// <summary>Número. Obligatorio: 4 a 20 letras, dígitos o guiones.</summary>
    [Required(ErrorMessage = "El campo 'numero' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'numero' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("numero")]
    public string? Numero { get; set; }

    /// <summary>¿Vigente? Opcional (por defecto true).</summary>
    [JsonPropertyName("vigente")]
    public bool? Vigente { get; set; }
}
