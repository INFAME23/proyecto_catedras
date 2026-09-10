// ==========================================================================
//  Servicios/ServicioModalidad.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Modalidad'. Mismo patrón que ServicioSede: recibe
//  IRepositorioModalidad por constructor, no conoce HTTP ni el motor.
//
//  A diferencia de Sede/EstadoProceso, aquí no hay reglas de coherencia
//  entre dos campos (esa validación de 'canal_difusion' ya quedó resuelta
//  en la PETICIÓN con [RegularExpression] — ver 3_plan.md §4.9).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Modalidad'.</summary>
public class ServicioModalidad : IServicioModalidad
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioModalidad _repositorio;

    public ServicioModalidad(IRepositorioModalidad repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Modalidad> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Modalidad> ObtenerAsync(string idModalidad)
    {
        var modalidad = await _repositorio.ObtenerPorIdAsync(idModalidad);

        if (modalidad is null)
            throw new NoEncontradoExcepcion($"No existe la modalidad '{idModalidad}'.");

        return modalidad;
    }

    public async Task CrearAsync(ModalidadCrear peticion)
    {
        var modalidad = new Modalidad
        {
            IdModalidad = peticion.IdModalidad!,
            Nombre = peticion.Nombre!,
            CanalDifusion = peticion.CanalDifusion!,
            Activo = peticion.Activo ?? true,   // por defecto true, igual que la columna
        };

        await _repositorio.CrearAsync(modalidad);
    }

    public async Task<int> ReemplazarAsync(string idModalidad, ModalidadReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["canal_difusion"] = peticion.CanalDifusion!,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idModalidad, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la modalidad '{idModalidad}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idModalidad, ModalidadActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idModalidad, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la modalidad '{idModalidad}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idModalidad)
    {
        int filas = await _repositorio.EliminarAsync(idModalidad);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la modalidad '{idModalidad}'.");

        return filas;
    }
}
