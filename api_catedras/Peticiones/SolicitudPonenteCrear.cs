// ==========================================================================
//  Peticiones/SolicitudPonenteCrear.cs  ·  Proyecto Cátedras · Subversión v7.1 · D56
// --------------------------------------------------------------------------
//  Body del POST /api/SolicitudPonente: lo que llena la persona en el
//  formulario «Soy ponente» del calendario. Las reglas reflejan los CHECK de
//  catedras.solicitud_ponente (migración 055); un campo mal escrito responde
//  422 con la lista { campo, mensaje } y el controlador no se ejecuta.
//
//  v7.1 D58: la persona propone una CÁTEDRA NUEVA y elige la fecha y la hora ('inicio' y 'fin')
//  en que quiere dictarla; ya no se postula a una sesión que existe. También elige la modalidad, la sede y el
//  periodo académico (el administrador los puede cambiar al aprobar).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/SolicitudPonente.</summary>
public class SolicitudPonenteCrear
{
    [Required(ErrorMessage = "El campo 'nombres' es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El campo 'nombres' debe tener entre 2 y 100 caracteres.")]
    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    [Required(ErrorMessage = "El campo 'apellidos' es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El campo 'apellidos' debe tener entre 2 y 100 caracteres.")]
    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    /// <summary>Cédula (CC), pasaporte (PA), licencia de conducción (LC) o código institucional (CI).</summary>
    [Required(ErrorMessage = "El campo 'fk_tipo_documento' es obligatorio.")]
    [RegularExpression("^(CC|PA|LC|CI)$",
        ErrorMessage = "El campo 'fk_tipo_documento' debe ser CC, PA, LC o CI.")]
    [JsonPropertyName("fk_tipo_documento")]
    public string? FkTipoDocumento { get; set; }

    [Required(ErrorMessage = "El campo 'numero_documento' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'numero_documento' debe tener de 4 a 20 letras, números o guiones.")]
    [JsonPropertyName("numero_documento")]
    public string? NumeroDocumento { get; set; }

    [Required(ErrorMessage = "El campo 'perfil_profesional' es obligatorio.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "El campo 'perfil_profesional' debe tener entre 3 y 200 caracteres.")]
    [JsonPropertyName("perfil_profesional")]
    public string? PerfilProfesional { get; set; }

    [Required(ErrorMessage = "El campo 'direccion' es obligatorio.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "El campo 'direccion' debe tener entre 5 y 200 caracteres.")]
    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }

    [Required(ErrorMessage = "El campo 'telefono' es obligatorio.")]
    [RegularExpression(@"^\+?[0-9 ()-]{7,20}$",
        ErrorMessage = "El campo 'telefono' debe tener de 7 a 20 caracteres: números, espacios, paréntesis, guiones y un '+' al inicio.")]
    [JsonPropertyName("telefono")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "El campo 'correo_institucional' es obligatorio.")]
    [StringLength(254, ErrorMessage = "El campo 'correo_institucional' no puede pasar de 254 caracteres.")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        ErrorMessage = "El campo 'correo_institucional' no es un correo válido.")]
    [JsonPropertyName("correo_institucional")]
    public string? CorreoInstitucional { get; set; }

    [Required(ErrorMessage = "El campo 'titulo_ponencia' es obligatorio.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "El campo 'titulo_ponencia' debe tener entre 5 y 200 caracteres.")]
    [JsonPropertyName("titulo_ponencia")]
    public string? TituloPonencia { get; set; }

    [Required(ErrorMessage = "El campo 'resumen_ponencia' es obligatorio.")]
    [StringLength(2000, MinimumLength = 30, ErrorMessage = "El campo 'resumen_ponencia' debe tener entre 30 y 2000 caracteres.")]
    [JsonPropertyName("resumen_ponencia")]
    public string? ResumenPonencia { get; set; }

    /// <summary>Fecha y hora en que el ponente quiere dictar su cátedra nueva (con zona: 2026-11-20T14:00:00-05:00).</summary>
    [Required(ErrorMessage = "El campo 'inicio' es obligatorio (fecha y hora con zona, p. ej. 2026-11-20T14:00:00-05:00).")]
    [JsonPropertyName("inicio")]
    public DateTimeOffset? Inicio { get; set; }

    [Required(ErrorMessage = "El campo 'fin' es obligatorio (fecha y hora con zona, p. ej. 2026-11-20T16:00:00-05:00).")]
    [JsonPropertyName("fin")]
    public DateTimeOffset? Fin { get; set; }

    /// <summary>Modalidad de la cátedra (PRESENCIAL, VIRTUAL, TELEPRESENCIAL, HIBRIDA...: las del catálogo). Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'fk_modalidad' es obligatorio.")]
    [StringLength(15, MinimumLength = 1, ErrorMessage = "El campo 'fk_modalidad' no puede pasar de 15 caracteres.")]
    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    /// <summary>Sede (del catálogo; debe estar activa). Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'fk_sede' es obligatorio.")]
    [StringLength(15, MinimumLength = 1, ErrorMessage = "El campo 'fk_sede' no puede pasar de 15 caracteres.")]
    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    /// <summary>Periodo académico (p. ej. 2026-2): debe estar activo y contener la fecha. Obligatorio.</summary>
    [Required(ErrorMessage = "El campo 'fk_periodo' es obligatorio.")]
    [RegularExpression(@"^[0-9]{4}-[1-3]$", ErrorMessage = "El campo 'fk_periodo' debe tener el formato AAAA-N (p. ej. 2026-2).")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }
}
