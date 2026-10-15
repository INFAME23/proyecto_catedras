// ==========================================================================
//  Peticiones/LoteMigracionGenerar.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Body del POST /api/LoteMigracion (RF181): GENERA un lote de migración
//  para una sesión. El cliente solo dice QUÉ sesión (y, opcionalmente,
//  quién la genera): el nombre del archivo, el total de filas y las filas
//  de detalle las calcula sp_generar_lote_migracion.
//
//  Nombres en snake_case, como TODAS las peticiones del proyecto desde la
//  v1 (el borrador de version7/6_contracts.md los escribe en camelCase:
//  'fkSesion'; se corrige aquí por consistencia con las otras 35 tablas).
//
//  Sesión sin registros migrables -> la BD lanza su propia excepción -> 500.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/LoteMigracion.</summary>
public class LoteMigracionGenerar
{
    /// <summary>Sesión a migrar. Obligatoria.</summary>
    [Required(ErrorMessage = "El campo 'fk_sesion' es obligatorio.")]
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_sesion' debe ser mayor que 0.")]
    [JsonPropertyName("fk_sesion")]
    public long? FkSesion { get; set; }

    /// <summary>Usuario que genera el lote. Opcional.</summary>
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_usuario' debe ser mayor que 0.")]
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }
}
