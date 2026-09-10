// ==========================================================================
//  Peticiones/EncuestaActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/Encuesta/{idEncuesta}: actualización PARCIAL.
//  Todo opcional; el body vacío es regla de negocio (400) y la decide el
//  SERVICIO. La coherencia de fechas también la valida el servicio, SOLO
//  cuando las DOS llegan juntas aquí (3_plan.md §4.8) — igual convención
//  que Sede: un campo que llega como null se interpreta como "no enviado".
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Encuesta/{idEncuesta}. Todo opcional.</summary>
public class EncuestaActualizar
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 120 caracteres.</summary>
    [StringLength(120, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 120 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva versión. Si llega: entero >= 1.</summary>
    [Range(1, short.MaxValue, ErrorMessage = "El campo 'version' debe ser un entero mayor o igual a 1.")]
    [JsonPropertyName("version")]
    public short? Version { get; set; }

    /// <summary>Nueva fecha de inicio de vigencia. Si llega.</summary>
    [JsonPropertyName("vigente_desde")]
    public DateOnly? VigenteDesde { get; set; }

    /// <summary>Nueva fecha de fin de vigencia. Si llega.</summary>
    [JsonPropertyName("vigente_hasta")]
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>Nuevo valor de "activa". Si llega: true o false.</summary>
    [JsonPropertyName("activa")]
    public bool? Activa { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que
    /// llegaron (no null). Lista blanca de columnas.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (Version is not null) datos["version"] = Version;
        if (VigenteDesde is not null) datos["vigente_desde"] = VigenteDesde;
        if (VigenteHasta is not null) datos["vigente_hasta"] = VigenteHasta;
        if (Activa is not null) datos["activa"] = Activa;

        return datos;
    }
}
