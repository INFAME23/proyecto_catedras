// ==========================================================================
//  Peticiones/UsuarioCrear.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/Usuario (RF134).
//
//  Obligatorios: fk_asistente, usuario, clave.
//  Opcional:     activo (por defecto true).
//
//  'clave' llega en TEXTO PLANO (mínimo 8 caracteres). Esta clase NO lleva
//  [JsonIgnore]: es la petición, y el cliente tiene que poder mandarla. Quien
//  la convierte en hash BCrypt es el SERVICIO (ADR D23, RNF13); el cliente
//  jamás manda ni lee un 'clave_hash'.
//
//  Si 'usuario' ya existe, o la persona ya tiene cuenta, la BD lo rechaza
//  (UNIQUE) y la API responde 500 (ADR D9): aquí no se vuelve a revisar.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Usuario.</summary>
public class UsuarioCrear
{
    /// <summary>Persona dueña de la cuenta (v3). Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Nombre de usuario. Obligatorio; refleja el CHECK de la tabla.</summary>
    [Required(ErrorMessage = "El campo 'usuario' es obligatorio.")]
    [RegularExpression("^[a-z0-9._-]{4,40}$",
        ErrorMessage = "El campo 'usuario' debe tener de 4 a 40 caracteres: minúsculas, dígitos, punto, guion y guion bajo.")]
    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    /// <summary>Clave en texto plano. Obligatoria, mínimo 8 caracteres. El servicio la hashea.</summary>
    [Required(ErrorMessage = "El campo 'clave' es obligatorio.")]
    [MinLength(8, ErrorMessage = "El campo 'clave' debe tener al menos 8 caracteres.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }

    /// <summary>¿Cuenta activa? Opcional (por defecto true).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>v7.1 · D54: true = en su primer ingreso debe cambiar la clave que le entregó el administrador. Opcional (por defecto false).</summary>
    [JsonPropertyName("debe_cambiar_clave")]
    public bool? DebeCambiarClave { get; set; }
}
