// ==========================================================================
//  Peticiones/AsistenteCrear.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del POST /api/Asistente.
//
//  Validaciones de FORMA (anotaciones -> 422, la BD no se toca):
//   · 'nombres' y 'apellidos' obligatorios.
//   · 'id_asis': si llega, 5 a 15 alfanuméricos (refleja chk_asistente_idasis).
//     NO lleva [Required]: depende de 'es_externo' (cruce de dos campos), y
//     eso lo valida el SERVICIO (-> 400), no una anotación.
//   · correos: formato básico "algo@algo.algo" (refleja el '%@%.%' de la BD).
//
//  Maestro-detalle (Guía IA, Regla 1): el body acepta además 4 bloques
//  OPCIONALES —'documentos', 'vinculaciones', 'programas' (listas) y
//  'consentimiento' (UN objeto)— que se guardan en la MISMA transacción
//  vía 'sp_guardar_asistente'. Ninguno lleva 'fk_asistente': es siempre
//  el asistente que se está creando. ASP.NET valida las anotaciones de cada
//  elemento: un documento inválido -> 422 con el campo
//  'documentos[0].numero', y la BD no se toca.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Asistente.</summary>
public class AsistenteCrear
{
    /// <summary>Código ASIS. Obligatorio si no es externo (lo valida el servicio).</summary>
    [RegularExpression("^[0-9A-Za-z]{5,15}$",
        ErrorMessage = "El campo 'id_asis' debe tener entre 5 y 15 letras o dígitos.")]
    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    /// <summary>Nombres. Obligatorio, 1 a 100 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombres' es obligatorio.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'nombres' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    /// <summary>Apellidos. Obligatorio, 1 a 100 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'apellidos' es obligatorio.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'apellidos' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    /// <summary>Nombre tal como llegó del ASIS. Opcional, hasta 200 caracteres.</summary>
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_completo_asis' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre_completo_asis")]
    public string? NombreCompletoAsis { get; set; }

    /// <summary>Correo institucional. Opcional (pero debe haber al menos un correo).</summary>
    [StringLength(254, ErrorMessage = "El campo 'correo_institucional' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        ErrorMessage = "El campo 'correo_institucional' no tiene formato de correo.")]
    [JsonPropertyName("correo_institucional")]
    public string? CorreoInstitucional { get; set; }

    /// <summary>Correo personal. Opcional.</summary>
    [StringLength(254, ErrorMessage = "El campo 'correo_personal' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        ErrorMessage = "El campo 'correo_personal' no tiene formato de correo.")]
    [JsonPropertyName("correo_personal")]
    public string? CorreoPersonal { get; set; }

    /// <summary>Celular. Opcional, hasta 20 caracteres.</summary>
    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'celular' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("celular")]
    public string? Celular { get; set; }

    /// <summary>¿Es externo? Opcional (por defecto false).</summary>
    [JsonPropertyName("es_externo")]
    public bool? EsExterno { get; set; }

    /// <summary>¿Está activo? Opcional (por defecto true).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>Lote de carga al que pertenece. Opcional (lo manda la carga masiva).</summary>
    [JsonPropertyName("fk_lote_carga")]
    public long? FkLoteCarga { get; set; }

    // --- Detalles iniciales (Guía IA, Regla 1). Todos opcionales. ---------

    /// <summary>Documentos iniciales. null o [] = sin documentos.</summary>
    [JsonPropertyName("documentos")]
    public List<DetalleDocumentoInicial>? Documentos { get; set; }

    /// <summary>Vinculaciones iniciales. null o [] = sin vinculaciones.</summary>
    [JsonPropertyName("vinculaciones")]
    public List<DetalleVinculacionInicial>? Vinculaciones { get; set; }

    /// <summary>Programas iniciales. null o [] = sin programas.</summary>
    [JsonPropertyName("programas")]
    public List<DetalleProgramaInicial>? Programas { get; set; }

    /// <summary>Consentimiento inicial: UN objeto (no lista). null = sin consentimiento.</summary>
    [JsonPropertyName("consentimiento")]
    public DetalleConsentimientoInicial? Consentimiento { get; set; }
}
