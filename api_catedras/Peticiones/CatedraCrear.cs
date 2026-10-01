// ==========================================================================
//  Peticiones/CatedraCrear.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del POST /api/Catedra.
//
//  RNF7: aquí NO existe 'nombre_asis' (columna generada) ni 'id_catedra'
//  ni 'creado_en' (los pone la BD). Si el cliente los manda, el
//  deserializador los ignora: no hay propiedad donde guardarlos.
//
//  'id_evento_asis' se valida como TEXTO de exactamente 9 dígitos con
//  [RegularExpression] (refleja el CHECK chk_catedra_asis). "123" -> 422.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Catedra.</summary>
public class CatedraCrear
{
    /// <summary>Código ASIS. Obligatorio: exactamente 9 dígitos (con ceros a la izquierda).</summary>
    [Required(ErrorMessage = "El campo 'id_evento_asis' es obligatorio.")]
    [StringLength(9, MinimumLength = 9,
        ErrorMessage = "El campo 'id_evento_asis' debe tener exactamente 9 caracteres.")]
    [RegularExpression("^[0-9]{9}$",
        ErrorMessage = "El campo 'id_evento_asis' debe tener exactamente 9 dígitos (p. ej. '000023636').")]
    [JsonPropertyName("id_evento_asis")]
    public string? IdEventoAsis { get; set; }

    /// <summary>Nombre. Obligatorio, 1 a 200 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Tipo de evento (v1). Obligatorio, exactamente 4 caracteres (CHAR(4)).</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_evento' es obligatorio.")]
    [StringLength(4, MinimumLength = 4,
        ErrorMessage = "El campo 'fk_tipo_evento' debe tener exactamente 4 caracteres.")]
    [JsonPropertyName("fk_tipo_evento")]
    public string? FkTipoEvento { get; set; }

    /// <summary>Dependencia. Opcional; si llega, 1 a 15 caracteres.</summary>
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_dependencia' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_dependencia")]
    public string? FkDependencia { get; set; }

    /// <summary>¿Está activa? Opcional (por defecto true).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
