// ==========================================================================
//  Peticiones/PreguntaInicial.cs  ·  Proyecto Cátedras · Maestro-detalle en el formulario
// --------------------------------------------------------------------------
//  Un elemento de la lista 'preguntas' del POST /api/Encuesta (Guía IA,
//  Regla 1: la encuesta, sus preguntas y las opciones de cada pregunta en
//  una sola petición, SP 'sp_guardar_encuesta', migración 043).
//
//  Es 'PreguntaCrear' SIN 'fk_encuesta': una pregunta inicial pertenece
//  SIEMPRE a la encuesta que se está creando (mismo criterio que
//  'OpcionInicial'). Y trae, a su vez, su propia lista 'opciones' (dos
//  niveles de detalle), con el mismo tipo 'OpcionInicial' de la v6.
//
//  "valor_min/valor_max: ambos o ninguno y max > min" cruza dos campos: lo
//  valida el SERVICIO (400), con la MISMA regla de ServicioPregunta.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Pregunta que nace junto con su encuesta (elemento de 'preguntas' en el POST).</summary>
public class PreguntaInicial
{
    /// <summary>Texto de la pregunta. Obligatorio, 1 a 300 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'enunciado' es obligatorio.")]
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enunciado' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enunciado")]
    public string? Enunciado { get; set; }

    /// <summary>Posición dentro de la encuesta. Obligatoria; repetida -> uq_pregunta_ord (500).</summary>
    [Required(ErrorMessage = "El campo 'orden' es obligatorio.")]
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'orden' debe ser mayor o igual que 1.")]
    [JsonPropertyName("orden")]
    public short? Orden { get; set; }

    /// <summary>Tipo de pregunta (v1). Obligatorio; un código inexistente lo rechaza la FK (500).</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_pregunta' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_pregunta' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_tipo_pregunta")]
    public string? FkTipoPregunta { get; set; }

    /// <summary>¿Es obligatoria para cerrar la encuesta? Opcional (por defecto false).</summary>
    [JsonPropertyName("obligatoria")]
    public bool? Obligatoria { get; set; }

    /// <summary>Valor mínimo esperado. Opcional; si llega, 'valor_max' también.</summary>
    [JsonPropertyName("valor_min")]
    public short? ValorMin { get; set; }

    /// <summary>Valor máximo esperado. Opcional; mayor que 'valor_min'.</summary>
    [JsonPropertyName("valor_max")]
    public short? ValorMax { get; set; }

    /// <summary>Opciones de la pregunta (segundo nivel). Opcional: null o [] = sin opciones.</summary>
    [JsonPropertyName("opciones")]
    public List<OpcionInicial>? Opciones { get; set; }
}
