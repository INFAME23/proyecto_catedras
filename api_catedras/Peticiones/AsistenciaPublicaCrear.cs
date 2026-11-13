// ==========================================================================
//  Peticiones/AsistenciaPublicaCrear.cs  ·  Subversión v7.2 · RF-S9
// --------------------------------------------------------------------------
//  Los tres bodies del registro público de asistencia (sin login, sin clave
//  por correo). Comparten el token del QR, la ubicación del navegador y los
//  datos del cliente (ip y user_agent, que los toma el front de la petición,
//  no el formulario), así que viven en una clase base.
//
//   · AsistenciaEstudianteCrear -> POST /api/AsistenciaPublica/estudiante
//   · AsistenciaDocenteCrear    -> POST /api/AsistenciaPublica/docente
//   · AsistenciaExternoCrear    -> POST /api/AsistenciaPublica/externo
//
//  Validaciones de FORMA (anotaciones -> 422, la BD no se toca). Las de
//  NEGOCIO (enlace vigente, persona, duplicado, ubicación contra la sede) las
//  hace el procedimiento almacenado y vuelven como un 'estado'.
//
//  La ubicación es OPCIONAL en el body porque depende del canal del enlace
//  (solo el canal QR la exige): quien lo decide es la base de datos, no esta
//  clase. Lo que sí se valida aquí es que, si llega, sea coherente (rango y
//  latitud con longitud).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Campos comunes de los tres registros públicos.</summary>
public abstract class AsistenciaPublicaBase : IValidatableObject
{
    /// <summary>Token (UUID) del enlace de registro: es lo que va dentro del QR.</summary>
    [Required(ErrorMessage = "El campo 'token' es obligatorio.")]
    [JsonPropertyName("token")]
    public Guid? Token { get; set; }

    /// <summary>Latitud del navegador, entre -90 y 90. Va junto a la longitud.</summary>
    [Range(-90.0, 90.0, ErrorMessage = "El campo 'latitud' debe estar entre -90 y 90.")]
    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    /// <summary>Longitud del navegador, entre -180 y 180. Va junto a la latitud.</summary>
    [Range(-180.0, 180.0, ErrorMessage = "El campo 'longitud' debe estar entre -180 y 180.")]
    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }

    /// <summary>IP de quien registra (la toma el front). Opcional; si llega, debe ser una IP válida (lo comprueba el servicio).</summary>
    [StringLength(45, ErrorMessage = "El campo 'ip' no puede pasar de 45 caracteres.")]
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>Navegador de quien registra. Opcional; la BD guarda hasta 300 caracteres.</summary>
    [StringLength(1000, ErrorMessage = "El campo 'user_agent' es demasiado largo.")]
    [JsonPropertyName("user_agent")]
    public string? UserAgent { get; set; }

    /// <summary>Latitud y longitud van juntas o ninguna (-> 422).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto) =>
        ValidacionGeo.LatitudYLongitudJuntas(Latitud, Longitud);
}

/// <summary>Body del POST /api/AsistenciaPublica/estudiante: se identifica con su carnet.</summary>
public class AsistenciaEstudianteCrear : AsistenciaPublicaBase
{
    /// <summary>Código de estudiante (asistente.id_asis): 5 a 15 letras o dígitos.</summary>
    [Required(ErrorMessage = "El campo 'carnet' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z]{5,15}$",
        ErrorMessage = "El campo 'carnet' debe tener entre 5 y 15 letras o dígitos.")]
    [JsonPropertyName("carnet")]
    public string? Carnet { get; set; }
}

/// <summary>Body del POST /api/AsistenciaPublica/docente.</summary>
public class AsistenciaDocenteCrear : AsistenciaPublicaBase
{
    /// <summary>Cédula: 4 a 20 letras, dígitos o guiones (mismo patrón que documento_asistente).</summary>
    [Required(ErrorMessage = "El campo 'cedula' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'cedula' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("cedula")]
    public string? Cedula { get; set; }

    /// <summary>Nombre completo, en un solo campo (1 a 200 caracteres).</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "El campo 'nombre' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Contacto: UN solo campo. Con '@' es un correo; si no, un celular.</summary>
    [Required(ErrorMessage = "El campo 'contacto' es obligatorio.")]
    [StringLength(254, MinimumLength = 1, ErrorMessage = "El campo 'contacto' debe tener entre 1 y 254 caracteres.")]
    [JsonPropertyName("contacto")]
    public string? Contacto { get; set; }

    /// <summary>Facultad del docente: id de GET /api/Facultad (Regla 2: sale de un select).</summary>
    [Required(ErrorMessage = "El campo 'fk_facultad' es obligatorio.")]
    [StringLength(10, MinimumLength = 1, ErrorMessage = "El campo 'fk_facultad' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("fk_facultad")]
    public string? FkFacultad { get; set; }
}

/// <summary>Body del POST /api/AsistenciaPublica/externo.</summary>
public class AsistenciaExternoCrear : AsistenciaPublicaBase
{
    /// <summary>Cédula: 4 a 20 letras, dígitos o guiones.</summary>
    [Required(ErrorMessage = "El campo 'cedula' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'cedula' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("cedula")]
    public string? Cedula { get; set; }

    /// <summary>Nombre completo, en un solo campo (1 a 200 caracteres).</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "El campo 'nombre' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Contacto: UN solo campo (correo si trae '@', celular si no).</summary>
    [Required(ErrorMessage = "El campo 'contacto' es obligatorio.")]
    [StringLength(254, MinimumLength = 1, ErrorMessage = "El campo 'contacto' debe tener entre 1 y 254 caracteres.")]
    [JsonPropertyName("contacto")]
    public string? Contacto { get; set; }

    /// <summary>De dónde viene (institución, empresa, ciudad…). Texto libre, 1 a 150 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'procedencia' es obligatorio.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "El campo 'procedencia' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("procedencia")]
    public string? Procedencia { get; set; }
}
