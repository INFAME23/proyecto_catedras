// ==========================================================================
//  Peticiones/LoteCargaAsistenteCerrar.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Body del POST /api/LoteCargaAsistente/{id}/cerrar (RF116).
//
//  Forma (anotaciones -> 422): los 3 contadores obligatorios y >= 0; cada
//  novedad con su número de fila, contenido y motivo.
//
//  Reglas del SERVICIO (cruces de campos del mismo body -> 400):
//   · filas_aceptadas + filas_rechazadas <= filas_leidas
//   · una novedad por cada fila rechazada (RNF8: ningún rechazo sin causa)
//   · el lote no puede estar ya cerrado
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/LoteCargaAsistente/{id}/cerrar.</summary>
public class LoteCargaAsistenteCerrar
{
    /// <summary>Filas que tenía el archivo. Obligatorio, >= 0.</summary>
    [Required(ErrorMessage = "El campo 'filas_leidas' es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "El campo 'filas_leidas' debe ser 0 o mayor.")]
    [JsonPropertyName("filas_leidas")]
    public int? FilasLeidas { get; set; }

    /// <summary>Filas guardadas. Obligatorio, >= 0.</summary>
    [Required(ErrorMessage = "El campo 'filas_aceptadas' es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "El campo 'filas_aceptadas' debe ser 0 o mayor.")]
    [JsonPropertyName("filas_aceptadas")]
    public int? FilasAceptadas { get; set; }

    /// <summary>Filas rechazadas. Obligatorio, >= 0.</summary>
    [Required(ErrorMessage = "El campo 'filas_rechazadas' es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "El campo 'filas_rechazadas' debe ser 0 o mayor.")]
    [JsonPropertyName("filas_rechazadas")]
    public int? FilasRechazadas { get; set; }

    /// <summary>Comentario del cierre. Opcional.</summary>
    [StringLength(2000, ErrorMessage = "El campo 'observaciones' admite hasta 2000 caracteres.")]
    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }

    /// <summary>Una novedad por fila rechazada. null o [] = ninguna.</summary>
    [JsonPropertyName("novedades")]
    public List<NovedadInicial>? Novedades { get; set; }
}
