// ==========================================================================
//  Modelos/InscripcionCatedra.cs  ·  Cátedras · Subversión v7.1 · D50
// --------------------------------------------------------------------------
//  Una fila de 'catedras.inscripcion_catedra' (migración 049), con el nombre
//  de la cátedra y de la persona que trae 'sp_listar_inscripcion_catedra'.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Inscripción de una persona a una cátedra.</summary>
public class InscripcionCatedra
{
    [JsonPropertyName("id_inscripcion")]
    public long IdInscripcion { get; set; }

    [JsonPropertyName("fk_catedra")]
    public long FkCatedra { get; set; }

    [JsonPropertyName("id_evento_asis")]
    public string? IdEventoAsis { get; set; }

    [JsonPropertyName("nombre_catedra")]
    public string? NombreCatedra { get; set; }

    [JsonPropertyName("fk_asistente")]
    public long FkAsistente { get; set; }

    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    /// <summary>ACTIVA o CANCELADA.</summary>
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "";

    [JsonPropertyName("inscrita_en")]
    public DateTime InscritaEn { get; set; }

    [JsonPropertyName("cancelada_en")]
    public DateTime? CanceladaEn { get; set; }
}
