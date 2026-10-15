// ==========================================================================
//  Modelos/LoteMigracion.cs  ·  Proyecto Cátedras · v7 (adelantada en el RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Una fila de 'catedras.lote_migracion': UN intento de migrar al ASIS los
//  registros de asistencia de una sesión. Es el maestro de
//  'detalle_migracion' (mismo patrón que lote_carga_asistente / novedad_carga
//  de la v3).
//
//  Definición real de la tabla (version7/5_data_model.md §2.1):
//    id_lote_migracion       BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_sesion               BIGINT        NOT NULL  FK -> sesion (RESTRICT)
//    nombre_proceso          VARCHAR(60)   NOT NULL  DEFAULT 'ME_MIGRACION_CATEDRA'
//    nombre_archivo          VARCHAR(200)  NOT NULL  (lo calcula el SP)
//    generado_en             TIMESTAMPTZ   NOT NULL  DEFAULT now()
//    fk_usuario              BIGINT        FK -> usuario (SET NULL)
//    instancia_proceso_asis  VARCHAR(30)   (el id de ejecución que devuelve ASIS)
//    fk_estado_proceso       VARCHAR(12)   NOT NULL  DEFAULT 'GENERADO'  FK -> estado_proceso
//    total_filas             INTEGER       NOT NULL  DEFAULT 0  CHECK >= 0 (lo calcula el SP)
//    observaciones           TEXT
//
//  'NombreArchivo' y 'TotalFilas' NO llevan 'required': los calcula
//  sp_generar_lote_migracion, el cliente nunca los manda.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un lote de migración de asistencia hacia el ASIS.</summary>
public class LoteMigracion
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_lote_migracion")]
    public long IdLoteMigracion { get; set; }

    /// <summary>Sesión cuyos registros se migran.</summary>
    [JsonPropertyName("fk_sesion")]
    public required long FkSesion { get; set; }

    /// <summary>Nombre del proceso en el ASIS. Por defecto 'ME_MIGRACION_CATEDRA'.</summary>
    [JsonPropertyName("nombre_proceso")]
    public string NombreProceso { get; set; } = "ME_MIGRACION_CATEDRA";

    /// <summary>Nombre del archivo generado (lo calcula la BD).</summary>
    [JsonPropertyName("nombre_archivo")]
    public string NombreArchivo { get; set; } = "";

    /// <summary>Cuándo se generó (lo pone la BD).</summary>
    [JsonPropertyName("generado_en")]
    public DateTimeOffset GeneradoEn { get; set; }

    /// <summary>Usuario que lo generó. Opcional.</summary>
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    /// <summary>Número de instancia que devuelve el ASIS al ejecutar el proceso.</summary>
    [JsonPropertyName("instancia_proceso_asis")]
    public string? InstanciaProcesoAsis { get; set; }

    /// <summary>Estado del proceso (catálogo 'estado_proceso'). Por defecto 'GENERADO'.</summary>
    [JsonPropertyName("fk_estado_proceso")]
    public string FkEstadoProceso { get; set; } = "GENERADO";

    /// <summary>Filas migrables que entraron al lote (lo calcula la BD).</summary>
    [JsonPropertyName("total_filas")]
    public int TotalFilas { get; set; }

    /// <summary>Comentario libre. Opcional.</summary>
    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }
}
