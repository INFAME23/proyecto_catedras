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
//
//  [SOLID · S / I] Contrato propio del PUT. Que PUT y PATCH tengan clases
//  distintas es lo que permite que la MISMA entrada dé 422 en uno y 200 en
//  el otro sin un solo 'if' de por medio.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Sede/{id_sede}.</summary>
public class SedeReemplazo : IValidatableObject
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

    // ----------------------------------------------------------------------
    //  v7.2 (RF-S11): ubicación de la sede. Los tres campos son OPCIONALES:
    //  una sede virtual no los lleva, y una sede física sin ellos no podrá
    //  generar QR (migración 062) hasta que se completen.
    // ----------------------------------------------------------------------

    /// <summary>Latitud en grados, entre -90 y 90. Va junto a la longitud.</summary>
    [Range(-90.0, 90.0, ErrorMessage = "El campo 'latitud' debe estar entre -90 y 90.")]
    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    /// <summary>Longitud en grados, entre -180 y 180. Va junto a la latitud.</summary>
    [Range(-180.0, 180.0, ErrorMessage = "El campo 'longitud' debe estar entre -180 y 180.")]
    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }

    /// <summary>Radio propio de la sede en metros (10 a 5000). Sin él se usa el parámetro global.</summary>
    [Range(10, 5000, ErrorMessage = "El campo 'radio_metros' debe estar entre 10 y 5000.")]
    [JsonPropertyName("radio_metros")]
    public int? RadioMetros { get; set; }

    /// <summary>Regla de dos campos: latitud y longitud juntas o ninguna (-> 422).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto) =>
        ValidacionGeo.LatitudYLongitudJuntas(Latitud, Longitud);
}
