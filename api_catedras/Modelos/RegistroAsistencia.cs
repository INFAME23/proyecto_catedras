// ==========================================================================
//  Modelos/RegistroAsistencia.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Una fila de 'catedras.registro_asistencia': el HECHO de que un asistente
//  quedó registrado en una sesión, en un instante, con el programa y la
//  vinculación que tenía en ese momento. Es la tabla central del sistema.
//
//  Nace de dos maneras:
//   · por el flujo normal: ClaveAcceso/validar (origen QR o ENLACE);
//   · a mano: POST /api/RegistroAsistencia (origen MANUAL o IMPORTADO).
//
//  Las dos INSTANTÁNEAS ('fk_programa_snapshot', 'fk_vinculacion_snapshot')
//  las calcula el trigger de la BD (RN-22, RN-07); nunca llegan del cliente,
//  por eso NO llevan 'required'.
//
//  Nota de mapeo 'inet' -> C#: 'ip' se lee con host(ip) y viaja como string.
//  v7.1: 'latitud' y 'longitud' (numeric(9,6)) viajan como decimal?.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un asistente registrado en una sesión.</summary>
public class RegistroAsistencia
{
    [JsonPropertyName("id_registro")]
    public long IdRegistro { get; set; }

    [JsonPropertyName("fk_sesion")]
    public required long FkSesion { get; set; }

    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Enlace usado. Null si el origen es MANUAL o IMPORTADO.</summary>
    [JsonPropertyName("fk_enlace")]
    public long? FkEnlace { get; set; }

    /// <summary>Clave usada. Null si el origen es MANUAL o IMPORTADO.</summary>
    [JsonPropertyName("fk_clave")]
    public long? FkClave { get; set; }

    [JsonPropertyName("registrado_en")]
    public DateTime RegistradoEn { get; set; }

    /// <summary>Programa del asistente en ese momento. Lo pone el trigger; puede quedar null.</summary>
    [JsonPropertyName("fk_programa_snapshot")]
    public string? FkProgramaSnapshot { get; set; }

    /// <summary>Vinculación del asistente en ese momento. La pone el trigger.</summary>
    [JsonPropertyName("fk_vinculacion_snapshot")]
    public string FkVinculacionSnapshot { get; set; } = "";

    /// <summary>QR, ENLACE, MANUAL o IMPORTADO.</summary>
    [JsonPropertyName("origen")]
    public string Origen { get; set; } = "QR";

    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>RF-S1 (v7.1). Latitud que entregó el navegador al autorregistrarse.
    /// Null en registros MANUAL / IMPORTADO y anteriores a la v7.1.</summary>
    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    /// <summary>RF-S1 (v7.1). Longitud; va siempre junto a la latitud.</summary>
    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }
}
