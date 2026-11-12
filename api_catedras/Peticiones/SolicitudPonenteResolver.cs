// ==========================================================================
//  Peticiones/SolicitudPonenteResolver.cs  ·  Proyecto Cátedras · Subversión v7.1 · D56
// --------------------------------------------------------------------------
//  Body del PATCH /api/SolicitudPonente/{id}/resolver: la decisión del ADMIN.
//
//    aprobar = true   -> crea/reutiliza al asistente y su documento y, si la
//                        solicitud eligió sesión, lo asigna como PONENTE.
//    aprobar = false  -> rechaza; 'observacion' (el motivo) es obligatoria.
//
//  Al APROBAR una solicitud con fecha propuesta, la cátedra se crea sola: aquí van sus datos (modalidad,
//  sede y periodo son obligatorios; el resto tiene valores por omisión).
//  'resuelto_por' es el id del usuario ADMIN que decide. La API no autoriza
//  (D28): el front lo toma de su sesión y lo manda para dejar la huella.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/SolicitudPonente/{id}/resolver.</summary>
public class SolicitudPonenteResolver
{
    [Required(ErrorMessage = "El campo 'aprobar' es obligatorio (true o false).")]
    [JsonPropertyName("aprobar")]
    public bool? Aprobar { get; set; }

    /// <summary>Nota del administrador. Obligatoria al rechazar; opcional al aprobar.</summary>
    [StringLength(500, ErrorMessage = "El campo 'observacion' no puede pasar de 500 caracteres.")]
    [JsonPropertyName("observacion")]
    public string? Observacion { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El campo 'resuelto_por' debe ser mayor que 0.")]
    [JsonPropertyName("resuelto_por")]
    public long? ResueltoPor { get; set; }

    // ---- Datos de la CÁTEDRA NUEVA (v7.1 D58). Solo cuentan al APROBAR una solicitud con fecha propuesta. ----

    /// <summary>Nombre de la cátedra; vacío = el título de la ponencia.</summary>
    [StringLength(200, ErrorMessage = "El campo 'nombre_catedra' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("nombre_catedra")]
    public string? NombreCatedra { get; set; }

    /// <summary>Tipo de evento (4 caracteres, p. ej. CAAB); vacío = CAAB.</summary>
    [StringLength(4, ErrorMessage = "El campo 'fk_tipo_evento' debe tener 4 caracteres.")]
    [JsonPropertyName("fk_tipo_evento")]
    public string? FkTipoEvento { get; set; }

    /// <summary>Código ASIS de 9 dígitos; vacío = el siguiente código libre.</summary>
    [RegularExpression("^[0-9]{9}$", ErrorMessage = "El campo 'id_evento_asis' debe tener 9 dígitos (con ceros a la izquierda).")]
    [JsonPropertyName("id_evento_asis")]
    public string? IdEventoAsis { get; set; }

    [StringLength(15, ErrorMessage = "El campo 'fk_modalidad' no puede pasar de 15 caracteres.")]
    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    [StringLength(15, ErrorMessage = "El campo 'fk_sede' no puede pasar de 15 caracteres.")]
    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    [StringLength(6, ErrorMessage = "El campo 'fk_periodo' debe tener 6 caracteres (p. ej. 2026-2).")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    [StringLength(200, ErrorMessage = "El campo 'lugar' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("lugar")]
    public string? Lugar { get; set; }

    /// <summary>Enlace de ingreso de la sesión (http/https); vacío = sin enlace (v7.1 D59).</summary>
    [StringLength(300, ErrorMessage = "El campo 'enlace_virtual' no puede pasar de 300 caracteres.")]
    [RegularExpression(@"^\s*$|^https?://\S+$", ErrorMessage = "El campo 'enlace_virtual' debe empezar por http:// o https://.")]
    [JsonPropertyName("enlace_virtual")]
    public string? EnlaceVirtual { get; set; }

    /// <summary>Ajuste de la fecha y hora propuestas; vacío = las propuestas.</summary>
    [JsonPropertyName("inicio")]
    public DateTimeOffset? Inicio { get; set; }

    [JsonPropertyName("fin")]
    public DateTimeOffset? Fin { get; set; }
}
