// ==========================================================================
//  Peticiones/ParametroCrear.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Cuerpo del POST /api/Parametro. Las propiedades son 'T?' para que un
//  campo ausente llegue como null y la validación responda 422 con un
//  mensaje claro (mismo patrón que SedeCrear).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Cuerpo del POST de 'Parametro'.</summary>
public class ParametroCrear
{
    [Required(ErrorMessage = "El campo 'clave' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'clave' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }

    [Required(AllowEmptyStrings = true, ErrorMessage = "El campo 'valor' es obligatorio.")]
    [StringLength(200,
        ErrorMessage = "El campo 'valor' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("valor")]
    public string? Valor { get; set; }

    // Opcional: si no llega, la columna usa su DEFAULT 'TEXTO'.
    [RegularExpression("^(TEXTO|ENTERO|BOOLEANO|FECHA|DECIMAL)$",
        ErrorMessage = "El campo 'tipo_dato' debe ser TEXTO, ENTERO, BOOLEANO, FECHA o DECIMAL.")]
    [JsonPropertyName("tipo_dato")]
    public string? TipoDato { get; set; }

    [Required(ErrorMessage = "El campo 'descripcion' es obligatorio.")]
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'descripcion' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario_modifica' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario_modifica")]
    public long? FkUsuarioModifica { get; set; }
}
