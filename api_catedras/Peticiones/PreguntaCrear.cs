// ==========================================================================
//  Peticiones/PreguntaCrear.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Body del POST /api/Pregunta (RF161).
//
//  'fk_tipo_pregunta' lleva SOLO [Required] + [StringLength], SIN
//  [RegularExpression]: a diferencia de 'nivel' en ProgramaAcademico (v2,
//  una lista cerrada sin tabla detrás), 'tipo_pregunta' SÍ es una FK real
//  a una tabla de la v1 — un código que no exista lo rechaza el motor
//  (500), la API no revalida la lista aquí (D9).
//
//  'valor_min'/'valor_max' llegan SIN anotación que cruce los dos campos:
//  "ambos o ninguno" y "max > min" son reglas de NEGOCIO (cruzan dos
//  campos), así que las valida el SERVICIO -> 400 (§4.3 de
//  version6/3_plan.md), igual criterio que 'Sesion.fin/inicio' (v4).
//
//  Retrofit V6 (Guía IA, Regla 1 · maestro-detalle): el body acepta además
//  una lista OPCIONAL 'opciones' con las opciones iniciales de la
//  pregunta. Si llega, pregunta y opciones se guardan en UNA sola
//  transacción mediante el SP 'sp_guardar_pregunta'. Si no llega (o llega
//  vacía), el POST se comporta como una pregunta sin opciones (p. ej.
//  ESCALA_5 o TEXTO_LIBRE).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Pregunta.</summary>
public class PreguntaCrear
{
    /// <summary>Encuesta a la que pertenece. Obligatoria (la existencia la vigila la FK -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_encuesta' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_encuesta' debe ser mayor que 0.")]
    [JsonPropertyName("fk_encuesta")]
    public long? FkEncuesta { get; set; }

    /// <summary>Texto de la pregunta. Obligatorio, 1 a 300 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'enunciado' es obligatorio.")]
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enunciado' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enunciado")]
    public string? Enunciado { get; set; }

    /// <summary>Posición dentro de la encuesta. Obligatoria; '(fk_encuesta, orden)' duplicado -> 500.</summary>
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

    /// <summary>Valor mínimo esperado. Opcional; si llega, 'valor_max' también debe llegar (servicio, 400).</summary>
    [JsonPropertyName("valor_min")]
    public short? ValorMin { get; set; }

    /// <summary>Valor máximo esperado. Opcional; si llega, debe ser mayor que 'valor_min' (servicio, 400).</summary>
    [JsonPropertyName("valor_max")]
    public short? ValorMax { get; set; }

    /// <summary>Opciones iniciales. Opcional: null o [] = pregunta sin opciones (p. ej. ESCALA_5).
    /// Cada elemento se valida con las anotaciones de <see cref="OpcionInicial"/>.</summary>
    [JsonPropertyName("opciones")]
    public List<OpcionInicial>? Opciones { get; set; }
}
