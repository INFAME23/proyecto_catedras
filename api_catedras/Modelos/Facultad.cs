// ==========================================================================
//  Modelos/Facultad.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una clase de C# que representa UNA FILA de la
//  tabla 'catedras.facultad'. Es lo que viaja entre capas y lo que la API
//  devuelve en el JSON de las lecturas.
//
//  Definición real de la tabla (ver version2/5_data_model.md §2.1):
//    id_facultad        VARCHAR(10)   NOT NULL  PRIMARY KEY
//    nombre             VARCHAR(120)  NOT NULL  UNIQUE (uq_facultad_nombre)
//    fk_facultad_padre  VARCHAR(10)             FK -> facultad.id_facultad
//                                               (ON DELETE SET NULL)
//    activo             BOOLEAN       NOT NULL  DEFAULT true
//    CHECK (fk_facultad_padre <> id_facultad)   -- chk_facultad_padre
//
//  NOVEDAD DE LA v2 — AUTORREFERENCIA: 'fk_facultad_padre' apunta a la MISMA
//  tabla. Así se modela una jerarquía (USB es la raíz; F_ING, F_ARTES...
//  son sus hijas). La regla "una facultad no puede ser su propio padre" la
//  valida el SERVICIO (no este modelo): ver ServicioFacultad.
//
//  [SOLID · S] Esta clase SOLO representa la fila: no se valida, no se
//  guarda a sí misma y no sabe de HTTP.
// ==========================================================================

// 'namespace' agrupa el código; el punto marca la carpeta.
namespace ApiCatedras.Modelos;

// Atributo [JsonPropertyName] para renombrar propiedades en el JSON.
using System.Text.Json.Serialization;

/// <summary>Una facultad (o la universidad raíz) dentro de la jerarquía académica.</summary>
public class Facultad
{
    /// <summary>Código único de la facultad. Clave primaria. 1 a 10 caracteres.</summary>
    [JsonPropertyName("id_facultad")]          // en el JSON la clave se llama "id_facultad"
    public required string IdFacultad { get; set; } // 'required' = hay que darle valor al crear el objeto

    /// <summary>Nombre visible. Obligatorio y único en la tabla.</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>Código de la facultad padre. Opcional: la raíz (USB) no tiene padre.</summary>
    [JsonPropertyName("fk_facultad_padre")]
    public string? FkFacultadPadre { get; set; } // el '?' = puede ser null

    /// <summary>¿Está activa? Por defecto true (igual que el DEFAULT de la columna).</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
