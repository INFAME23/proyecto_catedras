// ==========================================================================
//  Peticiones/DetalleMigracionActualizar.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Body del PATCH /api/DetalleMigracion/{idDetalle} (RF188): registra lo que
//  respondió el ASIS para UNA fila del archivo. PARCIAL, todo opcional
//  (null = "no enviado"); body vacío -> 400.
//
//  'resultado' es lista cerrada (PENDIENTE/ACEPTADO/RECHAZADO) -> 422 si no.
//  Marcar ACEPTADO un registro que ya está ACEPTADO en OTRO lote -> la BD
//  lo rechaza (ux_detmig_aceptado) -> 500. La API no lo revalida (D9).
//
//  Las columnas '*_enviado(a)' NO están: son copia literal de lo que salió
//  en el archivo y no se corrigen nunca.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/DetalleMigracion/{idDetalle}.</summary>
public class DetalleMigracionActualizar
{
    /// <summary>Resultado de la fila en el ASIS. Opcional; lista cerrada.</summary>
    [RegularExpression("^(PENDIENTE|ACEPTADO|RECHAZADO)$",
        ErrorMessage = "El campo 'resultado' debe ser PENDIENTE, ACEPTADO o RECHAZADO.")]
    [JsonPropertyName("resultado")]
    public string? Resultado { get; set; }

    /// <summary>Motivo o comentario del ASIS. Opcional, hasta 300 caracteres.</summary>
    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'mensaje' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("mensaje")]
    public string? Mensaje { get; set; }

    /// <summary>Solo los campos que llegaron, con el nombre de su columna.</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Resultado is not null) datos["resultado"] = Resultado;
        if (Mensaje is not null) datos["mensaje"] = Mensaje;

        return datos;
    }
}
