// ==========================================================================
//  Servicios/ServicioTipoDocumento.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'TipoDocumento'. Mismo patrón que ServicioSede:
//  recibe IRepositorioTipoDocumento por constructor. Sin reglas de
//  coherencia entre campos.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'TipoDocumento'.</summary>
public class ServicioTipoDocumento : IServicioTipoDocumento
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioTipoDocumento _repositorio;

    public ServicioTipoDocumento(IRepositorioTipoDocumento repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<TipoDocumento> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<TipoDocumento> ObtenerAsync(string idTipoDocumento)
    {
        var tipoDocumento = await _repositorio.ObtenerPorIdAsync(idTipoDocumento);

        if (tipoDocumento is null)
            throw new NoEncontradoExcepcion($"No existe el tipo de documento '{idTipoDocumento}'.");

        return tipoDocumento;
    }

    public async Task CrearAsync(TipoDocumentoCrear peticion)
    {
        var tipoDocumento = new TipoDocumento
        {
            IdTipoDocumento = peticion.IdTipoDocumento!,
            Nombre = peticion.Nombre!,
            Activo = peticion.Activo ?? true,
        };

        await _repositorio.CrearAsync(tipoDocumento);
    }

    public async Task<int> ReemplazarAsync(string idTipoDocumento, TipoDocumentoReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idTipoDocumento, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de documento '{idTipoDocumento}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idTipoDocumento, TipoDocumentoActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idTipoDocumento, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de documento '{idTipoDocumento}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idTipoDocumento)
    {
        int filas = await _repositorio.EliminarAsync(idTipoDocumento);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de documento '{idTipoDocumento}'.");

        return filas;
    }
}
