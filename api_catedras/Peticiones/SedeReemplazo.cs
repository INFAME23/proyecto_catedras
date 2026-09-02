// ==========================================================================
//  Peticiones/SedeReemplazo.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Body del PUT /api/Sede/{id_sede}: REEMPLAZAR la sede completa.
//
//  PUT = reemplazo total del recurso -> TODOS los campos obligatorios
//  (menos 'direccion', porque la columna admite NULL: mandarla vacía o no
//  mandarla significa "esta sede queda sin dirección", no "déjala como
//  estaba"). El 'id_sede' NO va en el body: viaja en la URL.
//
//  CONTRASTE DIDÁCTICO (criterio de aceptación 4): el mismo body sin
//  'nombre'...
//     · en PUT   -> 422 (falta un campo obligatorio del reemplazo)
//     · en PATCH -> 200 (PATCH es parcial: lo que no llega, no se toca)
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Sede/{id_sede}.</summary>
public class SedeReemplazo
{
    /// <summary>Nuevo nombre de la sede. Obligatorio, no vacío, hasta 80.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(80, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 80 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva dirección. Opcional (la columna admite NULL); hasta 200.</summary>
    [StringLength(200,
        ErrorMessage = "El campo 'direccion' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }

    /// <summary>¿Es virtual? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'es_virtual' es obligatorio (true o false).")]
    [JsonPropertyName("es_virtual")]
    public bool? EsVirtual { get; set; }

    /// <summary>¿Está activa? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
