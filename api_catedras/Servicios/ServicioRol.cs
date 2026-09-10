// ==========================================================================
//  Servicios/ServicioRol.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Rol'. Mismo patrón que ServicioSede: recibe
//  IRepositorioRol por constructor. Sin reglas de coherencia entre campos.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Rol'.</summary>
public class ServicioRol : IServicioRol
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioRol _repositorio;

    public ServicioRol(IRepositorioRol repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Rol> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Rol> ObtenerAsync(string idRol)
    {
        var rol = await _repositorio.ObtenerPorIdAsync(idRol);

        if (rol is null)
            throw new NoEncontradoExcepcion($"No existe el rol '{idRol}'.");

        return rol;
    }

    public async Task CrearAsync(RolCrear peticion)
    {
        var rol = new Rol
        {
            IdRol = peticion.IdRol!,
            Nombre = peticion.Nombre!,
            Descripcion = peticion.Descripcion,
        };

        await _repositorio.CrearAsync(rol);
    }

    public async Task<int> ReemplazarAsync(string idRol, RolReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["descripcion"] = peticion.Descripcion,
        };

        int filas = await _repositorio.ActualizarAsync(idRol, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el rol '{idRol}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idRol, RolActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idRol, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el rol '{idRol}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idRol)
    {
        int filas = await _repositorio.EliminarAsync(idRol);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el rol '{idRol}'.");

        return filas;
    }
}
