// ==========================================================================
//  Modelos/Encuesta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.encuesta'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.2):
//    id_encuesta    BIGINT   NOT NULL  PK  GENERATED ALWAYS AS IDENTITY
//    nombre         VARCHAR(120)  NOT NULL
//    version        SMALLINT      NOT NULL  DEFAULT 1  CHECK (version >= 1)
//    vigente_desde  DATE          NOT NULL  DEFAULT CURRENT_DATE
//    vigente_hasta  DATE          (admite NULL)
//    activa         BOOLEAN       NOT NULL  DEFAULT true
//    UNIQUE (nombre, version)                        -- uq_encuesta_ver
//    CHECK (vigente_hasta IS NULL OR vigente_hasta >= vigente_desde)
//                                                     -- chk_encuesta_vig
//
//  [SOLID · S] Solo representa la fila. 'IdEncuesta' NO lleva 'required':
//  a diferencia de Sede, la BD lo genera — el cliente nunca lo asigna
//  (3_plan.md §4.7). La regla de coherencia de fechas vive en el SERVICIO.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una encuesta: cuestionario de calidad en una versión concreta.</summary>
public class Encuesta
{
    /// <summary>Identificador autogenerado por la BD. El cliente nunca lo manda.</summary>
    [JsonPropertyName("id_encuesta")]
    public long IdEncuesta { get; set; }

    /// <summary>Nombre del cuestionario. Obligatorio.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Número de versión. Por defecto 1; debe ser >= 1.</summary>
    [JsonPropertyName("version")]
    public short Version { get; set; } = 1;

    /// <summary>Fecha desde la que rige esta versión. Por defecto, hoy.</summary>
    [JsonPropertyName("vigente_desde")]
    public DateOnly VigenteDesde { get; set; }

    /// <summary>Fecha hasta la que rige (opcional: null = sigue vigente).</summary>
    [JsonPropertyName("vigente_hasta")]
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>¿Está activa? Por defecto true.</summary>
    [JsonPropertyName("activa")]
    public bool Activa { get; set; } = true;
}
