// ==========================================================================
//  Peticiones/RolReemplazo.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PUT /api/Rol/{idRol}: reemplazo COMPLETO.
//  'nombre' obligatorio; 'descripcion' va en el body y admite null (igual
//  que 'direccion' en SedeReemplazo).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Rol/{idRol}.</summary>
public class RolReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva descripción. Opcional; admite null.</summary>
    [StringLength(200,
        ErrorMessage = "El campo 'descripcion' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}
