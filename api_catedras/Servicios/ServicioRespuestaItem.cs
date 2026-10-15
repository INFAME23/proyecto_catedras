// ==========================================================================
//  Servicios/ServicioRespuestaItem.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Reglas de negocio de 'RespuestaItem'. Solo lectura, sin ninguna regla
//  propia: las únicas comunes (límite > 0, "no existe" -> 404).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio (solo lectura) de 'RespuestaItem'.</summary>
public class ServicioRespuestaItem : IServicioRespuestaItem
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioRespuestaItem _repositorio;

    public ServicioRespuestaItem(IRepositorioRespuestaItem repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<RespuestaItem> Datos, int Total)> ListarAsync(long? fkRespuesta, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkRespuesta, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<RespuestaItem> ObtenerAsync(long idItem)
    {
        var item = await _repositorio.ObtenerPorIdAsync(idItem);

        if (item is null)
            throw new NoEncontradoExcepcion($"No existe el ítem de respuesta '{idItem}'.");

        return item;
    }
}
