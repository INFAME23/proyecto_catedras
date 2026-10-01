// ==========================================================================
//  Modelos/Dependencia.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Una fila de 'catedras.dependencia'. Misma forma que Facultad: tiene una
//  AUTORREFERENCIA ('fk_dependencia_padre') que modela la jerarquía de
//  dependencias administrativas (BIENESTAR -> PASTORAL, DEPORTES...).
//
//  Definición real de la tabla (ver version2/5_data_model.md §2.2):
//    id_dependencia        VARCHAR(15)   NOT NULL  PRIMARY KEY
//    nombre                VARCHAR(120)  NOT NULL  UNIQUE (uq_dependencia_nombre)
//    fk_dependencia_padre  VARCHAR(15)             FK -> dependencia.id_dependencia
//                                                  (ON DELETE SET NULL)
//    activo                BOOLEAN       NOT NULL  DEFAULT true
//    CHECK (fk_dependencia_padre <> id_dependencia) -- chk_dependencia_padre
//
//  'catedra.fk_dependencia' apunta a esta tabla (también ON DELETE SET NULL):
//  borrar una dependencia NO borra cátedras, solo les deja ese campo en null.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una dependencia administrativa de la universidad.</summary>
public class Dependencia
{
    /// <summary>Código único. Clave primaria. 1 a 15 caracteres.</summary>
    [JsonPropertyName("id_dependencia")]
    public required string IdDependencia { get; set; }

    /// <summary>Nombre visible. Obligatorio y único en la tabla.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Código de la dependencia padre. Opcional: las raíces no tienen.</summary>
    [JsonPropertyName("fk_dependencia_padre")]
    public string? FkDependenciaPadre { get; set; }

    /// <summary>¿Está activa? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
