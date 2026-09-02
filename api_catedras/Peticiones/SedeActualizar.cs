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
public class SedeActualizar
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

        return datos;
    }
}
