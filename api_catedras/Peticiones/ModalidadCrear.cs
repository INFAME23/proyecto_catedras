// ==========================================================================
//  Peticiones/ModalidadCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/Modalidad: CREAR una modalidad nueva.
//  Regla del POST: todos los campos obligatorios salvo 'activo', que tiene
//  valor por defecto en la BD (true) y en el servicio si no llega.
//
//  'canal_difusion' se valida aquí con [RegularExpression]: es una lista
//  CERRADA de valores ('QR'|'ENLACE'|'AMBOS', chk_modalidad_canal en la
//  BD) — comprobación de FORMA, no de negocio (3_plan.md §4.9).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Modalidad.</summary>
public class ModalidadCrear
{
    /// <summary>Código de la modalidad. Obligatorio, 1 a 15 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_modalidad' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'id_modalidad' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("id_modalidad")]
    public string? IdModalidad { get; set; }

    /// <summary>Nombre de la modalidad. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Canal de difusión. Obligatorio: "QR", "ENLACE" o "AMBOS".</summary>
    [Required(ErrorMessage = "El campo 'canal_difusion' es obligatorio.")]
    [RegularExpression("^(QR|ENLACE|AMBOS)$",
        ErrorMessage = "El campo 'canal_difusion' debe ser 'QR', 'ENLACE' o 'AMBOS'.")]
    [JsonPropertyName("canal_difusion")]
    public string? CanalDifusion { get; set; }

    /// <summary>¿Está activa? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
