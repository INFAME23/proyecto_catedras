// ==========================================================================
//  Servicios/ServicioNovedadCarga.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'NovedadCarga'. Solo lectura: las novedades nacen
//  al cerrar un lote (ServicioLoteCargaAsistente), nunca por aquí.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio (solo lectura) de 'NovedadCarga'.</summary>
public class ServicioNovedadCarga : IServicioNovedadCarga
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioNovedadCarga _repositorio;

    public ServicioNovedadCarga(IRepositorioNovedadCarga repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<NovedadCarga> Datos, int Total)> ListarAsync(long? fkLoteCarga, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkLoteCarga, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<NovedadCarga> ObtenerAsync(long idNovedad)
    {
        var novedad = await _repositorio.ObtenerPorIdAsync(idNovedad);

        if (novedad is null)
            throw new NoEncontradoExcepcion($"No existe la novedad '{idNovedad}'.");

        return novedad;
    }
}
