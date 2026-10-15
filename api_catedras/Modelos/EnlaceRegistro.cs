// ==========================================================================
//  Modelos/EnlaceRegistro.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Una fila de 'catedras.enlace_registro': el enlace (o QR) con el que los
//  asistentes se registran en UNA sesión durante una ventana de tiempo.
//
//  Casi todo lo calcula la BD (ADR D24), por eso solo 'FkSesion' es
//  'required': el cliente nunca manda token, url, canal ni ventana.
//   · token ........ uuid, lo genera la BD (gen_random_uuid)
//   · url_publica .. URL_BASE + token, lo arma sp_emitir_enlace
//   · canal ........ QR o ENLACE, lo decide la modalidad de la sesión (RN-13)
//   · ventana ...... tstzrange calculado a partir de inicio/fin de la sesión
//
//  NOTA DE MAPEO tstzrange -> C#: Dapper no conoce 'tstzrange'. El repositorio
//  lo lee con 'lower(ventana) AS ventana_desde, upper(ventana) AS ventana_hasta'
//  y el modelo expone DOS propiedades planas (UTC, como el resto de fechas).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un enlace de registro de asistencia emitido para una sesión.</summary>
public class EnlaceRegistro
{
    [JsonPropertyName("id_enlace")]
    public long IdEnlace { get; set; }

    /// <summary>Sesión para la que se emitió (v4).</summary>
    [JsonPropertyName("fk_sesion")]
    public required long FkSesion { get; set; }

    /// <summary>Token (uuid) que el asistente usa para pedir su clave. Lo genera la BD.</summary>
    [JsonPropertyName("token")]
    public Guid Token { get; set; }

    /// <summary>Dirección pública a compartir. La calcula la BD.</summary>
    [JsonPropertyName("url_publica")]
    public string UrlPublica { get; set; } = "";

    /// <summary>Ruta RELATIVA de la imagen del QR (nunca una ruta absoluta).</summary>
    [JsonPropertyName("ruta_imagen_qr")]
    public string? RutaImagenQr { get; set; }

    /// <summary>QR o ENLACE. Lo decide la modalidad de la sesión (RN-13).</summary>
    [JsonPropertyName("canal")]
    public string Canal { get; set; } = "";

    /// <summary>Inicio de la ventana en que el enlace sirve (UTC).</summary>
    [JsonPropertyName("ventana_desde")]
    public DateTime VentanaDesde { get; set; }

    /// <summary>Fin de la ventana en que el enlace sirve (UTC).</summary>
    [JsonPropertyName("ventana_hasta")]
    public DateTime VentanaHasta { get; set; }

    /// <summary>Tope de usos. Sin uso activo en la v5 (null).</summary>
    [JsonPropertyName("usos_maximos")]
    public int? UsosMaximos { get; set; }

    /// <summary>Cuándo se revocó (UTC). Null = sigue vigente.</summary>
    [JsonPropertyName("revocado_en")]
    public DateTime? RevocadoEn { get; set; }

    /// <summary>Usuario que lo emitió (opcional).</summary>
    [JsonPropertyName("fk_usuario_crea")]
    public long? FkUsuarioCrea { get; set; }

    [JsonPropertyName("creado_en")]
    public DateTime CreadoEn { get; set; }
}
