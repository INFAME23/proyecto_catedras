// ==========================================================================
//  Modelos/Modalidad.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.modalidad'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.4):
//    id_modalidad    VARCHAR(15)  NOT NULL  PRIMARY KEY
//    nombre          VARCHAR(60)  NOT NULL
//    canal_difusion  VARCHAR(10)  NOT NULL  CHECK IN ('QR','ENLACE','AMBOS')
//    activo          BOOLEAN      NOT NULL  DEFAULT true
//
//  [SOLID · S] Solo representa la fila; no valida ni se persiste a sí
//  misma. La validación de forma vive en 'Peticiones/'.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una modalidad: forma en que se dicta una sesión (presencial,
/// virtual...) y el canal por el que se difunde (QR, enlace o ambos).</summary>
public class Modalidad
{
    /// <summary>Código único de la modalidad. Clave primaria. 1 a 15 caracteres.</summary>
    [JsonPropertyName("id_modalidad")]
    public required string IdModalidad { get; set; }

    /// <summary>Nombre visible de la modalidad. Obligatorio.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Canal por el que se difunde: "QR", "ENLACE" o "AMBOS".</summary>
    [JsonPropertyName("canal_difusion")]
    public required string CanalDifusion { get; set; }

    /// <summary>¿La modalidad está activa? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
