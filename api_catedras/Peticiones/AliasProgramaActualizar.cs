// ==========================================================================
//  Peticiones/AliasProgramaActualizar.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Body del PATCH /api/AliasPrograma/{idAlias}: ACTUALIZACIÓN PARCIAL. Todo
//  opcional; solo se modifica lo que llega (null = "no enviado"). El body
//  vacío lo rechaza el SERVICIO con 400.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/AliasPrograma/{idAlias}. Todo opcional.</summary>
public class AliasProgramaActualizar
{
    /// <summary>Nuevo texto. Si llega: 1 a 200 caracteres.</summary>
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'texto_normalizado' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("texto_normalizado")]
    public string? TextoNormalizado { get; set; }

    /// <summary>Nuevo programa. Si llega: 1 a 5 caracteres.</summary>
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_programa' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_programa")]
    public string? FkPrograma { get; set; }

    /// <summary>Nuevo origen. Si llega: 1 a 30 caracteres.</summary>
    [StringLength(30, MinimumLength = 1,
        ErrorMessage = "El campo 'origen' debe tener entre 1 y 30 caracteres.")]
    [JsonPropertyName("origen")]
    public string? Origen { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (lista blanca de columnas).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (TextoNormalizado is not null) datos["texto_normalizado"] = TextoNormalizado;
        if (FkPrograma is not null) datos["fk_programa"] = FkPrograma;
        if (Origen is not null) datos["origen"] = Origen;

        return datos;
    }
}
