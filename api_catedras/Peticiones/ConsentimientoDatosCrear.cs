// ==========================================================================
//  Peticiones/ConsentimientoDatosCrear.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del POST /api/ConsentimientoDatos (RF112). Es la ÚNICA petición de
//  esta entidad (igual criterio que AliasProgramaCrear en v2, pero aún más
//  estricto): un consentimiento es evidencia legal (Ley 1581) y no se
//  corrige ni se borra por API, así que no hay Reemplazo ni Actualizar.
//
//  'aceptado_en' lo pone la BD (now()); el cliente no lo manda.
//  El mismo asistente aceptando la misma 'version_politica' dos veces
//  -> 500 (uq_consentimiento_ver).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/ConsentimientoDatos.</summary>
public class ConsentimientoDatosCrear
{
    /// <summary>Asistente que acepta. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Versión de la política. Obligatoria, 1 a 20 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'version_politica' es obligatorio.")]
    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'version_politica' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("version_politica")]
    public string? VersionPolitica { get; set; }

    /// <summary>Canal. Opcional (por defecto WEB); lista cerrada.</summary>
    [RegularExpression("^(WEB|PRESENCIAL|CORREO|IMPORTADO)$",
        ErrorMessage = "El campo 'canal' debe ser WEB, PRESENCIAL, CORREO o IMPORTADO.")]
    [JsonPropertyName("canal")]
    public string? Canal { get; set; }

    /// <summary>IP. Opcional; una IP mal escrita la rechaza la BD (tipo INET -> 500).</summary>
    [StringLength(45, MinimumLength = 1,
        ErrorMessage = "El campo 'ip' debe tener entre 1 y 45 caracteres.")]
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }
}
