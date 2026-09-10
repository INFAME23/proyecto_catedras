// ==========================================================================
//  Modelos/PeriodoAcademico.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.periodo_academico'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.5):
//    id_periodo  CHAR(6)  NOT NULL  PK   CHECK (id_periodo ~ '^[0-9]{4}-[1-3]$')
//    fecha_ini   DATE     NOT NULL  (sin DEFAULT)
//    fecha_fin   DATE     NOT NULL  (sin DEFAULT)  CHECK (fecha_fin > fecha_ini)
//    activo      BOOLEAN  NOT NULL  DEFAULT true
//
//  [SOLID · S] Solo representa la fila. La regla de coherencia
//  (fecha_fin > fecha_ini) NO se valida aquí: vive en el SERVICIO
//  (3_plan.md §4.8), porque cruza dos propiedades.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un periodo académico (p. ej. "2026-1"): rango de fechas que
/// agrupa sesiones y matrículas de un semestre.</summary>
public class PeriodoAcademico
{
    /// <summary>Código único del periodo. Clave primaria. Formato "AAAA-N" (N=1,2,3).</summary>
    [JsonPropertyName("id_periodo")]
    public required string IdPeriodo { get; set; }

    /// <summary>Fecha de inicio del periodo.</summary>
    [JsonPropertyName("fecha_ini")]
    public DateOnly FechaIni { get; set; }

    /// <summary>Fecha de fin del periodo. Debe ser posterior a "fecha_ini".</summary>
    [JsonPropertyName("fecha_fin")]
    public DateOnly FechaFin { get; set; }

    /// <summary>¿El periodo está activo? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
