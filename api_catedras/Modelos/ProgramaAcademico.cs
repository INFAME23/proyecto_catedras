// ==========================================================================
//  Modelos/ProgramaAcademico.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Una fila de 'catedras.programa_academico'.
//
//  Definición real de la tabla (ver version2/5_data_model.md §2.3):
//    codigo       VARCHAR(5)    NOT NULL  PRIMARY KEY
//                 CHECK (codigo ~ '^[A-Z][0-9A-Z]{3,4}$')   -- p. ej. M0286
//    nombre       VARCHAR(150)  NOT NULL  (NO es único)
//    fk_facultad  VARCHAR(10)             FK -> facultad (ON DELETE SET NULL)
//    nivel        VARCHAR(25)   NOT NULL  DEFAULT 'PREGRADO'  (lista cerrada)
//    activo       BOOLEAN       NOT NULL  DEFAULT true
//
//  FK ENTRANTES (las aplica el motor al borrar un programa):
//    alias_programa.fk_programa       -> ON DELETE CASCADE
//    programa_asistente.fk_programa   -> ON DELETE RESTRICT (v3, ya sembrada)
//  RESTRICT gana: un programa con asistentes NO se puede borrar (500).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un programa académico (pregrado, maestría...).</summary>
public class ProgramaAcademico
{
    /// <summary>Código del programa. Clave primaria (una letra + 3 o 4 alfanuméricos).</summary>
    [JsonPropertyName("codigo")]
    public required string Codigo { get; set; }

    /// <summary>Nombre del programa. Obligatorio.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Facultad a la que pertenece. Opcional.</summary>
    [JsonPropertyName("fk_facultad")]
    public string? FkFacultad { get; set; }

    /// <summary>Nivel de formación. Por defecto 'PREGRADO'.</summary>
    [JsonPropertyName("nivel")]
    public string Nivel { get; set; } = "PREGRADO";

    /// <summary>¿Está activo? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
