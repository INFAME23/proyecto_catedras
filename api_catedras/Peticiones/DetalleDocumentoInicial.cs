// ==========================================================================
//  Peticiones/DetalleDocumentoInicial.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Un elemento de la lista 'documentos' del POST /api/Asistente (Guía IA,
//  Regla 1). Mismo criterio que 'AliasInicial' de la v2: NO tiene
//  'fk_asistente', porque el documento pertenece SIEMPRE al asistente que
//  se crea en la misma petición (lo fija el SP con el id generado).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Documento que nace junto con su asistente.</summary>
public class DetalleDocumentoInicial
{
    /// <summary>Tipo de documento (CC, TI, CE...). Obligatorio; si no existe, la FK lo rechaza (500).</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_documento' es obligatorio.")]
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_documento' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_tipo_documento")]
    public string? FkTipoDocumento { get; set; }

    /// <summary>Número. Obligatorio: 4 a 20 letras, dígitos o guiones (chk_documento_numero).</summary>
    [Required(ErrorMessage = "El campo 'numero' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'numero' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("numero")]
    public string? Numero { get; set; }

    /// <summary>¿Vigente? Opcional (por defecto true).</summary>
    [JsonPropertyName("vigente")]
    public bool? Vigente { get; set; }
}
