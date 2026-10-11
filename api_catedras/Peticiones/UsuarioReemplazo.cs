// ==========================================================================
//  Peticiones/UsuarioReemplazo.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del PUT /api/Usuario/{idUsuario}: REEMPLAZO COMPLETO (RF135).
//
//  Obligatorias TODAS las columnas que el cliente controla: fk_asistente,
//  usuario, clave y activo. Un PUT reemplaza el recurso entero, así que
//  también fija una clave nueva (el servicio la vuelve a hashear). Para
//  cambiar solo un dato sin tocar la clave se usa el PATCH.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Usuario/{idUsuario}.</summary>
public class UsuarioReemplazo
{
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    [Required(ErrorMessage = "El campo 'usuario' es obligatorio.")]
    [RegularExpression("^[a-z0-9._-]{4,40}$",
        ErrorMessage = "El campo 'usuario' debe tener de 4 a 40 caracteres: minúsculas, dígitos, punto, guion y guion bajo.")]
    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    /// <summary>Clave en texto plano, obligatoria en el PUT (mínimo 8 caracteres).</summary>
    [Required(ErrorMessage = "El campo 'clave' es obligatorio.")]
    [MinLength(8, ErrorMessage = "El campo 'clave' debe tener al menos 8 caracteres.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }

    /// <summary>¿Cuenta activa? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
