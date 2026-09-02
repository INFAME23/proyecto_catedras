// ==========================================================================
//  Modelos/Sede.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una clase de C# que representa UNA FILA de la
//  tabla 'catedras.sede'. Es lo que viaja entre capas y lo que la API
//  devuelve en el JSON de las lecturas.
//
//  Definición real de la tabla (ver 5_data_model.md y el script provisto):
//    id_sede    VARCHAR(15)  NOT NULL  PRIMARY KEY
//    nombre     VARCHAR(80)  NOT NULL  UNIQUE
//    direccion  VARCHAR(200)           (admite NULL)
//    es_virtual BOOLEAN      NOT NULL  DEFAULT false
//    activo     BOOLEAN      NOT NULL  DEFAULT true
//
//  IMPORTANTE (Artículo 1, YAGNI): la v1 SOLO puede nombrar la tabla 'sede'.
//  Las otras 36 tablas del esquema existen pero son territorio de v2+.
//
//  [SOLID · S] Esta clase SOLO representa la fila. No se valida, no se
//  persiste a sí misma, no sabe de HTTP. Los DTO de entrada viven aparte en
//  'Peticiones/' (un contrato por verbo) -> el modelo no se infla con
//  campos que solo sirven a un verbo.
//  [ACID · C] Los valores por defecto (EsVirtual=false, Activo=true) y el
//  'required' reflejan las reglas de la tabla, pero la Consistencia REAL la
//  impone PostgreSQL, no este objeto.
// ==========================================================================

// 'namespace' agrupa el código; el punto marca la carpeta.
namespace ApiCatedras.Modelos;

// Atributo para renombrar propiedades en el JSON.
using System.Text.Json.Serialization;

/// <summary>
/// Una sede: lugar físico o virtual donde se dicta una sesión.
/// </summary>
public class Sede
{
    // En C#, "{ get; set; }" SON los getter/setter del lenguaje: no hay que
    // escribirlos a mano. 'required' obliga a asignar la propiedad al crear
    // el objeto (no puede quedar sin valor).

    /// <summary>Código único de la sede. Clave primaria. 1 a 15 caracteres.</summary>
    [JsonPropertyName("id_sede")]              // en el JSON la clave se llama "id_sede"
    public required string IdSede { get; set; }

    /// <summary>Nombre visible de la sede. Obligatorio y único en la tabla.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Dirección física. Opcional: la sede "VIRTUAL" no tiene.</summary>
    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }     // el '?' = puede ser null

    /// <summary>¿La sede es virtual? Por defecto false.</summary>
    [JsonPropertyName("es_virtual")]
    public bool EsVirtual { get; set; } = false;

    /// <summary>¿La sede está activa? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
