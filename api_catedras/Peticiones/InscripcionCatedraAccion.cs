// ==========================================================================
//  Peticiones/InscripcionCatedraAccion.cs  ·  Cátedras · Subversión v7.1 · D50
// --------------------------------------------------------------------------
//  Body de POST /api/InscripcionCatedra (inscribir) y de
//  POST /api/InscripcionCatedra/cancelar. Las dos llevan lo mismo: la cátedra
//  y la persona. Esta API no autoriza (ADR D28): que el estudiante solo se
//  inscriba a sí mismo lo garantiza el front, que toma la persona de la sesión.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class InscripcionCatedraAccion
{
    [Required(ErrorMessage = "El campo 'fk_catedra' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_catedra' debe ser mayor que 0.")]
    [JsonPropertyName("fk_catedra")]
    public long? FkCatedra { get; set; }

    [Required(ErrorMessage = "El campo 'fk_asistente' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }
}
