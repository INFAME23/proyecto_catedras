// ==========================================================================
//  Peticiones/NovedadInicial.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Un elemento de la lista 'novedades' del cierre de un lote (RF116). Sin
//  'fk_lote_carga': la novedad pertenece SIEMPRE al lote que se está
//  cerrando (lo fija el SP), igual que AliasInicial en la v2.
//
//  'motivo' admite hasta 200 caracteres (VARCHAR(200) en la BD). Se valida
//  AQUÍ (-> 422) y no se deja a la BD: si una sola novedad fuera más larga,
//  el SP revertiría el cierre COMPLETO del lote.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Una fila rechazada, dentro del cierre de un lote.</summary>
public class NovedadInicial
{
    /// <summary>Número de la fila en el archivo. Obligatorio, mayor que 0.</summary>
    [Required(ErrorMessage = "El campo 'numero_fila' es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El campo 'numero_fila' debe ser mayor que 0.")]
    [JsonPropertyName("numero_fila")]
    public int? NumeroFila { get; set; }

    /// <summary>La fila tal como llegó. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'contenido_crudo' es obligatorio.")]
    [JsonPropertyName("contenido_crudo")]
    public string? ContenidoCrudo { get; set; }

    /// <summary>Motivo del rechazo. Obligatorio, 1 a 200 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'motivo' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'motivo' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("motivo")]
    public string? Motivo { get; set; }
}
