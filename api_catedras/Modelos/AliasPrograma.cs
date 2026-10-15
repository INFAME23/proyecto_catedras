// ==========================================================================
//  Modelos/AliasPrograma.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Una fila de 'catedras.alias_programa': un texto libre con que alguien
//  escribió un programa en un formulario, y el código al que corresponde.
//
//  Definición real de la tabla (ver version2/5_data_model.md §2.4):
//    id_alias           BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    texto_normalizado  VARCHAR(200)  NOT NULL  UNIQUE (uq_alias_texto)
//    fk_programa        VARCHAR(5)    NOT NULL  FK -> programa_academico
//                                               (ON DELETE CASCADE)
//    origen             VARCHAR(30)   NOT NULL  DEFAULT 'FORMULARIO'
//    creado_en          TIMESTAMPTZ   NOT NULL  DEFAULT now()
//
//  'IdAlias' y 'CreadoEn' NO llevan 'required': los pone la BD, el cliente
//  nunca los manda (igual que 'id_encuesta' en la v1).
//
//  (La v2, ADR D12, la trataba como solo-anexo; el RETROFIT criterio 3 le
//  agregó PUT y PATCH para poder corregir un alias sin borrarlo.)
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Variante de texto libre con que se escribió un programa.</summary>
public class AliasPrograma
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_alias")]
    public long IdAlias { get; set; }

    /// <summary>El texto tal como se normalizó. Obligatorio y único.</summary>
    [JsonPropertyName("texto_normalizado")]
    public required string TextoNormalizado { get; set; }

    /// <summary>Código del programa al que corresponde el alias.</summary>
    [JsonPropertyName("fk_programa")]
    public required string FkPrograma { get; set; }

    /// <summary>De dónde salió el alias. Por defecto 'FORMULARIO'.</summary>
    [JsonPropertyName("origen")]
    public string Origen { get; set; } = "FORMULARIO";

    /// <summary>Cuándo se registró (lo pone la BD con now()). De solo lectura en la API.
    /// 'DateTime' en UTC: es lo que Npgsql entrega para una columna TIMESTAMPTZ.</summary>
    [JsonPropertyName("creado_en")]
    public DateTime CreadoEn { get; set; }
}
