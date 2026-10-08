// ==========================================================================
//  Peticiones/AsistenteReemplazo.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del PUT /api/Asistente/{idAsistente}: REEMPLAZO COMPLETO de las
//  columnas PROPIAS del asistente (RF98). NO toca sus detalles: documentos,
//  vinculaciones, programas y consentimientos tienen sus propios recursos.
//
//  Obligatorios: 'nombres', 'apellidos', 'es_externo' y 'activo' (falta
//  uno -> 422). Los opcionales que no lleguen quedan en NULL (es un PUT).
//  'fk_lote_carga' y 'creado_en' no se reemplazan: son la historia de
//  cómo nació la fila.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Asistente/{idAsistente}.</summary>
public class AsistenteReemplazo
{
    [RegularExpression("^[0-9A-Za-z]{5,15}$",
        ErrorMessage = "El campo 'id_asis' debe tener entre 5 y 15 letras o dígitos.")]
    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    [Required(ErrorMessage = "El campo 'nombres' es obligatorio.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'nombres' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    [Required(ErrorMessage = "El campo 'apellidos' es obligatorio.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'apellidos' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_completo_asis' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre_completo_asis")]
    public string? NombreCompletoAsis { get; set; }

    [StringLength(254, ErrorMessage = "El campo 'correo_institucional' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        ErrorMessage = "El campo 'correo_institucional' no tiene formato de correo.")]
    [JsonPropertyName("correo_institucional")]
    public string? CorreoInstitucional { get; set; }

    [StringLength(254, ErrorMessage = "El campo 'correo_personal' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        ErrorMessage = "El campo 'correo_personal' no tiene formato de correo.")]
    [JsonPropertyName("correo_personal")]
    public string? CorreoPersonal { get; set; }

    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'celular' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("celular")]
    public string? Celular { get; set; }

    /// <summary>¿Es externo? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'es_externo' es obligatorio (true o false).")]
    [JsonPropertyName("es_externo")]
    public bool? EsExterno { get; set; }

    /// <summary>¿Está activo? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
