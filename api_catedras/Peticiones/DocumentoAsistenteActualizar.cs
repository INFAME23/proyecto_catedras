// ==========================================================================
//  Peticiones/DocumentoAsistenteActualizar.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Body del PATCH /api/DocumentoAsistente/{idDocumento}: PARCIAL, todo
//  opcional (RF104). El uso típico: {"vigente": false} para "retirar" un
//  documento antes de registrar el nuevo vigente.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/DocumentoAsistente/{idDocumento}. Todo opcional.</summary>
public class DocumentoAsistenteActualizar
{
    [StringLength(5, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_tipo_documento' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("fk_tipo_documento")]
    public string? FkTipoDocumento { get; set; }

    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'numero' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("numero")]
    public string? Numero { get; set; }

    [JsonPropertyName("vigente")]
    public bool? Vigente { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (FkTipoDocumento is not null) datos["fk_tipo_documento"] = FkTipoDocumento;
        if (Numero is not null) datos["numero"] = Numero;
        if (Vigente is not null) datos["vigente"] = Vigente;

        return datos;
    }
}
