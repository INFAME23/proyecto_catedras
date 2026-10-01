// ==========================================================================
//  Peticiones/FacultadCrear.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del POST /api/Facultad: CREAR una facultad nueva.
//
//  Aquí SOLO se valida la FORMA de cada campo (obligatorio, longitud,
//  tipo). Si algo falla, ASP.NET responde 422 ANTES de entrar al
//  controlador (formato definido en Program.cs) y la BD no se toca.
//
//  Lo que NO se valida aquí: que 'fk_facultad_padre' sea distinto de
//  'id_facultad'. Esa regla CRUZA dos campos del mismo body -> es de
//  NEGOCIO y le toca al servicio (ServicioFacultad -> 400). Tampoco se
//  valida que el padre exista: eso lo garantiza la FK de la BD (-> 500,
//  decisión D9 de version2/4_research.md).
// ==========================================================================

namespace ApiCatedras.Peticiones;

// Espacio donde viven [Required], [StringLength], etc.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Facultad.</summary>
public class FacultadCrear
{
    /// <summary>Código de la facultad. Obligatorio, 1 a 10 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_facultad' es obligatorio.")]
    [StringLength(10, MinimumLength = 1,
        ErrorMessage = "El campo 'id_facultad' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("id_facultad")]
    public string? IdFacultad { get; set; }

    /// <summary>Nombre. Obligatorio, 1 a 120 caracteres (único en la tabla).</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(120, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 120 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Código de la facultad padre. Opcional (null = raíz).</summary>
    [StringLength(10, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_facultad_padre' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("fk_facultad_padre")]
    public string? FkFacultadPadre { get; set; }

    /// <summary>¿Está activa? Opcional: si no llega, true (DEFAULT de la columna).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }   // 'bool?': un texto o número aquí -> 422 sin escribir un 'if'
}
