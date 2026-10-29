// ==========================================================================
//  Peticiones/UsuarioCambiarClave.cs  ·  Proyecto Cátedras · Subversión v7.1
// --------------------------------------------------------------------------
//  Body del POST /api/Usuario/{idUsuario}/cambiar-clave (RF-S5).
//
//  Es el cambio "sabiendo la clave actual": no usa código por correo. Quien
//  olvidó su clave usa la recuperación (RecuperacionSolicitar /
//  RecuperacionRestablecer). Las dos claves llegan en TEXTO PLANO; el
//  SERVICIO verifica la actual con BCrypt.Verify y hashea la nueva (Guía IA,
//  Regla 3). La confirmación ("repite la clave nueva") es del front.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Usuario/{idUsuario}/cambiar-clave.</summary>
public class UsuarioCambiarClave
{
    [Required(ErrorMessage = "El campo 'clave_actual' es obligatorio.")]
    [JsonPropertyName("clave_actual")]
    public string? ClaveActual { get; set; }

    [Required(ErrorMessage = "El campo 'clave_nueva' es obligatorio.")]
    [MinLength(8, ErrorMessage = "El campo 'clave_nueva' debe tener al menos 8 caracteres.")]
    [JsonPropertyName("clave_nueva")]
    public string? ClaveNueva { get; set; }
}
