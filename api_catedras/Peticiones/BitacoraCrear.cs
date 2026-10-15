// ==========================================================================
//  Peticiones/BitacoraCrear.cs  ·  Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Body del POST /api/Bitacora. 'id_bitacora', 'ocurrido_en' y 'usuario_bd'
//  NO están aquí: los pone la BD (si el cliente los manda, se ignoran).
//  'datos_antes' / 'datos_despues' son JSON libre.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Bitacora.</summary>
public class BitacoraCrear
{
    /// <summary>Tabla auditada. Obligatoria, 1 a 63 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre_tabla' es obligatorio.")]
    [StringLength(63, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_tabla' debe tener entre 1 y 63 caracteres.")]
    [JsonPropertyName("nombre_tabla")]
    public string? NombreTabla { get; set; }

    /// <summary>INSERT, UPDATE o DELETE (refleja chk_bitacora_operacion). Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'operacion' es obligatorio.")]
    [RegularExpression("^(INSERT|UPDATE|DELETE)$",
        ErrorMessage = "El campo 'operacion' debe ser INSERT, UPDATE o DELETE.")]
    [JsonPropertyName("operacion")]
    public string? Operacion { get; set; }

    /// <summary>Usuario de la aplicación. Opcional (si no existe, la FK lo rechaza -> 500).</summary>
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    /// <summary>Llave de la fila afectada, como texto. Opcional, hasta 100 caracteres.</summary>
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'llave' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("llave")]
    public string? Llave { get; set; }

    /// <summary>La fila ANTES del cambio. Opcional (JSON libre).</summary>
    [JsonPropertyName("datos_antes")]
    public JsonElement? DatosAntes { get; set; }

    /// <summary>La fila DESPUÉS del cambio. Opcional (JSON libre).</summary>
    [JsonPropertyName("datos_despues")]
    public JsonElement? DatosDespues { get; set; }
}
