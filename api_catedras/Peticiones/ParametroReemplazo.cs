// ==========================================================================
//  Peticiones/ParametroReemplazo.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Cuerpo del PUT /api/Parametro/{clave}: reemplaza TODAS las columnas de
//  negocio. La clave va en la URL y no se puede cambiar.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Cuerpo del PUT de 'Parametro'.</summary>
public class ParametroReemplazo
{
    [Required(AllowEmptyStrings = true, ErrorMessage = "El campo 'valor' es obligatorio.")]
    [StringLength(200,
        ErrorMessage = "El campo 'valor' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("valor")]
    public string? Valor { get; set; }

    [Required(ErrorMessage = "El campo 'tipo_dato' es obligatorio.")]
    [RegularExpression("^(TEXTO|ENTERO|BOOLEANO|FECHA|DECIMAL)$",
        ErrorMessage = "El campo 'tipo_dato' debe ser TEXTO, ENTERO, BOOLEANO, FECHA o DECIMAL.")]
    [JsonPropertyName("tipo_dato")]
    public string? TipoDato { get; set; }

    [Required(ErrorMessage = "El campo 'descripcion' es obligatorio.")]
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'descripcion' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    // Opcional: si no llega, el PUT deja NULL en la columna.
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario_modifica' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario_modifica")]
    public long? FkUsuarioModifica { get; set; }
}
