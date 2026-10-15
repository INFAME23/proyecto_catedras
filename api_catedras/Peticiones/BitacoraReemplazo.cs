// ==========================================================================
//  Peticiones/BitacoraReemplazo.cs  ·  Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Body del PUT /api/Bitacora/{idBitacora}: REEMPLAZO COMPLETO de las 6
//  columnas de negocio. 'nombre_tabla' y 'operacion' son obligatorias; el
//  resto, si no llega, queda NULL. NO se puede cambiar 'ocurrido_en' ni
//  'usuario_bd'.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Bitacora/{idBitacora}.</summary>
public class BitacoraReemplazo
{
    [Required(ErrorMessage = "El campo 'nombre_tabla' es obligatorio.")]
    [StringLength(63, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_tabla' debe tener entre 1 y 63 caracteres.")]
    [JsonPropertyName("nombre_tabla")]
    public string? NombreTabla { get; set; }

    [Required(ErrorMessage = "El campo 'operacion' es obligatorio.")]
    [RegularExpression("^(INSERT|UPDATE|DELETE)$",
        ErrorMessage = "El campo 'operacion' debe ser INSERT, UPDATE o DELETE.")]
    [JsonPropertyName("operacion")]
    public string? Operacion { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'llave' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("llave")]
    public string? Llave { get; set; }

    [JsonPropertyName("datos_antes")]
    public JsonElement? DatosAntes { get; set; }

    [JsonPropertyName("datos_despues")]
    public JsonElement? DatosDespues { get; set; }
}
