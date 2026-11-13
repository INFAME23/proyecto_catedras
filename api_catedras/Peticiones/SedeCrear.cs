// ==========================================================================
//  Peticiones/SedeCrear.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Una PETICIÓN describe el BODY JSON que llega en UN verbo concreto.
//  NO es el modelo (modelo = entidad = fila de la tabla). Por eso vive en
//  su propia carpeta 'Peticiones/' (ver 4_research.md, D5).
//
//  Esta es la petición del POST /api/Sede: CREAR una sede nueva.
//  Regla del POST: TODOS los campos obligatorios (salvo 'direccion', que la
//  columna admite NULL). ASP.NET valida el body contra estas anotaciones
//  ANTES de entrar al controlador; si algo falla responde 422 con la lista
//  de errores (formato definido en Program.cs).
//
//  "El tipo también es regla": 'es_virtual' y 'activo' son 'bool?'. Si el
//  cliente manda "texto" o un número, no encaja y cae en 422 sin escribir
//  ni un 'if'.
//
//  [SOLID · S] Una petición por verbo (SedeCrear/SedeReemplazo/SedeActualizar):
//  cada clase tiene la ÚNICA responsabilidad de describir el body de SU
//  verbo. [SOLID · I] El controlador de POST solo depende de SedeCrear, no
//  de un DTO gigante compartido.
//  [FLUJO 2] Estas anotaciones son la frontera de "forma": se comprueban
//  ANTES del controlador; si fallan -> 422 y la BD no se toca.
//  [ACID · C] Es la primera línea de defensa de la Consistencia: filtra
//  basura antes de que llegue al motor.
// ==========================================================================

namespace ApiCatedras.Peticiones;

// Espacio donde viven [Required], [StringLength], etc.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Sede.</summary>
public class SedeCrear : IValidatableObject
{
    /// <summary>Código de la sede. Obligatorio, 1 a 15 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'id_sede' es obligatorio.")]
    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'id_sede' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("id_sede")]
    public string? IdSede { get; set; }

    /// <summary>Nombre de la sede. Obligatorio, no vacío, hasta 80 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre' es obligatorio.")]
    [StringLength(80, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 80 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    /// <summary>Dirección física. Opcional; si llega, hasta 200 caracteres.</summary>
    [StringLength(200,
        ErrorMessage = "El campo 'direccion' no puede pasar de 200 caracteres.")]
    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }

    /// <summary>¿Es virtual? Obligatorio en el POST (true o false).</summary>
    [Required(ErrorMessage = "El campo 'es_virtual' es obligatorio (true o false).")]
    [JsonPropertyName("es_virtual")]
    public bool? EsVirtual { get; set; }

    /// <summary>¿Está activa? Obligatorio en el POST (true o false).</summary>
    [Required(ErrorMessage = "El campo 'activo' es obligatorio (true o false).")]
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    // ----------------------------------------------------------------------
    //  v7.2 (RF-S11): ubicación de la sede. Los tres campos son OPCIONALES:
    //  una sede virtual no los lleva, y una sede física sin ellos no podrá
    //  generar QR (migración 062) hasta que se completen.
    // ----------------------------------------------------------------------

    /// <summary>Latitud en grados, entre -90 y 90. Va junto a la longitud.</summary>
    [Range(-90.0, 90.0, ErrorMessage = "El campo 'latitud' debe estar entre -90 y 90.")]
    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    /// <summary>Longitud en grados, entre -180 y 180. Va junto a la latitud.</summary>
    [Range(-180.0, 180.0, ErrorMessage = "El campo 'longitud' debe estar entre -180 y 180.")]
    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }

    /// <summary>Radio propio de la sede en metros (10 a 5000). Sin él se usa el parámetro global.</summary>
    [Range(10, 5000, ErrorMessage = "El campo 'radio_metros' debe estar entre 10 y 5000.")]
    [JsonPropertyName("radio_metros")]
    public int? RadioMetros { get; set; }

    /// <summary>Regla de dos campos: latitud y longitud juntas o ninguna (-> 422).</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext contexto) =>
        ValidacionGeo.LatitudYLongitudJuntas(Latitud, Longitud);
}
