// ==========================================================================
//  Modelos/EstadoProceso.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.estado_proceso'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.3):
//    id_estado    VARCHAR(12)  NOT NULL  PRIMARY KEY
//    nombre       VARCHAR(40)  NOT NULL
//    es_final     BOOLEAN      NOT NULL  DEFAULT false
//    es_exitoso   BOOLEAN      NOT NULL  DEFAULT false
//    CHECK (es_exitoso = false OR es_final = true)  -- chk_estado_coherente
//
//  [SOLID · S] Solo representa la fila. La regla de coherencia
//  (es_exitoso implica es_final) NO se valida aquí: vive en el SERVICIO
//  (3_plan.md §4.8), porque cruza dos propiedades.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un estado del Monitor de Procesos: los pasos por los que
/// atraviesa un lote de migración (generado, en cola, correcto...).</summary>
public class EstadoProceso
{
    /// <summary>Código único del estado. Clave primaria. 1 a 12 caracteres.</summary>
    [JsonPropertyName("id_estado")]
    public required string IdEstado { get; set; }

    /// <summary>Nombre visible del estado. Obligatorio.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>¿Es un estado final (no cambia más)? Por defecto false.</summary>
    [JsonPropertyName("es_final")]
    public bool EsFinal { get; set; } = false;

    /// <summary>¿Terminó con éxito? Por defecto false. Si es true, "es_final"
    /// también debe serlo (chk_estado_coherente).</summary>
    [JsonPropertyName("es_exitoso")]
    public bool EsExitoso { get; set; } = false;
}
