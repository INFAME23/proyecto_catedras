// ==========================================================================
//  Peticiones/ClaveAccesoValidar.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/ClaveAcceso/validar (RF150).
//
//  · id_clave: la clave que devolvió "solicitar".
//  · clave: el texto que el asistente escribió. NO se valida su formato aquí:
//    si no coincide, la BD la rechaza y no conviene dar pistas de su forma.
//  · ip (texto, el servicio la valida con IPAddress.TryParse -> 400) y
//    user_agent: opcionales.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/ClaveAcceso/validar.</summary>
public class ClaveAccesoValidar
{
    [Required(ErrorMessage = "El campo 'id_clave' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'id_clave' debe ser mayor que 0.")]
    [JsonPropertyName("id_clave")]
    public long? IdClave { get; set; }

    [Required(ErrorMessage = "El campo 'clave' es obligatorio.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'clave' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }

    [StringLength(45, MinimumLength = 1,
        ErrorMessage = "El campo 'ip' debe tener entre 1 y 45 caracteres.")]
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [StringLength(300, ErrorMessage = "El campo 'user_agent' admite hasta 300 caracteres.")]
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }
}
