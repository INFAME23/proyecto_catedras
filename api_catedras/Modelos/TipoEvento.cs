// ==========================================================================
//  Modelos/TipoEvento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  El MODELO es la ENTIDAD: una fila de 'catedras.tipo_evento'.
//
//  Definición real de la tabla (ver 5_data_model.md §2.8):
//    id_tipo_evento  CHAR(4)      NOT NULL  PK  -- longitud FIJA
//    nombre          VARCHAR(80)  NOT NULL      -- SIN restricción de unicidad
//    activo          BOOLEAN      NOT NULL  DEFAULT true
//
//  [SOLID · S] Solo representa la fila. A diferencia de Sede/Rol/
//  TipoDocumento, aquí 'nombre' NO es único (no existe uq_tipo_evento_nom):
//  dos tipos de evento SÍ pueden compartir nombre.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Clasificación del evento en el ASIS (p. ej. CAAB = Cátedra abierta).</summary>
public class TipoEvento
{
    /// <summary>Código único del tipo de evento. Clave primaria. Exactamente 4 caracteres.</summary>
    [JsonPropertyName("id_tipo_evento")]
    public required string IdTipoEvento { get; set; }

    /// <summary>Nombre visible del tipo de evento. Obligatorio (no único).</summary>
    [JsonPropertyName("nombre")]
    public required string Nombre { get; set; }

    /// <summary>¿Está activo? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}
