// ==========================================================================
//  Modelos/RespuestaItem.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Una fila de 'catedras.respuesta_item'.
//
//  Definición real de la tabla (ver version6/5_data_model.md §2.4):
//    id_item         BIGINT   PK  GENERATED ALWAYS AS IDENTITY
//    fk_respuesta    BIGINT   NOT NULL  FK -> respuesta_encuesta, CASCADE
//                             UNIQUE (fk_respuesta, fk_pregunta) -> uq_respuesta_item
//    fk_pregunta     BIGINT   NOT NULL  FK -> pregunta, RESTRICT
//    valor_numerico  SMALLINT NULO      exactamente 1 de los 3 (chk_respuesta_item_un_valor)
//    valor_texto     TEXT     NULO
//    fk_opcion       BIGINT   NULO      FK -> opcion_pregunta, RESTRICT
//
//  Ningún campo lleva 'required': esta entidad es de SOLO LECTURA (RF178)
//  — nace exclusivamente dentro de "responder" (RF174), igual criterio que
//  'NovedadCarga' en v3. No existe 'RespuestaItemCrear': no hace falta.
//
//  SIN FK ENTRANTES: hoja del grafo, como 'AliasPrograma' (v2) o
//  'NovedadCarga' (v3).
//
//  El trigger 'trg_respuesta_item_tipo' (RN-24/RN-25) vigila que la opción
//  elegida pertenezca a la pregunta y que la columna usada coincida con el
//  tipo de la pregunta — la API no lo revalida (D9, ver §4.4 de version6/3_plan.md).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una respuesta a una pregunta concreta, dentro de una 'RespuestaEncuesta'.</summary>
public class RespuestaItem
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_item")]
    public long IdItem { get; set; }

    /// <summary>Respuesta de encuesta a la que pertenece.</summary>
    [JsonPropertyName("fk_respuesta")]
    public long FkRespuesta { get; set; }

    /// <summary>Pregunta que este ítem responde.</summary>
    [JsonPropertyName("fk_pregunta")]
    public long FkPregunta { get; set; }

    /// <summary>Respuesta numérica (preguntas ESCALA_5/SI_NO). Exactamente 1 de los 3 campos viene lleno.</summary>
    [JsonPropertyName("valor_numerico")]
    public short? ValorNumerico { get; set; }

    /// <summary>Respuesta de texto libre (preguntas TEXTO_LIBRE).</summary>
    [JsonPropertyName("valor_texto")]
    public string? ValorTexto { get; set; }

    /// <summary>Opción elegida (preguntas OPCION_UNICA).</summary>
    [JsonPropertyName("fk_opcion")]
    public long? FkOpcion { get; set; }
}
