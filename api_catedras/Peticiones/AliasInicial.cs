// ==========================================================================
//  Peticiones/AliasInicial.cs  ·  Proyecto Cátedras · Retrofit V2
// --------------------------------------------------------------------------
//  Un elemento de la lista 'alias' del POST /api/ProgramaAcademico (Guía
//  IA, Regla 1: maestro-detalle en una sola petición).
//
//  Se parece a 'AliasProgramaCrear', pero NO tiene 'fk_programa': un alias
//  inicial pertenece SIEMPRE al programa que se está creando en la misma
//  petición, así que el código lo pone el servicio (no el cliente). Si
//  un cliente manda 'fk_programa' aquí, el deserializador lo ignora (no
//  hay propiedad donde guardarlo).
//
//  ASP.NET Core valida estas anotaciones también DENTRO de la lista: un
//  alias inválido -> 422 con el campo 'alias[0].texto_normalizado', y la
//  BD no se toca.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;   // [Required], [StringLength]
using System.Text.Json.Serialization;          // [JsonPropertyName]

/// <summary>Alias que nace junto con su programa (elemento de 'alias' en el POST).</summary>
public class AliasInicial
{
    /// <summary>Texto normalizado. Obligatorio, 1 a 200 caracteres, único en la tabla (lo vigila la BD).</summary>
    [Required(ErrorMessage = "El campo 'texto_normalizado' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'texto_normalizado' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("texto_normalizado")]
    public string? TextoNormalizado { get; set; }

    /// <summary>Origen del alias. Opcional (por defecto 'FORMULARIO'), hasta 30 caracteres.</summary>
    [StringLength(30, MinimumLength = 1,
        ErrorMessage = "El campo 'origen' debe tener entre 1 y 30 caracteres.")]
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }
}
