// ==========================================================================
//  Repositorios/IRepositorioLoteCargaAsistente.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.lote_carga_asistente'. 4
//  operaciones: listar, obtener, crear (abrir) y CERRAR. SIN
//  ActualizarAsync ni EliminarAsync (RF117): el lote es la bitácora de una
//  ejecución ya ocurrida.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.lote_carga_asistente'.</summary>
public interface IRepositorioLoteCargaAsistente
{
    /// <summary>Lotes, del más reciente al más antiguo, hasta 'limite'.</summary>
    Task<List<LoteCargaAsistente>> ObtenerTodosAsync(int limite);

    /// <summary>El lote con ese id, o null si no existe.</summary>
    Task<LoteCargaAsistente?> ObtenerPorIdAsync(long idLoteCarga);

    /// <summary>Inserta (abre) un lote y devuelve lo que generó la BD.</summary>
    Task<(long IdLoteCarga, DateTime CargadoEn)> CrearAsync(LoteCargaAsistente lote);

    /// <summary>Cierra el lote: contadores finales + novedades en UNA sola
    /// transacción, con el SP 'sp_guardar_lote_carga' (Guía IA, Regla 1).</summary>
    Task CerrarAsync(LoteCargaAsistente lote, IReadOnlyList<NovedadCarga> novedades);
}
