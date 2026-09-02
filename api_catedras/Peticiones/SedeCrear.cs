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
// ==========================================================================

namespace ApiCatedras.Peticiones;

// Espacio donde viven [Required], [StringLength], etc.
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Sede.</summary>
public class SedeCrear
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
}
