// ==========================================================================
//  Modelos/Rol.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.rol'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.6):
//    id_rol       VARCHAR(20)   NOT NULL  PK
//    nombre       VARCHAR(60)   NOT NULL  UNIQUE (uq_rol_nombre)
//    descripcion  VARCHAR(200)  (admite NULL)
//
//  [SOLID · S] Solo representa la fila. Sin reglas de negocio propias:
//  mismo nivel que Sede.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un rol del sistema de seguridad (p. ej. Administrador, Consulta).</summary>
public class Rol
{
    /// <summary>Código único del rol. Clave primaria. 1 a 20 caracteres.</summary>
    [JsonPropertyName("id_rol")]
    public required string IdRol { get; set; }

    /// <summary>Nombre visible del rol. Obligatorio y único en la tabla.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Descripción del rol. Opcional (nulo permitido).</summary>
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}
