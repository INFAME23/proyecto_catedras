// ==========================================================================
//  Servicios/ServicioTipoPregunta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'TipoPregunta'. Mismo patrón que
//  ServicioEstadoProceso, pero con TRES campos en vez de dos:
//  chk_tipo_pregunta_destino exige que EXACTAMENTE una de
//  guarda_numero/guarda_texto/guarda_opcion sea true. Cruza tres
//  propiedades -> vive AQUÍ, como ArgumentException -> 400 (3_plan.md
//  §4.8). En POST/PUT el servicio SIEMPRE tiene los tres valores (llegan
//  o toman su default false) y valida siempre; en PATCH solo puede
//  validar cuando el cliente manda LOS TRES juntos — si manda uno o dos,
//  el CHECK de la BD queda como respaldo.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'TipoPregunta'.</summary>
public class ServicioTipoPregunta : IServicioTipoPregunta
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioTipoPregunta _repositorio;

    public ServicioTipoPregunta(IRepositorioTipoPregunta repositorio)
    {
        _repositorio = repositorio;
    }

    // Regla de negocio compartida: exactamente uno de los tres destinos.
    private static void ValidarDestinoUnico(bool guardaNumero, bool guardaTexto, bool guardaOpcion)
    {
        int cuantos = (guardaNumero ? 1 : 0) + (guardaTexto ? 1 : 0) + (guardaOpcion ? 1 : 0);

        if (cuantos != 1)
            throw new ArgumentException(
                "Exactamente uno de 'guarda_numero', 'guarda_texto' o 'guarda_opcion' debe ser true.");
    }

    public async Task<(List<TipoPregunta> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<TipoPregunta> ObtenerAsync(string idTipoPregunta)
    {
        var tipoPregunta = await _repositorio.ObtenerPorIdAsync(idTipoPregunta);

        if (tipoPregunta is null)
            throw new NoEncontradoExcepcion($"No existe el tipo de pregunta '{idTipoPregunta}'.");

        return tipoPregunta;
    }

    public async Task CrearAsync(TipoPreguntaCrear peticion)
    {
        bool guardaNumero = peticion.GuardaNumero ?? false;
        bool guardaTexto = peticion.GuardaTexto ?? false;
        bool guardaOpcion = peticion.GuardaOpcion ?? false;

        ValidarDestinoUnico(guardaNumero, guardaTexto, guardaOpcion);

        var tipoPregunta = new TipoPregunta
        {
            IdTipoPregunta = peticion.IdTipoPregunta!,
            Nombre = peticion.Nombre!,
            GuardaNumero = guardaNumero,
            GuardaTexto = guardaTexto,
            GuardaOpcion = guardaOpcion,
        };

        await _repositorio.CrearAsync(tipoPregunta);
    }

    public async Task<int> ReemplazarAsync(string idTipoPregunta, TipoPreguntaReemplazo peticion)
    {
        bool guardaNumero = peticion.GuardaNumero!.Value;
        bool guardaTexto = peticion.GuardaTexto!.Value;
        bool guardaOpcion = peticion.GuardaOpcion!.Value;

        ValidarDestinoUnico(guardaNumero, guardaTexto, guardaOpcion);

        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["guarda_numero"] = guardaNumero,
            ["guarda_texto"] = guardaTexto,
            ["guarda_opcion"] = guardaOpcion,
        };

        int filas = await _repositorio.ActualizarAsync(idTipoPregunta, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de pregunta '{idTipoPregunta}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idTipoPregunta, TipoPreguntaActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // Solo se puede validar la coherencia cuando llegan LOS TRES campos
        // juntos en esta petición (3_plan.md §4.8); si llegan uno o dos, el
        // CHECK de la BD queda como respaldo.
        if (peticion.GuardaNumero is not null && peticion.GuardaTexto is not null && peticion.GuardaOpcion is not null)
            ValidarDestinoUnico(peticion.GuardaNumero.Value, peticion.GuardaTexto.Value, peticion.GuardaOpcion.Value);

        int filas = await _repositorio.ActualizarAsync(idTipoPregunta, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de pregunta '{idTipoPregunta}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idTipoPregunta)
    {
        int filas = await _repositorio.EliminarAsync(idTipoPregunta);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el tipo de pregunta '{idTipoPregunta}'.");

        return filas;
    }
}
