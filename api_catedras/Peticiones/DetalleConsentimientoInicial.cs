// ==========================================================================
//  Peticiones/DetalleConsentimientoInicial.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  El bloque 'consentimiento' del POST /api/Asistente (Guía IA, Regla 1).
//  Es UN objeto, no una lista: una persona nueva acepta como máximo la
//  política vigente en ese momento. Sin 'fk_asistente' (lo fija el SP).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Consentimiento que nace junto con su asistente.</summary>
public class DetalleConsentimientoInicial
{
    /// <summary>Versión de la política aceptada. Obligatoria, 1 a 20 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'version_politica' es obligatorio.")]
    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'version_politica' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("version_politica")]
    public string? VersionPolitica { get; set; }

    /// <summary>Canal. Opcional (por defecto WEB); lista cerrada.</summary>
    [RegularExpression("^(WEB|PRESENCIAL|CORREO|IMPORTADO)$",
        ErrorMessage = "El campo 'canal' debe ser WEB, PRESENCIAL, CORREO o IMPORTADO.")]
    [JsonPropertyName("canal")]
    public string? Canal { get; set; }

    /// <summary>IP desde la que se aceptó. Opcional; una IP mal escrita la rechaza la BD (500).</summary>
    [StringLength(45, MinimumLength = 1,
        ErrorMessage = "El campo 'ip' debe tener entre 1 y 45 caracteres.")]
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }
}
