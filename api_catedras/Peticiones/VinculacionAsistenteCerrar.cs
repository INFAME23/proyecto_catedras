// ==========================================================================
//  Peticiones/VinculacionAsistenteCerrar.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Body del PATCH /api/VinculacionAsistente/cerrar (RF109).
//
//  Como la tabla no tiene 'id' (ADR D16), la fila a cerrar se identifica
//  con su LLAVE COMPLETA, que viaja en el body: 'fk_asistente',
//  'fk_tipo_vinculacion' y 'fecha_ini'. Más 'fecha_fin' (por defecto hoy).
//
//  Reglas del SERVICIO (no de forma):
//   · llave inexistente -> 404
//   · ya estaba cerrada -> 400 (no se "re-cierra" algo que ya terminó)
//   · fecha_fin anterior a fecha_ini -> 400
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/VinculacionAsistente/cerrar.</summary>
public class VinculacionAsistenteCerrar
{
    /// <summary>Llave (1/3): asistente.</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Llave (2/3): tipo de vinculación.</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_vinculacion' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_vinculacion' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_tipo_vinculacion")]
    public string? FkTipoVinculacion { get; set; }

    /// <summary>Llave (3/3): fecha de inicio "AAAA-MM-DD".</summary>
    [Required(ErrorMessage = "El campo 'fecha_ini' es obligatorio (parte de la llave).")]
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }

    /// <summary>Fecha de cierre "AAAA-MM-DD". Opcional (por defecto, hoy).</summary>
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }
}
