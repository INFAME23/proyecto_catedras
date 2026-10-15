// ==========================================================================
//  Modelos/RespuestaEncuesta.cs  ·  Proyecto Cátedras · Versión 6
// --------------------------------------------------------------------------
//  Una fila de 'catedras.respuesta_encuesta'.
//
//  Definición real de la tabla (ver version6/5_data_model.md §2.3):
//    id_respuesta   BIGINT       PK  GENERATED ALWAYS AS IDENTITY
//    fk_registro    BIGINT       NOT NULL  FK -> registro_asistencia (v5), CASCADE
//                                UNIQUE -> uq_respuesta_registro (1 respuesta por registro, máximo)
//    fk_encuesta    BIGINT       NOT NULL  FK -> encuesta (v1), RESTRICT
//                                la fija el SP desde 'sesion.fk_encuesta'; el cliente NUNCA la manda
//    respondida_en  TIMESTAMPTZ  NOT NULL  DEFAULT now()
//    completa       BOOLEAN      NOT NULL  DEFAULT false
//
//  'IdRespuesta', 'FkEncuesta', 'RespondidaEn' y 'Completa' NO llevan
//  'required': los pone el SP 'fn_responder_encuesta_api'/'sp_cerrar_encuesta'
//  (ADR D31), nunca el cliente.
//
//  FK ENTRANTES: respuesta_item.fk_respuesta -> ON DELETE CASCADE.
//
//  Es un HECHO auditable (ADR D32, igual que ConsentimientoDatos en v3 y
//  RegistroAsistencia en v5): sin PUT, PATCH ni DELETE.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>La respuesta (completa) que un asistente dio a la encuesta de una sesión.</summary>
public class RespuestaEncuesta
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_respuesta")]
    public long IdRespuesta { get; set; }

    /// <summary>Registro de asistencia que respondió. Obligatorio, único (una respuesta por registro).</summary>
    [JsonPropertyName("fk_registro")]
    public required long FkRegistro { get; set; }

    /// <summary>Encuesta respondida. SIN 'required': la fija el SP desde la sesión del registro, no el cliente.</summary>
    [JsonPropertyName("fk_encuesta")]
    public long FkEncuesta { get; set; }

    /// <summary>Cuándo se respondió (lo pone la BD con now()). De solo lectura en la API.</summary>
    [JsonPropertyName("respondida_en")]
    public DateTimeOffset RespondidaEn { get; set; }

    /// <summary>¿Quedaron todas las preguntas obligatorias respondidas? Lo marca 'sp_cerrar_encuesta' (D31).</summary>
    [JsonPropertyName("completa")]
    public bool Completa { get; set; }
}
