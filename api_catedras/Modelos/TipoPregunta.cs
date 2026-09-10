// ==========================================================================
//  Modelos/TipoPregunta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.tipo_pregunta'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.9):
//    id_tipo_pregunta  VARCHAR(15)  NOT NULL  PK
//    nombre            VARCHAR(60)  NOT NULL      -- SIN restricción de unicidad
//    guarda_numero     BOOLEAN      NOT NULL  DEFAULT false
//    guarda_texto      BOOLEAN      NOT NULL  DEFAULT false
//    guarda_opcion     BOOLEAN      NOT NULL  DEFAULT false
//    CHECK (guarda_numero::int + guarda_texto::int + guarda_opcion::int = 1)
//                                                   -- chk_tipo_pregunta_destino
//
//  [SOLID · S] Solo representa la fila. La regla de coherencia (EXACTAMENTE
//  una de las tres en true) NO se valida aquí: vive en el SERVICIO
//  (3_plan.md §4.8), porque cruza TRES propiedades.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Clase de pregunta de encuesta: dice en cuál columna de
/// 'respuesta_item' se guarda la respuesta (número, texto u opción).</summary>
public class TipoPregunta
{
    /// <summary>Código único del tipo de pregunta. Clave primaria. 1 a 15 caracteres.</summary>
    [JsonPropertyName("id_tipo_pregunta")]
    public required string IdTipoPregunta { get; set; }

    /// <summary>Nombre visible del tipo de pregunta. Obligatorio (no único).</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>¿Guarda la respuesta como número? Por defecto false.</summary>
    [JsonPropertyName("guarda_numero")]
    public bool GuardaNumero { get; set; } = false;

    /// <summary>¿Guarda la respuesta como texto? Por defecto false.</summary>
    [JsonPropertyName("guarda_texto")]
    public bool GuardaTexto { get; set; } = false;

    /// <summary>¿Guarda la respuesta como opción? Por defecto false.
    /// Exactamente una de las tres debe ser true (chk_tipo_pregunta_destino).</summary>
    [JsonPropertyName("guarda_opcion")]
    public bool GuardaOpcion { get; set; } = false;
}
