// ==========================================================================
//  Modelos/TipoVinculacion.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.tipo_vinculacion'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.10):
//    id_tipo_vinculacion  VARCHAR(15)  NOT NULL  PK
//    nombre               VARCHAR(60)  NOT NULL  UNIQUE (uq_tipo_vinculacion_nom)
//    es_interno           BOOLEAN      NOT NULL  -- SIN DEFAULT: obligatorio en el POST
//    exige_programa       BOOLEAN      NOT NULL  DEFAULT true
//    activo               BOOLEAN      NOT NULL  DEFAULT true
//
//  [SOLID · S] Solo representa la fila. Sin reglas de coherencia entre
//  campos (a diferencia de EstadoProceso/TipoPregunta): la única
//  particularidad es que 'EsInterno' no tiene valor por defecto en C#
//  ('= false' NO refleja la BD, que exige el valor explícito).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Tipo de vinculación de un asistente con la universidad
/// (docente, estudiante, egresado, externo...).</summary>
public class TipoVinculacion
{
    /// <summary>Código único del tipo de vinculación. Clave primaria. 1 a 15 caracteres.</summary>
    [JsonPropertyName("id_tipo_vinculacion")]
    public required string IdTipoVinculacion { get; set; }

    /// <summary>Nombre visible del tipo de vinculación. Obligatorio y único.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>¿Es una vinculación interna a la universidad? Sin valor por
    /// defecto en la BD: siempre se asigna explícitamente.</summary>
    [JsonPropertyName("es_interno")]
    public bool EsInterno { get; set; }

    /// <summary>¿Exige tener un programa académico asociado? Por defecto true.</summary>
    [JsonPropertyName("exige_programa")]
    public bool ExigePrograma { get; set; } = true;

    /// <summary>¿Está activo? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
