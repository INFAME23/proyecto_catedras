// ==========================================================================
//  Peticiones/SesionReemplazo.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Body del PUT /api/Sesion/{idSesion}: REEMPLAZO COMPLETO (RF122).
//
//  Obligatorias TODAS las columnas NOT NULL de la tabla: fk_catedra,
//  numero_reunion, inicio, fin, fk_modalidad, fk_sede, fk_periodo y estado.
//  Las opcionales se escriben SIEMPRE (si no llegan, quedan NULL): eso es
//  "reemplazar" (Artículo 7, contraste PUT vs PATCH).
//
//  Diferencia con SesionCrear: aquí 'numero_reunion' va de 1 en adelante,
//  sin el 0 "automático". El trigger de la BD (ADR D20) es BEFORE INSERT:
//  en un UPDATE no se dispara, así que un 0 llegaría tal cual y lo
//  rechazaría el CHECK chk_sesion_reunion (500). Mejor un 422 claro aquí.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Sesion/{idSesion}.</summary>
public class SesionReemplazo
{
    [Required(ErrorMessage = "El campo 'fk_catedra' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_catedra' debe ser mayor que 0.")]
    [JsonPropertyName("fk_catedra")]
    public long? FkCatedra { get; set; }

    /// <summary>Obligatorio y >= 1: en un UPDATE la BD no lo calcula.</summary>
    [Required(ErrorMessage = "El campo 'numero_reunion' es obligatorio.")]
    [Range(1, short.MaxValue,
        ErrorMessage = "El campo 'numero_reunion' debe estar entre 1 y 32767.")]
    [JsonPropertyName("numero_reunion")]
    public short? NumeroReunion { get; set; }

    /// <summary>Si no llega o es null -> queda NULL.</summary>
    [JsonPropertyName("numero_reunion_asis")]
    public short? NumeroReunionAsis { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'titulo' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("titulo")]
    public string? Titulo { get; set; }

    [Required(ErrorMessage = "El campo 'inicio' es obligatorio (p. ej. '2026-10-20T14:00:00-05:00').")]
    [JsonPropertyName("inicio")]
    public DateTimeOffset? Inicio { get; set; }

    [Required(ErrorMessage = "El campo 'fin' es obligatorio (p. ej. '2026-10-20T16:00:00-05:00').")]
    [JsonPropertyName("fin")]
    public DateTimeOffset? Fin { get; set; }

    [Required(ErrorMessage = "El campo 'fk_modalidad' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_modalidad' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    [Required(ErrorMessage = "El campo 'fk_sede' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_sede' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    [Required(ErrorMessage = "El campo 'fk_periodo' es obligatorio.")]
    [StringLength(6, MinimumLength = 6,
        ErrorMessage = "El campo 'fk_periodo' debe tener exactamente 6 caracteres (p. ej. '2026-2').")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_encuesta' debe ser mayor que 0.")]
    [JsonPropertyName("fk_encuesta")]
    public long? FkEncuesta { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'lugar' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("lugar")]
    public string? Lugar { get; set; }

    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enlace_virtual' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enlace_virtual")]
    public string? EnlaceVirtual { get; set; }


    [Required(ErrorMessage = "El campo 'estado' es obligatorio.")]
    [RegularExpression("^(PROGRAMADA|ABIERTA|CERRADA|MIGRADA)$",
        ErrorMessage = "El campo 'estado' debe ser PROGRAMADA, ABIERTA, CERRADA o MIGRADA.")]
    [JsonPropertyName("estado")]
    public string? Estado { get; set; }
}
