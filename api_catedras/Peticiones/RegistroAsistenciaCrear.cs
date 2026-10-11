// ==========================================================================
//  Peticiones/RegistroAsistenciaCrear.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/RegistroAsistencia (RF156): alta MANUAL de asistencia.
//  Es la ÚNICA petición de esta entidad: un registro es un hecho auditable
//  (RF157), así que no hay Reemplazo ni Actualizar.
//
//  ⚠ 'origen' lleva SOLO [Required], SIN [RegularExpression] (ADR D26).
//  La lista cerrada {MANUAL, IMPORTADO} la valida el SERVICIO. Si se pusiera
//  aquí, un origen "QR" respondería 422 (forma) y el servicio nunca se
//  enteraría; la especificación exige 400 (criterio de aceptación 6).
//
//  No existen en esta petición: fk_clave, fk_enlace (siempre NULL en un
//  registro manual) ni las dos instantáneas (las calcula el trigger).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/RegistroAsistencia.</summary>
public class RegistroAsistenciaCrear
{
    [Required(ErrorMessage = "El campo 'fk_sesion' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_sesion' debe ser mayor que 0.")]
    [JsonPropertyName("fk_sesion")]
    public long? FkSesion { get; set; }

    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>MANUAL o IMPORTADO. Obligatorio; la lista la valida el servicio (D26).</summary>
    [Required(ErrorMessage = "El campo 'origen' es obligatorio.")]
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }
}
