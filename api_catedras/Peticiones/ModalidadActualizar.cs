// ==========================================================================
//  Peticiones/ModalidadActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/Modalidad/{idModalidad}: actualización PARCIAL.
//  Todo opcional; el body vacío es regla de negocio (400) y la decide el
//  SERVICIO, no esta petición.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Modalidad/{idModalidad}. Todo opcional.</summary>
public class ModalidadActualizar
{
    /// <summary>Nuevo nombre. Si llega: no vacío, hasta 60 caracteres.</summary>
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo canal de difusión. Si llega: "QR", "ENLACE" o "AMBOS".</summary>
    [RegularExpression("^(QR|ENLACE|AMBOS)$",
        ErrorMessage = "El campo 'canal_difusion' debe ser 'QR', 'ENLACE' o 'AMBOS'.")]
    [JsonPropertyName("canal_difusion")]
    public string? CanalDifusion { get; set; }

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
        if (CanalDifusion is not null) datos["canal_difusion"] = CanalDifusion;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
