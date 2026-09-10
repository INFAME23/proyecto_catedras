// ==========================================================================
//  Peticiones/EstadoProcesoCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/EstadoProceso: CREAR un estado nuevo.
//  'es_final'/'es_exitoso' son opcionales aquí (por defecto false, igual
//  que la columna); la regla de COHERENCIA entre ambos ("exitoso implica
//  final") NO es de forma -> la valida el SERVICIO (3_plan.md §4.8).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/EstadoProceso.</summary>
public class EstadoProcesoCrear
{
    /// <summary>Código del estado. Obligatorio, 1 a 12 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_estado' es obligatorio.")]
    [StringLength(12, MinimumLength = 1,
        ErrorMessage = "El campo 'id_estado' debe tener entre 1 y 12 caracteres.")]
    [JsonPropertyName("id_estado")]
    public string? IdEstado { get; set; }

    /// <summary>Nombre del estado. Obligatorio, hasta 40 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(40, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 40 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Es final? Opcional; si no llega, el servicio usa false.</summary>
    [JsonPropertyName("es_final")]
    public bool? EsFinal { get; set; }

    /// <summary>¿Fue exitoso? Opcional; si no llega, el servicio usa false.</summary>
    [JsonPropertyName("es_exitoso")]
    public bool? EsExitoso { get; set; }
}
