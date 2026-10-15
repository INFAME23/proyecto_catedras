// ==========================================================================
//  Peticiones/DocumentoAsistenteReemplazo.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Body del PUT /api/DocumentoAsistente/{idDocumento}: REEMPLAZO COMPLETO
//  (RF104). Obligatorios: 'fk_tipo_documento', 'numero' y 'vigente'.
//  'fk_asistente' NO se reemplaza: un documento no cambia de dueño (si se
//  registró en la persona equivocada, se borra y se crea en la correcta).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/DocumentoAsistente/{idDocumento}.</summary>
public class DocumentoAsistenteReemplazo
{
    [Required(ErrorMessage = "El campo 'fk_tipo_documento' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_documento' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_tipo_documento")]
    public string? FkTipoDocumento { get; set; }

    [Required(ErrorMessage = "El campo 'numero' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'numero' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("numero")]
    public string? Numero { get; set; }

    [Required(ErrorMessage = "El campo 'vigente' es obligatorio (true o false).")]
    [JsonPropertyName("vigente")]
    public bool? Vigente { get; set; }
}
