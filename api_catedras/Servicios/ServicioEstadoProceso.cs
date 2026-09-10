// ==========================================================================
//  Servicios/ServicioEstadoProceso.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'EstadoProceso'. Mismo patrón que ServicioSede:
//  recibe IRepositorioEstadoProceso por constructor.
//
//  Regla de coherencia (chk_estado_coherente en la BD): "es_exitoso"
//  implica "es_final". Es una relación entre DOS campos -> no la puede
//  expresar una anotación de un solo campo, así que vive AQUÍ, como
//  ArgumentException -> 400 (3_plan.md §4.8). En POST/PUT el servicio
//  SIEMPRE tiene los dos valores en la mano (llegan o toman su
//  default) y valida siempre; en PATCH solo puede validar cuando el
//  cliente manda los dos campos juntos — si manda uno solo, la
//  coherencia con el valor ya guardado la vigila el CHECK de la BD.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'EstadoProceso'.</summary>
public class ServicioEstadoProceso : IServicioEstadoProceso
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioEstadoProceso _repositorio;

    public ServicioEstadoProceso(IRepositorioEstadoProceso repositorio)
    {
        _repositorio = repositorio;
    }

    // Regla de negocio compartida: "exitoso" exige "final".
    private static void ValidarCoherencia(bool esFinal, bool esExitoso)
    {
        if (esExitoso && !esFinal)
            throw new ArgumentException(
                "Un estado 'exitoso' (es_exitoso=true) debe ser también 'final' (es_final=true).");
    }

    public async Task<(List<EstadoProceso> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<EstadoProceso> ObtenerAsync(string idEstado)
    {
        var estado = await _repositorio.ObtenerPorIdAsync(idEstado);

        if (estado is null)
            throw new NoEncontradoExcepcion($"No existe el estado de proceso '{idEstado}'.");

        return estado;
    }

    public async Task CrearAsync(EstadoProcesoCrear peticion)
    {
        bool esFinal = peticion.EsFinal ?? false;      // por defecto false, igual que la columna
        bool esExitoso = peticion.EsExitoso ?? false;

        ValidarCoherencia(esFinal, esExitoso);

        var estado = new EstadoProceso
        {
            IdEstado = peticion.IdEstado!,
            Nombre = peticion.Nombre!,
            EsFinal = esFinal,
            EsExitoso = esExitoso,
        };

        await _repositorio.CrearAsync(estado);
    }

    public async Task<int> ReemplazarAsync(string idEstado, EstadoProcesoReemplazo peticion)
    {
        bool esFinal = peticion.EsFinal!.Value;
        bool esExitoso = peticion.EsExitoso!.Value;

        ValidarCoherencia(esFinal, esExitoso);

        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["es_final"] = esFinal,
            ["es_exitoso"] = esExitoso,
        };

        int filas = await _repositorio.ActualizarAsync(idEstado, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el estado de proceso '{idEstado}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idEstado, EstadoProcesoActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // Solo se puede validar la coherencia cuando llegan LOS DOS campos
        // juntos en esta petición: si llega uno solo, no hay con qué
        // comparar sin leer antes la fila (decisión asumida en 3_plan.md
        // §4.8) — el CHECK de la BD queda como respaldo en ese caso.
        if (peticion.EsFinal is not null && peticion.EsExitoso is not null)
            ValidarCoherencia(peticion.EsFinal.Value, peticion.EsExitoso.Value);

        int filas = await _repositorio.ActualizarAsync(idEstado, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el estado de proceso '{idEstado}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idEstado)
    {
        int filas = await _repositorio.EliminarAsync(idEstado);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el estado de proceso '{idEstado}'.");

        return filas;
    }
}
