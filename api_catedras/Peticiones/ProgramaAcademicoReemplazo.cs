// ==========================================================================
//  Peticiones/ProgramaAcademicoReemplazo.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del PUT /api/ProgramaAcademico/{codigo}: REEMPLAZO COMPLETO.
//  'nombre', 'nivel' y 'activo' obligatorios (falta uno -> 422).
//  'fk_facultad' se escribe siempre: si no llega, queda NULL.
//  El 'codigo' viaja en la URL (la PK no se cambia).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/ProgramaAcademico/{codigo}.</summary>
public class ProgramaAcademicoReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, 1 a 150 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva facultad. Si no llega o es null -> el programa queda sin facultad.</summary>
    [StringLength(10, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_facultad' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("fk_facultad")]
    public string? FkFacultad { get; set; }

    /// <summary>Nivel. Obligatorio en el PUT, de la lista cerrada.</summary>
    [Required(ErrorMessage = "El campo 'nivel' es obligatorio.")]
    [RegularExpression("^(PREGRADO|ESPECIALIZACION|MAESTRIA|DOCTORADO|EDUCACION_CONTINUA|OTRO)$",
        ErrorMessage = "El campo 'nivel' debe ser PREGRADO, ESPECIALIZACION, MAESTRIA, DOCTORADO, EDUCACION_CONTINUA u OTRO.")]
    [JsonPropertyName("nivel")]
    public string? Nivel { get; set; }

    /// <summary>¿Está activo? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
