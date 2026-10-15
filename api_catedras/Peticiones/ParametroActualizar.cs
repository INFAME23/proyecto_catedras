// ==========================================================================
//  Peticiones/ParametroActualizar.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Cuerpo del PATCH /api/Parametro/{clave}: todos los campos son opcionales;
//  'ACamposEnviados' devuelve solo los que llegaron (mismo patrón que
//  SedeActualizar).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Cuerpo del PATCH de 'Parametro'.</summary>
public class ParametroActualizar
{
    [StringLength(200,
        ErrorMessage = "El campo 'valor' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("valor")]
    public string? Valor { get; set; }

    [RegularExpression("^(TEXTO|ENTERO|BOOLEANO|FECHA|DECIMAL)$",
        ErrorMessage = "El campo 'tipo_dato' debe ser TEXTO, ENTERO, BOOLEANO, FECHA o DECIMAL.")]
    [JsonPropertyName("tipo_dato")]
    public string? TipoDato { get; set; }

    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'descripcion' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario_modifica' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario_modifica")]
    public long? FkUsuarioModifica { get; set; }

    /// <summary>Solo los campos que el cliente envió (columna -> valor).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Valor is not null) datos["valor"] = Valor;
        if (TipoDato is not null) datos["tipo_dato"] = TipoDato;
        if (Descripcion is not null) datos["descripcion"] = Descripcion;
        if (FkUsuarioModifica is not null) datos["fk_usuario_modifica"] = FkUsuarioModifica;

        return datos;
    }
}
