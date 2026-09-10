// ==========================================================================
//  Peticiones/TipoVinculacionCrear.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del POST /api/TipoVinculacion: CREAR un tipo de vinculación nuevo.
//
//  'es_interno' es OBLIGATORIO aquí -- a diferencia de 'exige_programa' y
//  'activo' (que sí tienen DEFAULT en la BD), la columna 'es_interno' NO
//  tiene DEFAULT: no hay valor razonable que el servicio pueda aplicar
//  si el cliente no lo manda (3_plan.md, nota de diseño de TipoVinculacion).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/TipoVinculacion.</summary>
public class TipoVinculacionCrear
{
    /// <summary>Código del tipo de vinculación. Obligatorio, 1 a 15 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_tipo_vinculacion' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'id_tipo_vinculacion' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("id_tipo_vinculacion")]
    public string? IdTipoVinculacion { get; set; }

    /// <summary>Nombre del tipo de vinculación. Obligatorio, hasta 60 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(60, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 60 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>¿Es interno? OBLIGATORIO: la columna no tiene DEFAULT en la BD.</summary>
    [Required(ErrorMessage = "El campo 'es_interno' es obligatorio (true o false).")]
    [JsonPropertyName("es_interno")]
    public bool? EsInterno { get; set; }

    /// <summary>¿Exige programa? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("exige_programa")]
    public bool? ExigePrograma { get; set; }

    /// <summary>¿Está activo? Opcional; si no llega, el servicio usa true.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
