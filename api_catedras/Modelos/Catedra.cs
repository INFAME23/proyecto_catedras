// ==========================================================================
//  Modelos/Catedra.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Una fila de 'catedras.catedra': el CATÁLOGO de cátedras (5502 de ejemplo).
//
//  Definición real de la tabla (ver version2/5_data_model.md §2.5):
//    id_catedra      BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    id_evento_asis  CHAR(9)       NOT NULL UNIQUE  CHECK ('^[0-9]{9}$')
//    nombre          VARCHAR(200)  NOT NULL
//    nombre_asis     VARCHAR(30)   GENERATED ALWAYS AS (left(nombre, 30)) STORED
//    fk_tipo_evento  CHAR(4)       NOT NULL  FK -> tipo_evento (v1, RESTRICT)
//    fk_dependencia  VARCHAR(15)             FK -> dependencia (SET NULL)
//    activo          BOOLEAN       NOT NULL  DEFAULT true
//    creado_en       TIMESTAMPTZ   NOT NULL  DEFAULT now()
//
//  NOVEDAD DE LA v2 — COLUMNA GENERADA (RNF7): 'nombre_asis' la calcula la
//  BD a partir de 'nombre'. Vive SOLO aquí, en el modelo de respuesta;
//  ninguna clase de 'Peticiones/' la declara, así que el cliente no puede
//  escribirla. Tres propiedades las pone la BD: IdCatedra, NombreAsis y
//  CreadoEn (por eso no llevan 'required').
//
//  'IdEventoAsis' es TEXTO, no número: '000023636' como entero perdería los
//  ceros a la izquierda y dejaría de servir para la carga al ASIS.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una cátedra del catálogo (el "evento" del ASIS).</summary>
public class Catedra
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_catedra")]
    public long IdCatedra { get; set; }

    /// <summary>Código del evento en el ASIS: 9 dígitos con ceros a la izquierda.</summary>
    [JsonPropertyName("id_evento_asis")]
    public required string IdEventoAsis { get; set; }

    /// <summary>Nombre completo de la cátedra (hasta 200 caracteres).</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Primeros 30 caracteres del nombre. Columna GENERADA por la BD: solo lectura.</summary>
    [JsonPropertyName("nombre_asis")]
    public string NombreAsis { get; set; } = "";

    /// <summary>Código del tipo de evento (tabla de la v1). Obligatorio.</summary>
    [JsonPropertyName("fk_tipo_evento")]
    public required string FkTipoEvento { get; set; }

    /// <summary>Dependencia que organiza la cátedra. Opcional.</summary>
    [JsonPropertyName("fk_dependencia")]
    public string? FkDependencia { get; set; }

    /// <summary>¿Está activa? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>Cuándo se registró (DEFAULT now() de la BD). Solo lectura, en UTC.</summary>
    [JsonPropertyName("creado_en")]
    public DateTime CreadoEn { get; set; }
}
