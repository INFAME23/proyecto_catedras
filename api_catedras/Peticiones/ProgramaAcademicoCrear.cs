// ==========================================================================
//  Peticiones/ProgramaAcademicoCrear.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del POST /api/ProgramaAcademico.
//
//  Dos comprobaciones de FORMA nuevas, ambas con [RegularExpression]
//  (mismo criterio que 'canal_difusion' de Modalidad en la v1):
//   · 'codigo': una letra MAYÚSCULA + 3 o 4 alfanuméricos en mayúscula
//     ("M0286" pasa, "m0286" -> 422). Refleja el CHECK chk_programa_codigo.
//   · 'nivel': lista CERRADA de 6 valores (chk_programa_nivel). La BD lo
//     guarda como texto, así que aquí también es texto, no un 'enum' de C#.
//  Un fallo de forma -> 422 y la BD no se toca.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/ProgramaAcademico.</summary>
public class ProgramaAcademicoCrear
{
    /// <summary>Código. Obligatorio; formato: letra + 3 o 4 alfanuméricos, en mayúscula.</summary>
    [Required(ErrorMessage = "El campo 'codigo' es obligatorio.")]
    [RegularExpression("^[A-Z][0-9A-Z]{3,4}$",
        ErrorMessage = "El campo 'codigo' debe ser una letra mayúscula seguida de 3 o 4 letras mayúsculas o dígitos (p. ej. 'M0286').")]
    [JsonPropertyName("codigo")]
    public string? Codigo { get; set; }

    /// <summary>Nombre. Obligatorio, 1 a 150 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Facultad. Opcional; si llega, 1 a 10 caracteres (la FK la vigila la BD).</summary>
    [StringLength(10, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_facultad' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("fk_facultad")]
    public string? FkFacultad { get; set; }

    /// <summary>Nivel. Opcional (por defecto PREGRADO); si llega, debe estar en la lista.</summary>
    [RegularExpression("^(PREGRADO|ESPECIALIZACION|MAESTRIA|DOCTORADO|EDUCACION_CONTINUA|OTRO)$",
        ErrorMessage = "El campo 'nivel' debe ser PREGRADO, ESPECIALIZACION, MAESTRIA, DOCTORADO, EDUCACION_CONTINUA u OTRO.")]
    [JsonPropertyName("nivel")]
    public string? Nivel { get; set; }

    /// <summary>¿Está activo? Opcional (por defecto true).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
