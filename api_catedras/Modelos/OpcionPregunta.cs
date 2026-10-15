// ==========================================================================
//  Modelos/OpcionPregunta.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Una fila de 'catedras.opcion_pregunta'.
//
//  Definición real de la tabla (ver version6/5_data_model.md §2.2):
//    id_opcion    BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_pregunta  BIGINT        NOT NULL  FK -> pregunta, CASCADE
//                               UNIQUE (fk_pregunta, orden) -> uq_opcion_orden
//    etiqueta     VARCHAR(150)  NOT NULL
//    valor        SMALLINT      NULO       (peso numérico opcional, p. ej. para promediar)
//    orden        SMALLINT      NOT NULL
//
//  'IdOpcion' NO lleva 'required': lo pone la BD.
//
//  FK ENTRANTES: respuesta_item.fk_opcion -> ON DELETE RESTRICT (una
//  opción ya elegida en alguna respuesta de ejemplo no se puede borrar).
//
//  A diferencia de 'AliasPrograma' (v2) o 'ConsentimientoDatos' (v3), esta
//  tabla SÍ tiene molde completo de 5 verbos (RF167-RF171, D33): corregir
//  la etiqueta de una opción o reordenarla es una edición legítima, no
//  "una nueva versión del hecho".
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una opción de respuesta de una pregunta de tipo OPCION_UNICA.</summary>
public class OpcionPregunta
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_opcion")]
    public long IdOpcion { get; set; }

    /// <summary>Pregunta a la que pertenece. Obligatoria.</summary>
    [JsonPropertyName("fk_pregunta")]
    public required long FkPregunta { get; set; }

    /// <summary>Texto de la opción. Obligatorio, hasta 150 caracteres.</summary>
    [JsonPropertyName("etiqueta")]
    public required string Etiqueta { get; set; }

    /// <summary>Peso numérico opcional de la opción (p. ej. para promediar respuestas).</summary>
    [JsonPropertyName("valor")]
    public short? Valor { get; set; }

    /// <summary>Posición dentro de la pregunta. Obligatoria, única junto con 'fk_pregunta'.</summary>
    [JsonPropertyName("orden")]
    public required short Orden { get; set; }
}
