// ==========================================================================
//  Modelos/Pregunta.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Una fila de 'catedras.pregunta'.
//
//  Definición real de la tabla (ver version6/5_data_model.md §2.1):
//    id_pregunta       BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_encuesta       BIGINT        NOT NULL  FK -> encuesta (v1), CASCADE
//                                    UNIQUE (fk_encuesta, orden) -> uq_pregunta_ord
//    enunciado         VARCHAR(300)  NOT NULL
//    orden             SMALLINT      NOT NULL  CHECK >= 1
//    fk_tipo_pregunta  VARCHAR(15)   NOT NULL  FK -> tipo_pregunta (v1), RESTRICT
//    obligatoria       BOOLEAN       NOT NULL  DEFAULT false
//    valor_min         SMALLINT      NULO      CHECK ambos o ninguno, max > min
//    valor_max         SMALLINT      NULO
//
//  'IdPregunta' NO lleva 'required': lo pone la BD (igual que 'id_sesion'
//  en la v1), el cliente nunca lo manda.
//
//  FK ENTRANTES (las aplica el motor):
//    opcion_pregunta.fk_pregunta  -> ON DELETE CASCADE
//    respuesta_item.fk_pregunta   -> ON DELETE RESTRICT
//  RESTRICT gana: una pregunta con respuestas de ejemplo NO se puede
//  borrar (500), y la cascada hacia sus opciones no llega a ejecutarse.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una pregunta de una encuesta de calidad.</summary>
public class Pregunta
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_pregunta")]
    public long IdPregunta { get; set; }

    /// <summary>Encuesta a la que pertenece. Obligatoria.</summary>
    [JsonPropertyName("fk_encuesta")]
    public required long FkEncuesta { get; set; }

    /// <summary>Texto de la pregunta. Obligatorio, hasta 300 caracteres.</summary>
    [JsonPropertyName("enunciado")]
    public required string Enunciado { get; set; }

    /// <summary>Posición dentro de la encuesta. Obligatoria, única junto con 'fk_encuesta'.</summary>
    [JsonPropertyName("orden")]
    public required short Orden { get; set; }

    /// <summary>Tipo de pregunta (v1): decide qué columna de 'respuesta_item' se espera.</summary>
    [JsonPropertyName("fk_tipo_pregunta")]
    public required string FkTipoPregunta { get; set; }

    /// <summary>¿Hay que responderla sí o sí para poder cerrar la encuesta? Por defecto false.</summary>
    [JsonPropertyName("obligatoria")]
    public bool Obligatoria { get; set; } = false;

    /// <summary>Valor mínimo esperado (declarativo: lo vigila el trigger de 'respuesta_item', no esta tabla).</summary>
    [JsonPropertyName("valor_min")]
    public short? ValorMin { get; set; }

    /// <summary>Valor máximo esperado. Ambos o ninguno, y siempre mayor que 'valor_min'.</summary>
    [JsonPropertyName("valor_max")]
    public short? ValorMax { get; set; }
}
