// ==========================================================================
//  Peticiones/SedeActualizar.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Body del PATCH /api/Sede/{id_sede}: ACTUALIZACIÓN PARCIAL.
//
//  PATCH = parcial -> TODOS los campos OPCIONALES. Solo se modifica lo que
//  llegue; cada campo que llegue se valida (longitud, tipo). Lo que NO
//  llega, no se toca.
//
//  Convención de esta v1 (limitación asumida y documentada): un campo que
//  llega como null se interpreta como "no enviado". Para dejar 'direccion'
//  explícitamente vacía se usa PUT (reemplazo completo). Distinguir
//  "ausente" de "null" exigiría leer el JSON crudo y no aporta a la v1.
//
//  El body VACÍO (ningún campo) NO es un error de forma: es una regla de
//  negocio -> lo rechaza el SERVICIO con 400 (ver ServicioSede).
//
//  [SOLID · S] Reparto claro: aquí SOLO "forma" (longitudes, tipos); el
//  "body vacío" es negocio y vive en ServicioSede. Cada cosa en su capa.
//  'ACamposEnviados()' arma el diccionario columna->valor SOLO con lo que
//  llegó: [ACID · C] así el UPDATE no pisa columnas que el cliente nunca
//  mandó.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Sede/{id_sede}. Todo opcional.</summary>
public class SedeActualizar : IValidatableObject
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 80 caracteres.</summary>
    [StringLength(80, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 80 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nueva dirección. Si llega: hasta 200 caracteres.</summary>
    [StringLength(200,
        ErrorMessage = "El campo 'direccion' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }

    /// <summary>Nuevo valor de "es virtual". Si llega: true o false.</summary>
    [JsonPropertyName("es_virtual")]
    public bool? EsVirtual { get; set; }

    /// <summary>Nuevo valor de "activo". Si llega: true o false.</summary>
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

    /// <summary>
    /// Ayuda para el servicio: arma el diccionario columna -> valor SOLO con
    /// los campos que llegaron (no null). Lista blanca de columnas: nunca
    /// se usan claves que venga del cliente.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        // Diccionario vacío al que agregamos solo lo que vino.
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (Direccion is not null) datos["direccion"] = Direccion;
        if (EsVirtual is not null) datos["es_virtual"] = EsVirtual;
        if (Activo is not null) datos["activo"] = Activo;

        // v7.2: solo viajan los campos de ubicación que llegaron (latitud y longitud, validadas, vienen juntas).
        if (Latitud is not null) datos["latitud"] = Latitud;
        if (Longitud is not null) datos["longitud"] = Longitud;
        if (RadioMetros is not null) datos["radio_metros"] = RadioMetros;

        return datos;
    }
}
