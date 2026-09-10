// ==========================================================================
//  Peticiones/RolCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/Rol: CREAR un rol nuevo.
//  Regla del POST: todos los campos obligatorios salvo 'descripcion',
//  que la columna admite NULL (igual que 'direccion' en Sede).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Rol.</summary>
public class RolCrear
{
    /// <summary>Código del rol. Obligatorio, 1 a 20 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_rol' es obligatorio.")]
    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'id_rol' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("id_rol")]
    public string? IdRol { get; set; }

    /// <summary>Nombre del rol. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Descripción del rol. Opcional, hasta 200 caracteres.</summary>
    [StringLength(200,
        ErrorMessage = "El campo 'descripcion' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}
