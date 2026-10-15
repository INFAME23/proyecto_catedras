// ==========================================================================
//  Modelos/NovedadCarga.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.novedad_carga': una fila del archivo que la carga
//  masiva RECHAZÓ, con el motivo. Es el detalle de 'lote_carga_asistente'.
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.7):
//    id_novedad       BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_lote_carga    BIGINT        NOT NULL  FK -> lote_carga_asistente (CASCADE)
//    numero_fila      INTEGER       NOT NULL  CHECK (numero_fila > 0)
//    contenido_crudo  TEXT          NOT NULL  (la fila tal como llegó)
//    motivo           VARCHAR(200)  NOT NULL
//
//  Nace ÚNICAMENTE dentro de 'sp_guardar_lote_carga' (al cerrar un lote):
//  la API no tiene POST propio para esta tabla (RF118).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una fila rechazada por una carga masiva.</summary>
public class NovedadCarga
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_novedad")]
    public long IdNovedad { get; set; }

    /// <summary>Lote al que pertenece.</summary>
    [JsonPropertyName("fk_lote_carga")]
    public required long FkLoteCarga { get; set; }

    /// <summary>Número de la fila en el archivo (empieza en 1).</summary>
    [JsonPropertyName("numero_fila")]
    public required int NumeroFila { get; set; }

    /// <summary>La fila tal como llegó, para poder reprocesarla a mano.</summary>
    [JsonPropertyName("contenido_crudo")]
    public required string ContenidoCrudo { get; set; }

    /// <summary>Por qué se rechazó (hasta 200 caracteres).</summary>
    [JsonPropertyName("motivo")]
    public required string Motivo { get; set; }
}
