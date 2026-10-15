// ==========================================================================
//  Modelos/ProgramaAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.programa_asistente': la matrícula de una persona
//  en un programa académico, por periodo.
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.5):
//    fk_asistente  BIGINT       PK (1/3)  FK -> asistente (CASCADE)
//    fk_programa   VARCHAR(5)   PK (2/3)  FK -> programa_academico (v2, RESTRICT)
//    fk_periodo    CHAR(6)      PK (3/3)  FK -> periodo_academico (v1, RESTRICT)
//    estado        VARCHAR(15)  NOT NULL  DEFAULT 'ACTIVO'
//                  CHECK (estado IN ('ACTIVO','RETIRADO','GRADUADO','APLAZADO'))
//
//  Igual que VinculacionAsistente: LLAVE COMPUESTA, sin columna 'id' (ADR D16).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Matrícula de un asistente en un programa, en un periodo.</summary>
public class ProgramaAsistente
{
    /// <summary>Asistente matriculado. Parte 1 de la llave.</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Código del programa (v2). Parte 2 de la llave.</summary>
    [JsonPropertyName("fk_programa")]
    public required string FkPrograma { get; set; }

    /// <summary>Periodo académico (v1), p. ej. '2026-1'. Parte 3 de la llave.</summary>
    [JsonPropertyName("fk_periodo")]
    public required string FkPeriodo { get; set; }

    /// <summary>Estado de la matrícula. Por defecto 'ACTIVO'.</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "ACTIVO";
}
