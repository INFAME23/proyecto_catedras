// ==========================================================================
//  Modelos/ClaveAcceso.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Una fila de 'catedras.clave_acceso': la clave de UN solo uso que recibe un
//  asistente para confirmar su registro (RN-17). Solo se consulta, para
//  auditoría: se crea con "solicitar" y se consume con "validar" (ADR D24).
//
//  SEGURIDAD (RNF12, RF151):
//   · 'clave_hash' (bytea, sha256(clave + enviado_a), RN-34) lleva
//     [JsonIgnore] y el repositorio NI SIQUIERA la lee: no hay razón para
//     traerla a memoria. La propiedad existe para que el modelo refleje la
//     tabla y para que, si algún día se lee, no pueda salir en un JSON.
//   · La clave en texto plano NO está en el modelo: la API solo la conoce en
//     la respuesta de "solicitar".
//
//  Nota de mapeo 'inet' -> C#: 'ip_solicitud' se lee con host(ip_solicitud)
//  y viaja como string (ver el repositorio).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una clave de un solo uso emitida a un asistente para un enlace.</summary>
public class ClaveAcceso
{
    [JsonPropertyName("id_clave")]
    public long IdClave { get; set; }

    /// <summary>Asistente al que se le emitió (lo resuelve fn_resolver_asistente).</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Enlace de registro con el que se pidió.</summary>
    [JsonPropertyName("fk_enlace")]
    public required long FkEnlace { get; set; }

    /// <summary>Hash de la clave. NUNCA se serializa (RNF12).</summary>
    [JsonIgnore]
    public byte[] ClaveHash { get; set; } = [];

    /// <summary>Correo al que "se envió" la clave (institucional si existe, RN-19).</summary>
    [JsonPropertyName("enviado_a")]
    public required string EnviadoA { get; set; }

    [JsonPropertyName("es_correo_institucional")]
    public bool EsCorreoInstitucional { get; set; }

    [JsonPropertyName("generado_en")]
    public DateTime GeneradoEn { get; set; }

    /// <summary>Hasta cuándo sirve (por defecto 10 minutos después de generarla).</summary>
    [JsonPropertyName("expira_en")]
    public DateTime ExpiraEn { get; set; }

    /// <summary>Cuándo se consumió. Null = todavía sin usar.</summary>
    [JsonPropertyName("usado_en")]
    public DateTime? UsadoEn { get; set; }

    /// <summary>Intentos fallidos. Ver RF153: en el script actual de la BD no se acumulan.</summary>
    [JsonPropertyName("intentos")]
    public short Intentos { get; set; }

    [JsonPropertyName("ip_solicitud")]
    public string? IpSolicitud { get; set; }
}
