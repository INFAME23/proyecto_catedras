// ==========================================================================
//  Modelos/ConsentimientoDatos.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.consentimiento_datos'. Ley 1581 de 2012: el
//  consentimiento para tratar datos personales es un HECHO que se registra,
//  no un aviso en pantalla. Por eso la API solo lo lista, lo obtiene y lo
//  crea: nunca lo edita ni lo borra (RF113).
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.6):
//    id_consentimiento  BIGINT       PK  GENERATED ALWAYS AS IDENTITY
//    fk_asistente       BIGINT       NOT NULL  FK -> asistente (CASCADE)
//    version_politica   VARCHAR(20)  NOT NULL
//    aceptado_en        TIMESTAMPTZ  NOT NULL  DEFAULT now()
//    ip                 INET         (dirección IP; opcional)
//    canal              VARCHAR(20)  NOT NULL  DEFAULT 'WEB'
//                       CHECK (canal IN ('WEB','PRESENCIAL','CORREO','IMPORTADO'))
//    UNIQUE (fk_asistente, version_politica)   -- uq_consentimiento_ver
//
//  'ip' es de tipo INET en la BD; en C# se maneja como TEXTO (string). El
//  repositorio hace la conversión en el SQL (ip::text al leer, CAST(... AS
//  inet) al escribir).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Consentimiento de tratamiento de datos que aceptó un asistente.</summary>
public class ConsentimientoDatos
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_consentimiento")]
    public long IdConsentimiento { get; set; }

    /// <summary>Asistente que aceptó.</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Versión de la política aceptada (p. ej. 'v1.0').</summary>
    [JsonPropertyName("version_politica")]
    public required string VersionPolitica { get; set; }

    /// <summary>Cuándo se aceptó (lo pone la BD). De solo lectura.</summary>
    [JsonPropertyName("aceptado_en")]
    public DateTime AceptadoEn { get; set; }

    /// <summary>IP desde la que se aceptó, como texto. Opcional.</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Canal por el que se aceptó. Por defecto 'WEB'.</summary>
    [JsonPropertyName("canal")]
    public string Canal { get; set; } = "WEB";
}
