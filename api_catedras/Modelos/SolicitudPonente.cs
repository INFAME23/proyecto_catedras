// ==========================================================================
//  Modelos/SolicitudPonente.cs  ·  Proyecto Cátedras · Subversión v7.1 · D56
// --------------------------------------------------------------------------
//  Una fila de 'catedras.v_solicitud_ponente': la postulación de un ponente
//  hecha desde el calendario público, con los nombres que la bandeja del ADMIN
//  necesita (tipo de documento, cátedra y sesión elegidas).
//
//  Ciclo de vida: PENDIENTE -> APROBADA | RECHAZADA (una sola vez).
//  v7.1 D58: el ponente propone una CÁTEDRA NUEVA con fecha y hora. Al aprobarla, la base de datos
//  crea/reutiliza al asistente y su documento, crea la cátedra con su sesión y asigna al ponente
//  como PONENTE (sp_resolver_solicitud_ponente).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>La postulación de un ponente y su resolución.</summary>
public class SolicitudPonente
{
    [JsonPropertyName("id_solicitud")]
    public long IdSolicitud { get; set; }

    [JsonPropertyName("nombres")]
    public string Nombres { get; set; } = "";

    [JsonPropertyName("apellidos")]
    public string Apellidos { get; set; } = "";

    [JsonPropertyName("nombre_completo")]
    public string NombreCompleto { get; set; } = "";

    /// <summary>CC, PA, LC o CI (ver migración 055).</summary>
    [JsonPropertyName("fk_tipo_documento")]
    public string FkTipoDocumento { get; set; } = "";

    [JsonPropertyName("nombre_tipo_documento")]
    public string? NombreTipoDocumento { get; set; }

    [JsonPropertyName("numero_documento")]
    public string NumeroDocumento { get; set; } = "";

    [JsonPropertyName("perfil_profesional")]
    public string PerfilProfesional { get; set; } = "";

    [JsonPropertyName("direccion")]
    public string Direccion { get; set; } = "";

    [JsonPropertyName("telefono")]
    public string Telefono { get; set; } = "";

    [JsonPropertyName("correo_institucional")]
    public string CorreoInstitucional { get; set; } = "";

    /// <summary>¿El dominio del correo está en el parámetro DOMINIOS_INSTITUCIONALES? Informativo: no bloquea.</summary>
    [JsonPropertyName("correo_es_institucional")]
    public bool CorreoEsInstitucional { get; set; }

    [JsonPropertyName("titulo_ponencia")]
    public string TituloPonencia { get; set; } = "";

    [JsonPropertyName("resumen_ponencia")]
    public string ResumenPonencia { get; set; } = "";

    /// <summary>Fecha y hora de inicio que el ponente propone para su cátedra nueva (null en las solicitudes anteriores a la 057).</summary>
    [JsonPropertyName("inicio_propuesto")]
    public DateTimeOffset? InicioPropuesto { get; set; }

    [JsonPropertyName("fin_propuesto")]
    public DateTimeOffset? FinPropuesto { get; set; }

    /// <summary>Modalidad, sede y periodo que el ponente eligió para su cátedra nueva (null en las solicitudes anteriores a la 058).</summary>
    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    [JsonPropertyName("nombre_modalidad")]
    public string? NombreModalidad { get; set; }

    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    [JsonPropertyName("nombre_sede")]
    public string? NombreSede { get; set; }

    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    /// <summary>La sesión del ponente: la de la cátedra creada al aprobar (o, en solicitudes antiguas, la que eligió). Null mientras está pendiente.</summary>
    [JsonPropertyName("fk_sesion")]
    public long? FkSesion { get; set; }

    /// <summary>La cátedra que se creó al aprobar la solicitud.</summary>
    [JsonPropertyName("fk_catedra")]
    public long? FkCatedra { get; set; }

    [JsonPropertyName("nombre_catedra")]
    public string? NombreCatedra { get; set; }

    [JsonPropertyName("numero_reunion")]
    public short? NumeroReunion { get; set; }

    [JsonPropertyName("inicio_sesion")]
    public DateTimeOffset? InicioSesion { get; set; }

    /// <summary>PENDIENTE, APROBADA o RECHAZADA.</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "PENDIENTE";

    [JsonPropertyName("observacion_admin")]
    public string? ObservacionAdmin { get; set; }

    /// <summary>La persona (asistente) creada o reutilizada al aprobar.</summary>
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    [JsonPropertyName("resuelto_por")]
    public long? ResueltoPor { get; set; }

    [JsonPropertyName("resuelto_en")]
    public DateTimeOffset? ResueltoEn { get; set; }

    [JsonPropertyName("creado_en")]
    public DateTimeOffset CreadoEn { get; set; }
}
