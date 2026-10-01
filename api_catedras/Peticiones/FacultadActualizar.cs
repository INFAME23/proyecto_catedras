// ==========================================================================
//  Peticiones/FacultadActualizar.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del PATCH /api/Facultad/{idFacultad}: ACTUALIZACIÓN PARCIAL.
//  Todo opcional; solo se modifica lo que llega.
//
//  Convención heredada de la v1: un campo null = "no enviado". Por eso un
//  PATCH no puede QUITAR el padre (dejarlo en null): para eso se usa PUT.
//  El body vacío lo rechaza el SERVICIO con 400 (es regla de negocio).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Facultad/{idFacultad}. Todo opcional.</summary>
public class FacultadActualizar
{
    /// <summary>Nuevo nombre. Si llega: 1 a 120 caracteres.</summary>
    [StringLength(120, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 120 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo padre. Si llega: 1 a 10 caracteres.</summary>
    [StringLength(10, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_facultad_padre' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("fk_facultad_padre")]
    public string? FkFacultadPadre { get; set; }

    /// <summary>Nuevo valor de "activo". Si llega: true o false.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que llegaron
    /// (no null). Las claves son nombres de columna fijos (lista blanca):
    /// nunca vienen del cliente.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (FkFacultadPadre is not null) datos["fk_facultad_padre"] = FkFacultadPadre;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
