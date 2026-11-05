// ==========================================================================
//  Modelos/Sesion.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Una fila de 'catedras.sesion': UNA reunión concreta de una cátedra, en
//  una fecha, modalidad, sede y periodo (5714 de ejemplo).
//
//  Definición real de la tabla (ver version4/5_data_model.md §2.1):
//    id_sesion            BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_catedra           BIGINT        NOT NULL  FK -> catedra (v2, CASCADE)
//    numero_reunion       SMALLINT      NOT NULL  CHECK >= 1  (trigger, ADR D20)
//    numero_reunion_asis  SMALLINT      número de reunión según el ASIS
//    titulo               VARCHAR(200)
//    inicio / fin         TIMESTAMPTZ   NOT NULL  CHECK fin > inicio
//    fk_modalidad         VARCHAR(15)   NOT NULL  FK -> modalidad (v1, RESTRICT)
//    fk_sede              VARCHAR(15)   NOT NULL  FK -> sede (v1, RESTRICT)
//    fk_periodo           CHAR(6)       NOT NULL  FK -> periodo_academico (v1, RESTRICT)
//    fk_encuesta          BIGINT        FK -> encuesta (v1, SET NULL)
//    lugar                VARCHAR(200)
//    enlace_virtual       VARCHAR(300)
//    cupo                 INT           CHECK cupo IS NULL OR cupo > 0
//    estado               VARCHAR(15)   NOT NULL DEFAULT 'PROGRAMADA' (lista cerrada)
//    creado_en            TIMESTAMPTZ   NOT NULL DEFAULT now()
//
//  NOVEDAD DE LA v4 — FECHA CON HORA (ADR D21): 'inicio' y 'fin' son
//  TIMESTAMPTZ (un instante exacto). En el modelo se guardan como DateTime
//  en UTC, igual que 'CreadoEn' en todas las tablas anteriores: así Npgsql
//  los escribe y los lee sin conversiones raras. El cliente puede mandar la
//  hora con su zona ("2026-10-20T14:00:00-05:00"); el SERVICIO la pasa a UTC.
//
//  Tres propiedades las pone la BD y por eso no llevan 'required':
//  IdSesion (IDENTITY), CreadoEn (DEFAULT now()) y NumeroReunion (si llega
//  0, el trigger 'fn_trg_sesion_consecutivo' la calcula, ADR D20).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una sesión (reunión concreta) de una cátedra.</summary>
public class Sesion
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_sesion")]
    public long IdSesion { get; set; }

    /// <summary>Cátedra a la que pertenece esta reunión (tabla de la v2).</summary>
    [JsonPropertyName("fk_catedra")]
    public required long FkCatedra { get; set; }

    /// <summary>Número de reunión dentro de la cátedra. 0 al crear = "que lo calcule la BD".</summary>
    [JsonPropertyName("numero_reunion")]
    public short NumeroReunion { get; set; }

    /// <summary>Número de reunión según el ASIS (contador propio del ASIS). Opcional.</summary>
    [JsonPropertyName("numero_reunion_asis")]
    public short? NumeroReunionAsis { get; set; }

    /// <summary>Título de la sesión. Opcional.</summary>
    [JsonPropertyName("titulo")]
    public string? Titulo { get; set; }

    /// <summary>Instante de inicio, en UTC.</summary>
    [JsonPropertyName("inicio")]
    public required DateTime Inicio { get; set; }

    /// <summary>Instante de fin, en UTC. Debe ser posterior a 'inicio'.</summary>
    [JsonPropertyName("fin")]
    public required DateTime Fin { get; set; }

    /// <summary>Modalidad (tabla de la v1): PRESENCIAL, VIRTUAL...</summary>
    [JsonPropertyName("fk_modalidad")]
    public required string FkModalidad { get; set; }

    /// <summary>Sede (tabla de la v1): SAN_BENITO, BELLO, VIRTUAL...</summary>
    [JsonPropertyName("fk_sede")]
    public required string FkSede { get; set; }

    /// <summary>Periodo académico (tabla de la v1), p. ej. '2026-2'.</summary>
    [JsonPropertyName("fk_periodo")]
    public required string FkPeriodo { get; set; }

    /// <summary>Encuesta que se aplicará al cierre (tabla de la v1). Opcional.</summary>
    [JsonPropertyName("fk_encuesta")]
    public long? FkEncuesta { get; set; }

    /// <summary>Lugar físico (salón, auditorio). Opcional.</summary>
    [JsonPropertyName("lugar")]
    public string? Lugar { get; set; }

    /// <summary>Enlace de la reunión virtual. Opcional.</summary>
    [JsonPropertyName("enlace_virtual")]
    public string? EnlaceVirtual { get; set; }


    /// <summary>PROGRAMADA, ABIERTA, CERRADA o MIGRADA. Por defecto PROGRAMADA.</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "PROGRAMADA";

    /// <summary>Cuándo se registró (DEFAULT now() de la BD). Solo lectura, en UTC.</summary>
    [JsonPropertyName("creado_en")]
    public DateTime CreadoEn { get; set; }
}
