// ==========================================================================
//  Servicios/ServicioParametro.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Parametro'. Mismo patrón que ServicioSede: valida
//  el 'limite', arma el modelo / el diccionario de columnas y traduce
//  "0 filas" a NoEncontradoExcepcion. No sabe de HTTP ni de SQL.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio del recurso 'Parametro'.</summary>
public class ServicioParametro : IServicioParametro
{
    private const int LimitePorDefecto = 1000;
    private const string TipoDatoPorDefecto = "TEXTO";

    private readonly IRepositorioParametro _repositorio;

    public ServicioParametro(IRepositorioParametro repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Parametro> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Parametro> ObtenerAsync(string clave)
    {
        var parametro = await _repositorio.ObtenerPorIdAsync(clave);

        if (parametro is null)
            throw new NoEncontradoExcepcion($"No existe el parámetro '{clave}'.");

        return parametro;
    }

    public async Task CrearAsync(ParametroCrear peticion)
    {
        var parametro = new Parametro
        {
            Clave = peticion.Clave!,
            Valor = peticion.Valor!,
            TipoDato = peticion.TipoDato ?? TipoDatoPorDefecto,   // DEFAULT de la columna
            Descripcion = peticion.Descripcion!,
            FkUsuarioModifica = peticion.FkUsuarioModifica,       // opcional
        };

        await _repositorio.CrearAsync(parametro);
    }

    public async Task<int> ReemplazarAsync(string clave, ParametroReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["valor"] = peticion.Valor!,
            ["tipo_dato"] = peticion.TipoDato!,
            ["descripcion"] = peticion.Descripcion!,
            ["fk_usuario_modifica"] = peticion.FkUsuarioModifica,   // si no vino -> NULL
        };

        int filas = await _repositorio.ActualizarAsync(clave, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el parámetro '{clave}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string clave, ParametroActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(clave, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el parámetro '{clave}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string clave)
    {
        int filas = await _repositorio.EliminarAsync(clave);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el parámetro '{clave}'.");

        return filas;
    }
}
