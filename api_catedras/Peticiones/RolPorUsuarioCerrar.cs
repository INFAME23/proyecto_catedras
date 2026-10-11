// ==========================================================================
//  Peticiones/RolPorUsuarioCerrar.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del PATCH /api/RolPorUsuario/cerrar (RF142).
//
//  Como la tabla no tiene 'id' (ADR D16), la fila a cerrar se identifica
//  con su LLAVE COMPLETA, que viaja en el body: 'fk_usuario', 'fk_rol' y
//  'fecha_ini'. Más 'fecha_fin' (por defecto hoy).
//
//  Reglas del SERVICIO (no de forma):
//   · llave inexistente -> 404
//   · ya estaba cerrado -> 400 (no se "re-cierra" algo que ya terminó)
//   · fecha_fin anterior a fecha_ini -> 400
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/RolPorUsuario/cerrar.</summary>
public class RolPorUsuarioCerrar
{
    /// <summary>Llave (1/3): usuario.</summary>
    [Required(ErrorMessage = "El campo 'fk_usuario' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    /// <summary>Llave (2/3): rol.</summary>
    [Required(ErrorMessage = "El campo 'fk_rol' es obligatorio.")]
    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_rol' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("fk_rol")]
    public string? FkRol { get; set; }

    /// <summary>Llave (3/3): fecha de inicio "AAAA-MM-DD".</summary>
    [Required(ErrorMessage = "El campo 'fecha_ini' es obligatorio (parte de la llave).")]
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }

    /// <summary>Fecha de cierre "AAAA-MM-DD". Opcional (por defecto, hoy).</summary>
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }
}
