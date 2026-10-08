// ==========================================================================
//  Peticiones/PonenciaCrear.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Body del POST /api/Ponencia (RF129): ASIGNA un asistente a una sesión
//  con un rol.
//
//  ÚNICA petición de esta entidad (mismo criterio que AliasProgramaCrear y
//  ConsentimientoDatosCrear): no existen 'PonenciaReemplazo' ni
//  'PonenciaActualizar' porque las 3 columnas de la tabla son la llave y
//  no queda ninguna columna que editar (ADR D19). Esa ausencia ES la spec.
//
//  La misma combinación exacta (sesión + asistente + rol) ya existente ->
//  500 (PK duplicada, D9). El mismo asistente con OTRO rol en la misma
//  sesión es una fila nueva y válida.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Ponencia.</summary>
public class PonenciaCrear
{
    /// <summary>Sesión. Obligatoria (si no existe, la FK lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_sesion' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_sesion' debe ser mayor que 0.")]
    [JsonPropertyName("fk_sesion")]
    public long? FkSesion { get; set; }

    /// <summary>Asistente (v3). Obligatorio (si no existe, la FK lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    /// <summary>Rol. Opcional (por defecto PONENTE); lista cerrada (refleja chk_ponencia_rol).</summary>
    [RegularExpression("^(PONENTE|MODERADOR|INVITADO|ORGANIZADOR)$",
        ErrorMessage = "El campo 'rol' debe ser PONENTE, MODERADOR, INVITADO u ORGANIZADOR.")]
    [JsonPropertyName("rol")]
    public string? Rol { get; set; }
}
