// ==========================================================================
//  Peticiones/EnlaceInicial.cs  ·  Proyecto Cátedras · Maestro-detalle en el formulario
// --------------------------------------------------------------------------
//  El objeto opcional 'enlace' del POST /api/Sesion (Guía IA, Regla 1: la
//  sesión y su primer enlace de registro en una sola petición, SP
//  'sp_guardar_sesion', migración 044).
//
//  Es 'EnlaceRegistroEmitir' SIN 'fk_sesion': el enlace pertenece SIEMPRE a
//  la sesión que se está creando. Si llega (aunque sea vacío, '{}'), se
//  emite el enlace; si no llega, la sesión se crea sin enlace, como antes.
//  Minutos ausentes -> los de los parámetros VENTANA_MINUTOS_ANTES /
//  _DESPUES (los decide 'sp_emitir_enlace', provisto por el script).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Primer enlace de registro de una sesión que se está creando.</summary>
public class EnlaceInicial
{
    /// <summary>Minutos antes del inicio en que abre el registro. Opcional, 0 a 1440.</summary>
    [Range(0, 1440, ErrorMessage = "El campo 'minutos_antes' debe estar entre 0 y 1440.")]
    [JsonPropertyName("minutos_antes")]
    public int? MinutosAntes { get; set; }

    /// <summary>Minutos después del fin en que cierra el registro. Opcional, 0 a 1440.</summary>
    [Range(0, 1440, ErrorMessage = "El campo 'minutos_despues' debe estar entre 0 y 1440.")]
    [JsonPropertyName("minutos_despues")]
    public int? MinutosDespues { get; set; }

    /// <summary>Usuario que emite el enlace. Opcional (queda en 'fk_usuario_crea').</summary>
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }
}
