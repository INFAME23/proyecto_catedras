// ==========================================================================
//  Peticiones/EnlaceRegistroEmitir.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del POST /api/EnlaceRegistro (RF145): EMITIR un enlace.
//  Es la ÚNICA petición de esta entidad (ADR D24): no es un CRUD, así que no
//  hay Crear/Reemplazo/Actualizar. Revocar no lleva body (el id va en la ruta).
//
//  Solo 'fk_sesion' es obligatorio. Los minutos y el usuario son opcionales:
//  si no llegan, la BD usa los parámetros VENTANA_MINUTOS_ANTES/DESPUES.
//  El cliente NUNCA manda canal, token, ventana ni url (RF148): ni siquiera
//  existen aquí.
//
//  Emitir para una sesión que ya tenía un enlace vigente REVOCA el anterior
//  (RN-14). No es un error: es el comportamiento documentado.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/EnlaceRegistro.</summary>
public class EnlaceRegistroEmitir
{
    /// <summary>Sesión a la que se le emite el enlace. Obligatorio
    /// (si no existe, la función de la BD lo rechaza -> 500).</summary>
    [Required(ErrorMessage = "El campo 'fk_sesion' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_sesion' debe ser mayor que 0.")]
    [JsonPropertyName("fk_sesion")]
    public long? FkSesion { get; set; }

    /// <summary>Minutos ANTES del inicio de la sesión en que ya sirve. Opcional.</summary>
    [Range(0, 1440, ErrorMessage = "El campo 'minutos_antes' debe estar entre 0 y 1440.")]
    [JsonPropertyName("minutos_antes")]
    public int? MinutosAntes { get; set; }

    /// <summary>Minutos DESPUÉS del fin de la sesión en que todavía sirve. Opcional.</summary>
    [Range(0, 1440, ErrorMessage = "El campo 'minutos_despues' debe estar entre 0 y 1440.")]
    [JsonPropertyName("minutos_despues")]
    public int? MinutosDespues { get; set; }

    /// <summary>Usuario que emite el enlace. Opcional (si no existe, la FK -> 500).</summary>
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }
}
