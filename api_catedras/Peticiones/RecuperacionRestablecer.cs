// ==========================================================================
//  Peticiones/RecuperacionRestablecer.cs  ·  Proyecto Cátedras · Subversión v7.1
// --------------------------------------------------------------------------
//  Body del POST /api/Usuario/recuperacion/restablecer (RF-S5 / RF-S6).
//
//  · id_codigo: el que devolvió "solicitar".
//  · codigo: lo que la persona recibió por correo. Como la clave de acceso de
//    la v5, NO se valida su forma aquí: si no coincide la BD lo rechaza y no
//    conviene dar pistas.
//  · clave_nueva: texto plano, mínimo 8. El servicio la hashea con BCrypt.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Usuario/recuperacion/restablecer.</summary>
public class RecuperacionRestablecer
{
    [Required(ErrorMessage = "El campo 'id_codigo' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'id_codigo' debe ser mayor que 0.")]
    [JsonPropertyName("id_codigo")]
    public long? IdCodigo { get; set; }

    [Required(ErrorMessage = "El campo 'codigo' es obligatorio.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'codigo' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("codigo")]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "El campo 'clave_nueva' es obligatorio.")]
    [MinLength(8, ErrorMessage = "El campo 'clave_nueva' debe tener al menos 8 caracteres.")]
    [JsonPropertyName("clave_nueva")]
    public string? ClaveNueva { get; set; }
}
