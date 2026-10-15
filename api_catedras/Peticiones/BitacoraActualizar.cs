// ==========================================================================
//  Peticiones/BitacoraActualizar.cs  ·  Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Body del PATCH /api/Bitacora/{idBitacora}: ACTUALIZACIÓN PARCIAL. Todo
//  opcional; null = "no enviado" (un PATCH no puede dejar en NULL un campo:
//  para eso se usa PUT). Body vacío -> 400 desde el servicio.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Bitacora/{idBitacora}. Todo opcional.</summary>
public class BitacoraActualizar
{
    [StringLength(63, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_tabla' debe tener entre 1 y 63 caracteres.")]
    [JsonPropertyName("nombre_tabla")]
    public string? NombreTabla { get; set; }

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

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (lista blanca de columnas).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (NombreTabla is not null) datos["nombre_tabla"] = NombreTabla;
        if (Operacion is not null) datos["operacion"] = Operacion;
        if (FkUsuario is not null) datos["fk_usuario"] = FkUsuario;
        if (Llave is not null) datos["llave"] = Llave;
        if (DatosAntes is not null) datos["datos_antes"] = DatosAntes;
        if (DatosDespues is not null) datos["datos_despues"] = DatosDespues;

        return datos;
    }
}
