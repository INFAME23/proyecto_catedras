// ==========================================================================
//  Servicios/ServicioOpcionPregunta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Reglas de negocio de 'OpcionPregunta'. No tiene reglas que crucen
//  campos (D9): '(fk_pregunta, orden)' único lo vigila la BD
//  (uq_opcion_orden, 500) y la existencia de la pregunta es una FK (500).
//  Aquí quedan solo las reglas comunes: límite > 0, PATCH no vacío y "no
//  existe" -> 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'OpcionPregunta'.</summary>
public class ServicioOpcionPregunta : IServicioOpcionPregunta
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioOpcionPregunta _repositorio;

    public ServicioOpcionPregunta(IRepositorioOpcionPregunta repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<OpcionPregunta> Datos, int Total)> ListarAsync(long? fkPregunta, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkPregunta, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<OpcionPregunta> ObtenerAsync(long idOpcion)
    {
        var opcion = await _repositorio.ObtenerPorIdAsync(idOpcion);

        if (opcion is null)
            throw new NoEncontradoExcepcion($"No existe la opción de pregunta '{idOpcion}'.");

        return opcion;
    }

    public async Task<long> CrearAsync(OpcionPreguntaCrear peticion)
    {
        var opcion = new OpcionPregunta
        {
            FkPregunta = peticion.FkPregunta!.Value,
            Etiqueta = peticion.Etiqueta!,
            Valor = peticion.Valor,
            Orden = peticion.Orden!.Value,
        };

        return await _repositorio.CrearAsync(opcion);
    }

    public async Task<int> ReemplazarAsync(long idOpcion, OpcionPreguntaReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["etiqueta"] = peticion.Etiqueta!,
            ["valor"] = peticion.Valor,
            ["orden"] = peticion.Orden!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idOpcion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la opción de pregunta '{idOpcion}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idOpcion, OpcionPreguntaActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idOpcion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la opción de pregunta '{idOpcion}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idOpcion)
    {
        int filas = await _repositorio.EliminarAsync(idOpcion);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la opción de pregunta '{idOpcion}'.");

        return filas;
    }
}
