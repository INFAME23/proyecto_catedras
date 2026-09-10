// ==========================================================================
//  Peticiones/TipoVinculacionActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/TipoVinculacion/{idTipoVinculacion}: actualización
//  PARCIAL. Todo opcional (incluido 'es_interno': en el PATCH sí puede
//  quedar sin tocar, a diferencia del POST); el body vacío es regla de
//  negocio (400) y la decide el SERVICIO.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/TipoVinculacion/{idTipoVinculacion}. Todo opcional.</summary>
public class TipoVinculacionActualizar
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 60 caracteres.</summary>
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo valor de "es_interno". Si llega: true o false.</summary>
    [JsonPropertyName("es_interno")]
    public bool? EsInterno { get; set; }

    /// <summary>Nuevo valor de "exige_programa". Si llega: true o false.</summary>
    [JsonPropertyName("exige_programa")]
    public bool? ExigePrograma { get; set; }

    /// <summary>Nuevo valor de "activo". Si llega: true o false.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que
    /// llegaron (no null). Lista blanca de columnas.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (EsInterno is not null) datos["es_interno"] = EsInterno;
        if (ExigePrograma is not null) datos["exige_programa"] = ExigePrograma;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
