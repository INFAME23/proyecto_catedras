// ==========================================================================
//  Peticiones/CatedraActualizar.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del PATCH /api/Catedra/{idCatedra}: PARCIAL, todo opcional.
//  Sin 'nombre_asis' (RNF7). Convención v1: null = "no enviado".
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Catedra/{idCatedra}. Todo opcional.</summary>
public class CatedraActualizar
{
    [StringLength(9, MinimumLength = 9,
        ErrorMessage = "El campo 'id_evento_asis' debe tener exactamente 9 caracteres.")]
    [RegularExpression("^[0-9]{9}$",
        ErrorMessage = "El campo 'id_evento_asis' debe tener exactamente 9 dígitos (p. ej. '000023636').")]
    [JsonPropertyName("id_evento_asis")]
    public string? IdEventoAsis { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [StringLength(4, MinimumLength = 4,
        ErrorMessage = "El campo 'fk_tipo_evento' debe tener exactamente 4 caracteres.")]
    [JsonPropertyName("fk_tipo_evento")]
    public string? FkTipoEvento { get; set; }

    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_dependencia' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_dependencia")]
    public string? FkDependencia { get; set; }

    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).
    /// 'nombre_asis' jamás aparece: es una columna generada.</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (IdEventoAsis is not null) datos["id_evento_asis"] = IdEventoAsis;
        if (Nombre is not null) datos["nombre"] = Nombre;
        if (FkTipoEvento is not null) datos["fk_tipo_evento"] = FkTipoEvento;
        if (FkDependencia is not null) datos["fk_dependencia"] = FkDependencia;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
