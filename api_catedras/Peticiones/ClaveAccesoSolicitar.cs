// ==========================================================================
//  Peticiones/ClaveAccesoSolicitar.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/ClaveAcceso/solicitar (RF149). Junto con
//  ClaveAccesoValidar es la ÚNICA petición de esta entidad (ADR D24).
//
//  · identificador: código ASIS, correo o número de documento. Lo resuelve la
//    BD (fn_resolver_asistente); aquí solo se exige que venga algo.
//  · token: el uuid del enlace vigente. Llega como TEXTO y lo convierte el
//    SERVICIO con Guid.TryParse (400 si no es un uuid).
//  · ip: opcional, como TEXTO; el SERVICIO la valida con IPAddress.TryParse
//    (400 si no es una IP). La BD la recibe como 'inet'.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/ClaveAcceso/solicitar.</summary>
public class ClaveAccesoSolicitar
{
    [Required(ErrorMessage = "El campo 'identificador' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'identificador' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("identificador")]
    public string? Identificador { get; set; }

    [Required(ErrorMessage = "El campo 'token' es obligatorio.")]
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [StringLength(45, MinimumLength = 1,
        ErrorMessage = "El campo 'ip' debe tener entre 1 y 45 caracteres.")]
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }
}
