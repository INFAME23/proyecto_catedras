// ==========================================================================
//  Peticiones/DetalleVinculacionInicial.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Un elemento de la lista 'vinculaciones' del POST /api/Asistente (Guía
//  IA, Regla 1). Sin 'fk_asistente' (lo fija el SP). Una vinculación nace
//  ABIERTA: no hay 'fecha_fin' aquí; se cierra después con
//  PATCH /api/VinculacionAsistente/cerrar.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Vinculación que nace junto con su asistente.</summary>
public class DetalleVinculacionInicial
{
    /// <summary>Tipo de vinculación (ESTUDIANTE, DOCENTE...). Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_vinculacion' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_vinculacion' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_tipo_vinculacion")]
    public string? FkTipoVinculacion { get; set; }

    /// <summary>Fecha de inicio "AAAA-MM-DD". Opcional (por defecto, hoy).</summary>
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }
}
