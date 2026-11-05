// ==========================================================================
//  Peticiones/QrSesionEmitir.cs  ·  Cátedras · Subversión v7.1 · D53
// --------------------------------------------------------------------------
//  Body de POST /api/QrSesion. 'regenerar' = true revoca el QR vigente y crea
//  otro (RN-14); sin él, si ya hay uno vigente se devuelve ese (idempotente).
//  La ventana NO viaja aquí: la fija la BD con QR_MINUTOS_ANTES_FIN y
//  QR_MINUTOS_DESPUES_FIN respecto al fin de la sesión.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class QrSesionEmitir
{
    [Required(ErrorMessage = "El campo 'fk_sesion' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_sesion' debe ser mayor que 0.")]
    [JsonPropertyName("fk_sesion")]
    public long? FkSesion { get; set; }

    /// <summary>Quién lo emite (opcional, queda en enlace_registro.fk_usuario_crea).</summary>
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    [JsonPropertyName("regenerar")]
    public bool? Regenerar { get; set; }
}
