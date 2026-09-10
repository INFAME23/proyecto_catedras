// ==========================================================================
//  Servicios/ServicioTipoEvento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'TipoEvento'. Mismo patrón que ServicioSede:
//  recibe IRepositorioTipoEvento por constructor. Sin reglas de
//  coherencia entre campos.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'TipoEvento'.</summary>
public class ServicioTipoEvento : IServicioTipoEvento
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioTipoEvento _repositorio;

    public ServicioTipoEvento(IRepositorioTipoEvento repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<TipoEvento> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<TipoEvento> ObtenerAsync(string idTipoEvento)
    {
        var tipoEvento = await _repositorio.ObtenerPorIdAsync(idTipoEvento);

        if (tipoEvento is null)
            throw new NoEncontradoExcepcion($"No existe el tipo de evento '{idTipoEvento}'.");

        return tipoEvento;
    }

    public async Task CrearAsync(TipoEventoCrear peticion)
    {
        var tipoEvento = new TipoEvento
        {
            IdTipoEvento = peticion.IdTipoEvento!,
            Nombre = peticion.Nombre!,
            Activo = peticion.Activo ?? true,
        };

        await _repositorio.CrearAsync(tipoEvento);
    }

    public async Task<int> ReemplazarAsync(string idTipoEvento, TipoEventoReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idTipoEvento, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de evento '{idTipoEvento}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idTipoEvento, TipoEventoActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idTipoEvento, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de evento '{idTipoEvento}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idTipoEvento)
    {
        int filas = await _repositorio.EliminarAsync(idTipoEvento);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de evento '{idTipoEvento}'.");

        return filas;
    }
}
