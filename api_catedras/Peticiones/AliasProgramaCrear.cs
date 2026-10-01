// ==========================================================================
//  Peticiones/AliasProgramaCrear.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del POST /api/AliasPrograma. Es la ÚNICA petición de esta entidad:
//  no existen 'AliasProgramaReemplazo' ni 'AliasProgramaActualizar' porque
//  la tabla es de solo-anexo (ADR D12). La ausencia ES la especificación.
//
//  'id_alias' y 'creado_en' NO están aquí: los pone la BD. Si un cliente
//  los manda en el JSON, el deserializador los ignora (no hay propiedad
//  donde guardarlos), así que nunca llegan al INSERT.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/AliasPrograma.</summary>
public class AliasProgramaCrear
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

    /// <summary>Origen del alias. Opcional (por defecto 'FORMULARIO'), hasta 30 caracteres.</summary>
    [StringLength(30, MinimumLength = 1,
        ErrorMessage = "El campo 'origen' debe tener entre 1 y 30 caracteres.")]
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }
}
