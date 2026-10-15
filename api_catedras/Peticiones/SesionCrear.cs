// ==========================================================================
//  Peticiones/SesionCrear.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Body del POST /api/Sesion (RF121).
//
//  Obligatorios: fk_catedra, inicio, fin, fk_modalidad, fk_sede, fk_periodo.
//  Opcionales:   numero_reunion, numero_reunion_asis, titulo, fk_encuesta,
//                lugar, enlace_virtual, cupo, estado.
//
//  'numero_reunion' ausente o 0 -> la BD lo calcula (trigger, ADR D20). Por
//  eso aquí el rango empieza en 0 y no en 1: el 0 es una señal válida.
//
//  'inicio' y 'fin' son DateTimeOffset: aceptan la hora CON su zona
//  ("2026-10-20T14:00:00-05:00"). El servicio la convierte a UTC (ADR D21).
//
//  Lo que NO está aquí a propósito: la regla "fin > inicio". Cruza DOS
//  campos del mismo body, así que es de negocio y la valida el SERVICIO
//  (400), no una anotación (RF125). Las anotaciones solo miran la FORMA de
//  cada campo por separado (422).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Sesion.</summary>
public class SesionCrear
{
    /// <summary>Cátedra (v2). Obligatoria (si no existe, la FK lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_catedra' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_catedra' debe ser mayor que 0.")]
    [JsonPropertyName("fk_catedra")]
    public long? FkCatedra { get; set; }

    /// <summary>Número de reunión. Opcional: ausente o 0 = lo calcula la BD (ADR D20).</summary>
    [Range(0, short.MaxValue,
        ErrorMessage = "El campo 'numero_reunion' debe estar entre 0 (automático) y 32767.")]
    [JsonPropertyName("numero_reunion")]
    public short? NumeroReunion { get; set; }

    /// <summary>Número de reunión según el ASIS. Opcional.</summary>
    [JsonPropertyName("numero_reunion_asis")]
    public short? NumeroReunionAsis { get; set; }

    /// <summary>Título. Opcional; si llega, 1 a 200 caracteres.</summary>
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'titulo' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("titulo")]
    public string? Titulo { get; set; }

    /// <summary>Inicio, con zona horaria. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'inicio' es obligatorio (p. ej. '2026-10-20T14:00:00-05:00').")]
    [JsonPropertyName("inicio")]
    public DateTimeOffset? Inicio { get; set; }

    /// <summary>Fin, con zona horaria. Obligatorio (y posterior a 'inicio': lo valida el servicio).</summary>
    [Required(ErrorMessage = "El campo 'fin' es obligatorio (p. ej. '2026-10-20T16:00:00-05:00').")]
    [JsonPropertyName("fin")]
    public DateTimeOffset? Fin { get; set; }

    /// <summary>Modalidad (v1). Obligatoria, 1 a 15 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'fk_modalidad' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_modalidad' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    /// <summary>Sede (v1). Obligatoria, 1 a 15 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'fk_sede' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_sede' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    /// <summary>Periodo (v1), p. ej. '2026-2'. Obligatorio, exactamente 6 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'fk_periodo' es obligatorio.")]
    [StringLength(6, MinimumLength = 6,
        ErrorMessage = "El campo 'fk_periodo' debe tener exactamente 6 caracteres (p. ej. '2026-2').")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    /// <summary>Encuesta (v1) que se aplicará al cierre. Opcional.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_encuesta' debe ser mayor que 0.")]
    [JsonPropertyName("fk_encuesta")]
    public long? FkEncuesta { get; set; }

    /// <summary>Lugar. Opcional; si llega, 1 a 200 caracteres.</summary>
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'lugar' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("lugar")]
    public string? Lugar { get; set; }

    /// <summary>Enlace virtual. Opcional; si llega, 1 a 300 caracteres.</summary>
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enlace_virtual' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enlace_virtual")]
    public string? EnlaceVirtual { get; set; }

    /// <summary>Cupo. Opcional; si llega, mayor que 0 (refleja chk_sesion_cupo).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "El campo 'cupo' debe ser mayor que 0.")]
    [JsonPropertyName("cupo")]
    public int? Cupo { get; set; }

    /// <summary>Estado. Opcional (por defecto PROGRAMADA); lista cerrada (refleja chk_sesion_estado).</summary>
    [RegularExpression("^(PROGRAMADA|ABIERTA|CERRADA|MIGRADA)$",
        ErrorMessage = "El campo 'estado' debe ser PROGRAMADA, ABIERTA, CERRADA o MIGRADA.")]
    [JsonPropertyName("estado")]
    public string? Estado { get; set; }
}
