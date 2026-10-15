// ==========================================================================
//  Peticiones/UsuarioLogin.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/Usuario/login (RF139).
//
//  Solo pide que ambos campos EXISTAN. No valida el formato de 'usuario' ni
//  el largo de 'clave' a propósito (ADR D27): si lo hiciera, un usuario mal
//  escrito daría 422 y uno bien escrito pero inexistente daría 401, y esa
//  diferencia le contaría a un atacante qué formato es válido. Cualquier
//  credencial que no sirva debe terminar en el MISMO 401 genérico.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Usuario/login.</summary>
public class UsuarioLogin
{
    /// <summary>Nombre de usuario. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'usuario' es obligatorio.")]
    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    /// <summary>Clave en texto plano. Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'clave' es obligatorio.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }
}
