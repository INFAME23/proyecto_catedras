// ==========================================================================
//  Peticiones/RecuperacionSolicitar.cs  ·  Proyecto Cátedras · Subversión v7.1
// --------------------------------------------------------------------------
//  Body del POST /api/Usuario/recuperacion/solicitar (RF-S5 / RF-S6).
//
//  · identificador: el nombre de usuario O el correo de la persona.
//  · ip (texto, el servicio la valida con IPAddress.TryParse -> 400): opcional.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Usuario/recuperacion/solicitar.</summary>
public class RecuperacionSolicitar
{
    [Required(ErrorMessage = "El campo 'identificador' es obligatorio.")]
    [StringLength(254, MinimumLength = 1,
        ErrorMessage = "El campo 'identificador' debe tener entre 1 y 254 caracteres.")]
    [JsonPropertyName("identificador")]
    public string? Identificador { get; set; }

    [StringLength(45, MinimumLength = 1,
        ErrorMessage = "El campo 'ip' debe tener entre 1 y 45 caracteres.")]
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }
}
