// ==========================================================================
//  Servicios/ServicioTipoVinculacion.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'TipoVinculacion'. Mismo patrón que ServicioSede:
//  recibe IRepositorioTipoVinculacion por constructor. Sin reglas de
//  coherencia entre campos; la única particularidad es que 'es_interno'
//  es obligatorio en el POST (la petición ya lo exige con [Required],
//  así que aquí solo se transcribe el valor, sin aplicar default).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'TipoVinculacion'.</summary>
public class ServicioTipoVinculacion : IServicioTipoVinculacion
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioTipoVinculacion _repositorio;

    public ServicioTipoVinculacion(IRepositorioTipoVinculacion repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<TipoVinculacion> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<TipoVinculacion> ObtenerAsync(string idTipoVinculacion)
    {
        var tipoVinculacion = await _repositorio.ObtenerPorIdAsync(idTipoVinculacion);

        if (tipoVinculacion is null)
            throw new NoEncontradoExcepcion($"No existe el tipo de vinculación '{idTipoVinculacion}'.");

        return tipoVinculacion;
    }

    public async Task CrearAsync(TipoVinculacionCrear peticion)
    {
        var tipoVinculacion = new TipoVinculacion
        {
            IdTipoVinculacion = peticion.IdTipoVinculacion!,
            Nombre = peticion.Nombre!,
            EsInterno = peticion.EsInterno!.Value,      // obligatorio: sin default posible
            ExigePrograma = peticion.ExigePrograma ?? true,
            Activo = peticion.Activo ?? true,
        };

        await _repositorio.CrearAsync(tipoVinculacion);
    }

    public async Task<int> ReemplazarAsync(string idTipoVinculacion, TipoVinculacionReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["es_interno"] = peticion.EsInterno!.Value,
            ["exige_programa"] = peticion.ExigePrograma!.Value,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idTipoVinculacion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de vinculación '{idTipoVinculacion}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idTipoVinculacion, TipoVinculacionActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idTipoVinculacion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de vinculación '{idTipoVinculacion}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idTipoVinculacion)
    {
        int filas = await _repositorio.EliminarAsync(idTipoVinculacion);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de vinculación '{idTipoVinculacion}'.");

        return filas;
    }
}
