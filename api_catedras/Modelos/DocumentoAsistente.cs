// ==========================================================================
//  Modelos/DocumentoAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.documento_asistente': un documento de identidad de
//  una persona. Una persona puede tener varios a lo largo del tiempo, pero
//  SOLO UNO vigente.
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.3):
//    id_documento       BIGINT       PK  GENERATED ALWAYS AS IDENTITY
//    fk_asistente       BIGINT       NOT NULL  FK -> asistente (CASCADE)
//    fk_tipo_documento  VARCHAR(5)   NOT NULL  FK -> tipo_documento (v1, RESTRICT)
//    numero             VARCHAR(20)  NOT NULL  CHECK ('^[0-9A-Za-z-]{4,20}$')
//    vigente            BOOLEAN      NOT NULL  DEFAULT true
//    registrado_en      TIMESTAMPTZ  NOT NULL  DEFAULT now()
//
//  Dos reglas que vigila la BD (y que la API NO revalida, D9):
//    uq_documento_numero   UNIQUE (fk_tipo_documento, numero)
//    ux_documento_vigente  índice único PARCIAL: (fk_asistente) WHERE vigente
//                          -> un asistente no puede tener 2 vigentes a la vez.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un documento de identidad de un asistente.</summary>
public class DocumentoAsistente
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_documento")]
    public long IdDocumento { get; set; }

    /// <summary>Asistente dueño del documento.</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Tipo de documento (CC, TI, CE...). Catálogo de la v1.</summary>
    [JsonPropertyName("fk_tipo_documento")]
    public required string FkTipoDocumento { get; set; }

    /// <summary>Número del documento (4 a 20 alfanuméricos o guiones).</summary>
    [JsonPropertyName("numero")]
    public required string Numero { get; set; }

    /// <summary>¿Es el documento vigente? Por defecto true.</summary>
    [JsonPropertyName("vigente")]
    public bool Vigente { get; set; } = true;

    /// <summary>Cuándo se registró (lo pone la BD). De solo lectura.</summary>
    [JsonPropertyName("registrado_en")]
    public DateTime RegistradoEn { get; set; }
}
