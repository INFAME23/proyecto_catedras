// ==========================================================================
//  Modelos/LoteCargaAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.lote_carga_asistente': UNA ejecución de la carga
//  masiva del archivo de personas del ASIS. Sus 3 contadores son la
//  evidencia de qué se aceptó y qué no (ADR D15).
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.1):
//    id_lote_carga     BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    nombre_archivo    VARCHAR(200)  NOT NULL
//    cargado_en        TIMESTAMPTZ   NOT NULL  DEFAULT now()
//    fk_usuario        BIGINT        FK -> usuario (v5). SIEMPRE NULL en v3.
//    filas_leidas      INTEGER       NOT NULL  DEFAULT 0
//    filas_aceptadas   INTEGER       NOT NULL  DEFAULT 0
//    filas_rechazadas  INTEGER       NOT NULL  DEFAULT 0
//    observaciones     TEXT
//    CHECK chk_lote_carga_filas: aceptadas + rechazadas <= leidas (y >= 0)
//
//  "Abierto" o "cerrado" NO es una columna: un lote recién abierto tiene
//  los 3 contadores en 0 y 'observaciones' en null. La propiedad calculada
//  'EstaCerrado' aplica esa convención (version3/5_data_model.md §2.1).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una ejecución de la carga masiva de asistentes.</summary>
public class LoteCargaAsistente
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_lote_carga")]
    public long IdLoteCarga { get; set; }

    /// <summary>Nombre del archivo que se cargó.</summary>
    [JsonPropertyName("nombre_archivo")]
    public required string NombreArchivo { get; set; }

    /// <summary>Cuándo se abrió el lote (lo pone la BD). De solo lectura.</summary>
    [JsonPropertyName("cargado_en")]
    public DateTime CargadoEn { get; set; }

    /// <summary>Usuario que hizo la carga. Siempre null hasta la v5 (no existe 'usuario').</summary>
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    /// <summary>Filas que tenía el archivo.</summary>
    [JsonPropertyName("filas_leidas")]
    public int FilasLeidas { get; set; }

    /// <summary>Filas que se guardaron como asistente.</summary>
    [JsonPropertyName("filas_aceptadas")]
    public int FilasAceptadas { get; set; }

    /// <summary>Filas que la API rechazó (cada una tiene su novedad).</summary>
    [JsonPropertyName("filas_rechazadas")]
    public int FilasRechazadas { get; set; }

    /// <summary>Comentario libre del cierre. Opcional.</summary>
    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }

    /// <summary>¿Ya se cerró? Calculado (no es columna): algún contador mayor
    /// que 0 o 'observaciones' con valor. '=>' define una propiedad de solo
    /// lectura que se recalcula cada vez que se consulta.</summary>
    [JsonPropertyName("cerrado")]
    public bool EstaCerrado =>
        FilasLeidas > 0 || FilasAceptadas > 0 || FilasRechazadas > 0 || Observaciones is not null;
}
