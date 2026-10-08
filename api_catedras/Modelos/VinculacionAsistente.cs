// ==========================================================================
//  Modelos/VinculacionAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.vinculacion_asistente': la vinculación de una
//  persona con la universidad (estudiante, docente...) a lo largo del tiempo.
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.4):
//    fk_asistente         BIGINT       PK (1/3)  FK -> asistente (CASCADE)
//    fk_tipo_vinculacion  VARCHAR(15)  PK (2/3)  FK -> tipo_vinculacion (v1)
//    fecha_ini            DATE         PK (3/3)
//    fecha_fin            DATE         NULL = sigue vigente
//    CHECK chk_vinculacion_fechas: fecha_fin IS NULL OR fecha_fin >= fecha_ini
//
//  NOVEDAD DE LA v3 — LLAVE COMPUESTA (ADR D16): esta tabla NO tiene una
//  columna 'id'. La fila se identifica con sus 3 columnas de llave. Por eso
//  este modelo no tiene propiedad 'Id' y la API no ofrece '/{id}'.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una vinculación (con vigencia) de un asistente.</summary>
public class VinculacionAsistente
{
    /// <summary>Asistente vinculado. Parte 1 de la llave.</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Tipo de vinculación (catálogo v1). Parte 2 de la llave.</summary>
    [JsonPropertyName("fk_tipo_vinculacion")]
    public required string FkTipoVinculacion { get; set; }

    /// <summary>Fecha en que empezó. Parte 3 de la llave.</summary>
    [JsonPropertyName("fecha_ini")]
    public required DateOnly FechaIni { get; set; }

    /// <summary>Fecha en que terminó. Null = sigue vigente.</summary>
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }
}
