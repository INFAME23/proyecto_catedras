// ==========================================================================
//  Peticiones/RolPorUsuarioCrear.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/RolPorUsuario (RF141): ABRE un rol para un usuario.
//  Nace sin 'fecha_fin' (vigente). La misma llave exacta (usuario + rol +
//  fecha_ini) ya existente -> 500 (PK duplicada). Usuario o rol inexistente
//  -> 500 (FK).
//
//  Tabla de LLAVE COMPUESTA (ADR D16): esta entidad NO tiene peticiones
//  'Reemplazo' ni 'Actualizar'. Esa ausencia ES la especificación: la
//  historia de roles no se reemplaza, solo se cierra (RolPorUsuarioCerrar).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/RolPorUsuario.</summary>
public class RolPorUsuarioCrear
{
    /// <summary>Usuario que recibe el rol. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_usuario' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    /// <summary>Código del rol (catálogo v1: ADMIN, CONSULTA...). Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_rol' es obligatorio.")]
    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_rol' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("fk_rol")]
    public string? FkRol { get; set; }

    /// <summary>Fecha de inicio "AAAA-MM-DD". Opcional (por defecto, hoy).</summary>
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }
}
