// ==========================================================================
//  Repositorios/IRepositorioLoteMigracion.cs  ·  Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.lote_migracion'. 4 operaciones
//  (RF179-RF185): listar, obtener, crear (GENERAR) y actualizar. SIN
//  EliminarAsync: borrar un lote borraría la evidencia de un intento de
//  migración real (RF185). Que ese método NO exista aquí es parte de la
//  especificación, igual que en IRepositorioLoteCargaAsistente.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.lote_migracion'.</summary>
public interface IRepositorioLoteMigracion
{
    /// <summary>Lotes, del más reciente al más antiguo, hasta 'limite'. Con
    /// 'fkSesion', solo los de esa sesión.</summary>
    Task<List<LoteMigracion>> ObtenerTodosAsync(long? fkSesion, int limite);

    /// <summary>El lote con ese id, o null si no existe.</summary>
    Task<LoteMigracion?> ObtenerPorIdAsync(long idLoteMigracion);

    /// <summary>GENERA el lote de la sesión: maestro + una fila de detalle por
    /// registro migrable + sesión en 'MIGRADA', todo en UNA transacción
    /// (sp_crear_lote_migracion -> sp_generar_lote_migracion). Devuelve el
    /// lote como quedó guardado. Sin registros migrables -> NpgsqlException.</summary>
    Task<LoteMigracion> CrearAsync(long fkSesion, long? fkUsuario);

    /// <summary>Actualiza las columnas de 'datos' (consulta + mezcla +
    /// sp_actualizar). Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idLoteMigracion, Dictionary<string, object?> datos);
}
