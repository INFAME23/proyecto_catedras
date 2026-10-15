// ==========================================================================
//  Peticiones/VinculacionAsistenteCrear.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Body del POST /api/VinculacionAsistente (RF108): ABRE una vinculación.
//  Nace sin 'fecha_fin' (vigente). La misma llave exacta (asistente + tipo
//  + fecha_ini) ya existente -> 500 (PK duplicada, D9).
//
//  Tabla de LLAVE COMPUESTA (ADR D16): esta entidad NO tiene peticiones
//  'Reemplazo' ni 'Actualizar'. Esa ausencia ES la especificación: la
//  historia no se reemplaza, solo se cierra (VinculacionAsistenteCerrar).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/VinculacionAsistente.</summary>
public class VinculacionAsistenteCrear
{
    /// <summary>Asistente. Obligatorio (si no existe, la FK lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Tipo de vinculación (v1). Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_vinculacion' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_vinculacion' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_tipo_vinculacion")]
    public string? FkTipoVinculacion { get; set; }

    /// <summary>Fecha de inicio "AAAA-MM-DD". Opcional (por defecto, hoy).</summary>
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }
}
