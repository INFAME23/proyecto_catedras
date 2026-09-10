// ==========================================================================
//  Modelos/TipoDocumento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.tipo_documento'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.7):
//    id_tipo_documento  VARCHAR(5)   NOT NULL  PK
//    nombre             VARCHAR(60)  NOT NULL  UNIQUE (uq_tipo_documento_nom)
//    activo             BOOLEAN      NOT NULL  DEFAULT true
//
//  [SOLID · S] Solo representa la fila. Sin reglas de negocio propias:
//  mismo nivel que Sede/Modalidad.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una clase de documento de identidad (cédula, pasaporte, etc.).</summary>
public class TipoDocumento
{
    /// <summary>Código único del tipo de documento. Clave primaria. 1 a 5 caracteres.</summary>
    [JsonPropertyName("id_tipo_documento")]
    public required string IdTipoDocumento { get; set; }

    /// <summary>Nombre visible del tipo de documento. Obligatorio y único.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>¿Está activo? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
