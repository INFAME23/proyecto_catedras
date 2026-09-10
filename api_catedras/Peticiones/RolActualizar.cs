// ==========================================================================
//  Peticiones/RolActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/Rol/{idRol}: actualización PARCIAL.
//  Todo opcional; el body vacío es regla de negocio (400) y la decide el
//  SERVICIO.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Rol/{idRol}. Todo opcional.</summary>
public class RolActualizar
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 60 caracteres.</summary>
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva descripción. Si llega: hasta 200 caracteres.</summary>
    [StringLength(200,
        ErrorMessage = "El campo 'descripcion' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que
    /// llegaron (no null). Lista blanca de columnas.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (Descripcion is not null) datos["descripcion"] = Descripcion;

        return datos;
    }
}
