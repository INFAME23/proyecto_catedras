// ==========================================================================
//  Peticiones/LoteCargaAsistenteCrear.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del POST /api/LoteCargaAsistente (RF115): ABRE un lote de carga.
//  Solo 'nombre_archivo'. NO expone los 3 contadores (la BD los pone en 0
//  al crear) ni 'fk_usuario' (siempre null hasta la v5: la tabla 'usuario'
//  todavía no existe en la API — salvedad de la FK circular, mapa §3).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/LoteCargaAsistente.</summary>
public class LoteCargaAsistenteCrear
{
    /// <summary>Nombre del archivo que se va a cargar. Obligatorio, 1 a 200 caracteres.</summary>
    [Required(ErrorMessage = "El campo 'nombre_archivo' es obligatorio.")]
    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_archivo' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre_archivo")]
    public string? NombreArchivo { get; set; }
}
