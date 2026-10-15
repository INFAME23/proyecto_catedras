// ==========================================================================
//  Peticiones/OpcionInicial.cs  ·  Proyecto Cátedras · Retrofit V6
// --------------------------------------------------------------------------
//  Un elemento de la lista 'opciones' del POST /api/Pregunta (Guía IA,
//  Regla 1: maestro-detalle en una sola petición). Ver version6/2_spec.md
//  RF161 y version6/3_plan.md §4.1.
//
//  Se parece a 'OpcionPreguntaCrear', pero NO tiene 'fk_pregunta': una
//  opción inicial pertenece SIEMPRE a la pregunta que se está creando en
//  la misma petición, así que el id lo pone el servicio (no el cliente) —
//  mismo criterio exacto que 'AliasInicial' de v2. Si un cliente manda
//  'fk_pregunta' aquí, el deserializador lo ignora (no hay propiedad donde
//  guardarlo).
//
//  ASP.NET Core valida estas anotaciones también DENTRO de la lista: una
//  opción inválida -> 422 con el campo 'opciones[0].etiqueta', y la BD no
//  se toca.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Opción que nace junto con su pregunta (elemento de 'opciones' en el POST).</summary>
public class OpcionInicial
{
    /// <summary>Texto de la opción. Obligatorio, 1 a 150 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'etiqueta' es obligatorio.")]
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'etiqueta' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("etiqueta")]
    public string? Etiqueta { get; set; }

    /// <summary>Peso numérico opcional (p. ej. para promediar respuestas de opción única).</summary>
    [JsonPropertyName("valor")]
    public short? Valor { get; set; }

    /// <summary>Posición dentro de la pregunta. Obligatoria, igual molde que 'Pregunta.Orden'.</summary>
    [Required(ErrorMessage = "El campo 'orden' es obligatorio.")]
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }
}
