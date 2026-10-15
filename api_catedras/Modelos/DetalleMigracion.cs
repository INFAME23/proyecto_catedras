// ==========================================================================
//  Modelos/DetalleMigracion.cs  ·  Proyecto Cátedras · v7 (adelantada en el RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Una fila de 'catedras.detalle_migracion': UN registro de asistencia tal
//  como salió en el archivo de un lote, y el resultado que dio el ASIS.
//  Nace únicamente dentro de sp_generar_lote_migracion (RF189), igual que
//  novedad_carga nace dentro del cierre de su lote.
//
//  Definición real de la tabla (version7/5_data_model.md §2.2):
//    id_detalle         BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_lote_migracion  BIGINT        NOT NULL  FK -> lote_migracion (CASCADE)
//    fk_registro        BIGINT        NOT NULL  FK -> registro_asistencia (RESTRICT)
//    id_asis_enviado    VARCHAR(15)   NOT NULL  (COPIA LITERAL de lo enviado)
//    programa_enviado   VARCHAR(5)              (COPIA LITERAL)
//    reunion_enviada    SMALLINT      NOT NULL  (COPIA LITERAL)
//    resultado          VARCHAR(12)   NOT NULL  DEFAULT 'PENDIENTE'
//                       CHECK (resultado IN ('PENDIENTE','ACEPTADO','RECHAZADO'))
//    mensaje            VARCHAR(300)
//    UNIQUE (fk_lote_migracion, fk_registro)              -- uq_detalle_lote_reg
//    UNIQUE (fk_registro) WHERE resultado = 'ACEPTADO'    -- ux_detmig_aceptado
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una fila enviada en un lote de migración, con su resultado.</summary>
public class DetalleMigracion
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_detalle")]
    public long IdDetalle { get; set; }

    /// <summary>Lote al que pertenece.</summary>
    [JsonPropertyName("fk_lote_migracion")]
    public required long FkLoteMigracion { get; set; }

    /// <summary>Registro de asistencia que se envió.</summary>
    [JsonPropertyName("fk_registro")]
    public required long FkRegistro { get; set; }

    /// <summary>ID ASIS tal como salió en el archivo (lo copia la BD).</summary>
    [JsonPropertyName("id_asis_enviado")]
    public string IdAsisEnviado { get; set; } = "";

    /// <summary>Código de programa tal como salió en el archivo.</summary>
    [JsonPropertyName("programa_enviado")]
    public string? ProgramaEnviado { get; set; }

    /// <summary>Número de reunión tal como salió en el archivo (lo copia la BD).</summary>
    [JsonPropertyName("reunion_enviada")]
    public short ReunionEnviada { get; set; }

    /// <summary>PENDIENTE, ACEPTADO o RECHAZADO. Por defecto PENDIENTE.</summary>
    [JsonPropertyName("resultado")]
    public string Resultado { get; set; } = "PENDIENTE";

    /// <summary>Motivo, si el ASIS rechazó la fila. Opcional.</summary>
    [JsonPropertyName("mensaje")]
    public string? Mensaje { get; set; }
}
