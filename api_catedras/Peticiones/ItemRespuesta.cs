// ==========================================================================
//  Peticiones/ItemRespuesta.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Un elemento de la lista 'respuestas' del POST /api/RespuestaEncuesta
//  (RF174). Ver version6/2_spec.md y version6/6_contracts.md §3:
//    { "pregunta": 1, "numero": 5 }
//    { "pregunta": 3, "texto": "Muy buena la sesión" }
//    { "pregunta": 5, "opcion": 12 }
//
//  'Numero'/'Texto'/'Opcion' son los tres OPCIONALES: exactamente UNO debe
//  venir, según el tipo de la pregunta correspondiente — pero esa regla
//  NO se anota aquí (D9, igual criterio que toda FK/regla que ya vigila
//  la BD): la verifica el trigger 'fn_trg_respuesta_item_tipo' dentro del
//  mismo CALL que inserta el detalle, y si no se cumple la excepción sube
//  como NpgsqlException -> 500. Anotar "exactamente uno de los tres" aquí
//  duplicaría, sin necesidad real, una regla que la BD ya aplica completa
//  (mismo razonamiento que 'origen' en RegistroAsistenciaCrear, v5, pero
//  llevado un paso más allá: ahí SÍ había servicio; aquí ni eso hace falta).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Una respuesta, dentro de la lista 'respuestas' de <see cref="RespuestaEncuestaResponder"/>.</summary>
public class ItemRespuesta
{
    /// <summary>Pregunta que se está respondiendo. Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'pregunta' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'pregunta' debe ser mayor que 0.")]
    [JsonPropertyName("pregunta")]
    public long? Pregunta { get; set; }

    /// <summary>Respuesta numérica (preguntas ESCALA_5/SI_NO). Exactamente uno de los 3 (lo vigila la BD).</summary>
    [JsonPropertyName("numero")]
    public short? Numero { get; set; }

    /// <summary>Respuesta de texto libre (preguntas TEXTO_LIBRE).</summary>
    [JsonPropertyName("texto")]
    public string? Texto { get; set; }

    /// <summary>Opción elegida (preguntas OPCION_UNICA).</summary>
    [JsonPropertyName("opcion")]
    public long? Opcion { get; set; }
}
