// ==========================================================================
//  Repositorios/IRepositorioDetalleMigracion.cs  ·  Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.detalle_migracion'. 3
//  operaciones (RF186-RF189): listar, obtener y actualizar el resultado.
//  SIN CrearAsync: un detalle nace únicamente al generar su lote
//  (sp_generar_lote_migracion), igual que NovedadCarga en la v3. SIN
//  EliminarAsync: es la evidencia de lo que se envió al ASIS.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.detalle_migracion'.</summary>
public interface IRepositorioDetalleMigracion
{
    /// <summary>Detalles por lote e id, hasta 'limite'. Con 'fkLoteMigracion',
    /// solo los de ese lote.</summary>
    Task<List<DetalleMigracion>> ObtenerTodosAsync(long? fkLoteMigracion, int limite);

    /// <summary>El detalle con ese id, o null si no existe.</summary>
    Task<DetalleMigracion?> ObtenerPorIdAsync(long idDetalle);

    /// <summary>Actualiza 'resultado'/'mensaje' (consulta + mezcla +
    /// sp_actualizar). Devuelve filas afectadas (0 = no existe). Un segundo
    /// ACEPTADO del mismo registro -> NpgsqlException (ux_detmig_aceptado).</summary>
    Task<int> ActualizarAsync(long idDetalle, Dictionary<string, object?> datos);
}
