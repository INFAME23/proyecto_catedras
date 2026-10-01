// ==========================================================================
//  Peticiones/DependenciaReemplazo.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Body del PUT /api/Dependencia/{idDependencia}: REEMPLAZO COMPLETO.
//
//  'nombre' y 'activo' son obligatorios (falta uno -> 422). El padre se
//  escribe SIEMPRE: si no llega (o llega null), la dependencia queda SIN
//  padre — eso es lo que significa "reemplazar todo" (igual que
//  'direccion' de Sede en la v1). El 'id_dependencia' viaja en la URL, no aquí.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Dependencia/{idDependencia}.</summary>
public class DependenciaReemplazo
{
    /// <summary>Nuevo nombre. Obligatorio, 1 a 120 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(120, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 120 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Nuevo padre. Si no llega o es null -> la dependencia queda sin padre.</summary>
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_dependencia_padre' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_dependencia_padre")]
    public string? FkDependenciaPadre { get; set; }

    /// <summary>¿Está activa? Obligatorio en el PUT.</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }
}
