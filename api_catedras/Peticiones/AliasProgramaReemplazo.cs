// ==========================================================================
//  Peticiones/AliasProgramaReemplazo.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Body del PUT /api/AliasPrograma/{idAlias}: REEMPLAZO COMPLETO de las 3
//  columnas editables. 'texto_normalizado' y 'fk_programa' son obligatorios
//  (falta uno -> 422). 'origen' es opcional: si no llega queda en
//  'FORMULARIO' (el DEFAULT de la columna, que no admite NULL).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/AliasPrograma/{idAlias}.</summary>
public class AliasProgramaReemplazo
{
    /// <summary>Texto normalizado. Obligatorio, 1 a 200 caracteres, único en la tabla.</summary>
    [Required(ErrorMessage = "El campo 'texto_normalizado' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'texto_normalizado' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("texto_normalizado")]
    public string? TextoNormalizado { get; set; }

    /// <summary>Código del programa. Obligatorio (si no existe, la FK lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_programa' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_programa' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_programa")]
    public string? FkPrograma { get; set; }

    /// <summary>Origen. Opcional (por defecto 'FORMULARIO'), hasta 30 caracteres.</summary>
    [StringLength(30, MinimumLength = 1,
        ErrorMessage = "El campo 'origen' debe tener entre 1 y 30 caracteres.")]
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }
}
