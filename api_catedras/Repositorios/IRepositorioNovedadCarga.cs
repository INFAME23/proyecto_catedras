// ==========================================================================
//  Repositorios/IRepositorioNovedadCarga.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.novedad_carga'. SOLO LECTURA:
//  no hay CrearAsync porque una novedad nace únicamente dentro del SP que
//  cierra el lote (RF118), nunca con un INSERT de este repositorio.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de lectura sobre la tabla 'catedras.novedad_carga'.</summary>
public interface IRepositorioNovedadCarga
{
    /// <summary>Novedades por número de fila, hasta 'limite'. Con 'fkLoteCarga',
    /// solo las de ese lote.</summary>
    Task<List<NovedadCarga>> ObtenerTodosAsync(long? fkLoteCarga, int limite);

    /// <summary>La novedad con ese id, o null si no existe.</summary>
    Task<NovedadCarga?> ObtenerPorIdAsync(long idNovedad);
}
