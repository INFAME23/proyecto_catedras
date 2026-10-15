// ==========================================================================
//  Peticiones/CatedraReemplazo.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del PUT /api/Catedra/{idCatedra}: REEMPLAZO COMPLETO.
//  Obligatorios: id_evento_asis, nombre, fk_tipo_evento y activo.
//  'fk_dependencia' se escribe siempre (si no llega, queda NULL).
//  Sin 'nombre_asis' (RNF7): al cambiar 'nombre', la BD la recalcula sola.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Catedra/{idCatedra}.</summary>
public class CatedraReemplazo
{
    [Required(ErrorMessage = "El campo 'id_evento_asis' es obligatorio.")]
    [StringLength(9, MinimumLength = 9,
        ErrorMessage = "El campo 'id_evento_asis' debe tener exactamente 9 caracteres.")]
    [RegularExpression("^[0-9]{9}$",
        ErrorMessage = "El campo 'id_evento_asis' debe tener exactamente 9 dígitos (p. ej. '000023636').")]
    [JsonPropertyName("id_evento_asis")]
    public string? IdEventoAsis { get; set; }

    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "El campo 'fk_tipo_evento' es obligatorio.")]
    [StringLength(4, MinimumLength = 4,
        ErrorMessage = "El campo 'fk_tipo_evento' debe tener exactamente 4 caracteres.")]
    [JsonPropertyName("fk_tipo_evento")]
    public string? FkTipoEvento { get; set; }

    /// <summary>Si no llega o es null -> la cátedra queda sin dependencia.</summary>
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_dependencia' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_dependencia")]
    public string? FkDependencia { get; set; }

    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
