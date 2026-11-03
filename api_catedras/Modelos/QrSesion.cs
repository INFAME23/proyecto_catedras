// ==========================================================================
//  Modelos/QrSesion.cs  ·  Cátedras · Subversión v7.1 · D53
// --------------------------------------------------------------------------
//  El QR de cierre de una sesión (una fila de 'enlace_registro' con
//  requiere_inscripcion = true) y la fila de asistencia que devuelve
//  'sp_asistencia_sesion'. Son vistas de consulta, no tablas nuevas.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>El QR de una sesión: el enlace de registro con su ventana de tiempo.</summary>
public class QrSesion
{
    [JsonPropertyName("id_enlace")]
    public long IdEnlace { get; set; }

    [JsonPropertyName("fk_sesion")]
    public long FkSesion { get; set; }

    /// <summary>Lo que va dentro del QR (la URL del front lleva este token).</summary>
    [JsonPropertyName("token")]
    public Guid Token { get; set; }

    /// <summary>QR (presencial, híbrida) o ENLACE (virtual, telepresencial), según la modalidad de la sesión (RN-13).</summary>
    [JsonPropertyName("canal")]
    public string? Canal { get; set; }

    [JsonPropertyName("ventana_desde")]
    public DateTime VentanaDesde { get; set; }

    [JsonPropertyName("ventana_hasta")]
    public DateTime VentanaHasta { get; set; }

    [JsonPropertyName("revocado_en")]
    public DateTime? RevocadoEn { get; set; }

    [JsonPropertyName("requiere_inscripcion")]
    public bool RequiereInscripcion { get; set; }

    /// <summary>true si ya había uno vigente y se devolvió ese; false si se acaba de crear.</summary>
    [JsonPropertyName("ya_existia")]
    public bool YaExistia { get; set; }
}

/// <summary>Una persona en la lista de asistencia de una sesión.</summary>
public class AsistenciaSesion
{
    [JsonPropertyName("fk_asistente")]
    public long FkAsistente { get; set; }

    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    /// <summary>¿Tiene inscripción ACTIVA en la cátedra de la sesión?</summary>
    [JsonPropertyName("inscrito")]
    public bool Inscrito { get; set; }

    /// <summary>¿Registró asistencia a ESTA sesión?</summary>
    [JsonPropertyName("asistio")]
    public bool Asistio { get; set; }

    [JsonPropertyName("registrado_en")]
    public DateTime? RegistradoEn { get; set; }

    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }

    [JsonPropertyName("origen")]
    public string? Origen { get; set; }
}
