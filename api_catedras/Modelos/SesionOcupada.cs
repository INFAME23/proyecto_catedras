// ==========================================================================
//  Modelos/SesionOcupada.cs  ·  Cátedras · Subversión v7.1 · D52
// --------------------------------------------------------------------------
//  Una sesión que ocupa un horario: lo que devuelven 'sp_sesiones_en_conflicto'
//  (con 'motivos') y 'sp_agenda_sesiones' (con 'nombre_sede'). Es solo una
//  vista de consulta, no una tabla.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Sesión que ocupa un horario (consulta de disponibilidad).</summary>
public class SesionOcupada
{
    [JsonPropertyName("id_sesion")]
    public long IdSesion { get; set; }

    [JsonPropertyName("fk_catedra")]
    public long FkCatedra { get; set; }

    [JsonPropertyName("nombre_catedra")]
    public string? NombreCatedra { get; set; }

    [JsonPropertyName("numero_reunion")]
    public int NumeroReunion { get; set; }

    [JsonPropertyName("inicio")]
    public DateTime Inicio { get; set; }

    [JsonPropertyName("fin")]
    public DateTime Fin { get; set; }

    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    /// <summary>Solo en la agenda.</summary>
    [JsonPropertyName("nombre_sede")]
    public string? NombreSede { get; set; }

    [JsonPropertyName("lugar")]
    public string? Lugar { get; set; }

    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    [JsonPropertyName("estado")]
    public string? Estado { get; set; }

    /// <summary>Solo en los conflictos: LUGAR (misma sede física y salón), PONENTE (ya participa) y/o
    /// HORARIO (a menos de MINUTOS_ENTRE_SESIONES de otra sesión, de cualquier sede; v7.1 D59).</summary>
    [JsonPropertyName("motivos")]
    public List<string>? Motivos { get; set; }

    /// <summary>Solo en los conflictos: true si además se traslapa con el horario; false = solo está demasiado cerca.</summary>
    [JsonPropertyName("solapa")]
    public bool? Solapa { get; set; }
}
