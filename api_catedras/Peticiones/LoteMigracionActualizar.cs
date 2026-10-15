// ==========================================================================
//  Peticiones/LoteMigracionActualizar.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Body del PATCH /api/LoteMigracion/{idLote} (RF182): PARCIAL, todo
//  opcional. Registra lo que va respondiendo el ASIS: el estado del proceso
//  (GENERADO -> EN_COLA -> EN_CURSO -> CORRECTO/ERROR/INCORRECTO, ADR D35)
//  y un comentario. Convención del proyecto: null = "no enviado".
//
//  Body sin ningún campo -> 400 (lo revisa el servicio).
//  'fk_estado_proceso' que no existe en el catálogo -> FK -> 500 (D9).
//
//  'instancia_proceso_asis' NO está aquí porque el borrador de la v7 no lo
//  pide; sp_actualizar_lote_migracion ya lo recibe, así que agregarlo es
//  sumar UNA propiedad aquí y una línea en ACamposEnviados.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/LoteMigracion/{idLote}.</summary>
public class LoteMigracionActualizar
{
    /// <summary>Estado nuevo del proceso (catálogo 'estado_proceso'). Opcional.</summary>
    [StringLength(12, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_estado_proceso' debe tener entre 1 y 12 caracteres.")]
    [JsonPropertyName("fk_estado_proceso")]
    public string? FkEstadoProceso { get; set; }

    /// <summary>Comentario. Opcional.</summary>
    [StringLength(2000, MinimumLength = 1,
        ErrorMessage = "El campo 'observaciones' debe tener entre 1 y 2000 caracteres.")]
    [JsonPropertyName("observaciones")]
    public string? Observaciones { get; set; }

    /// <summary>Solo los campos que llegaron, con el nombre de su columna.</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (FkEstadoProceso is not null) datos["fk_estado_proceso"] = FkEstadoProceso;
        if (Observaciones is not null) datos["observaciones"] = Observaciones;

        return datos;
    }
}
