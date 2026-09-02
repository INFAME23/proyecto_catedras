// ==========================================================================
//  Servicios/ServicioSede.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Implementación de las reglas de negocio del recurso 'Sede'.
//
//  Recibe IRepositorioSede POR CONSTRUCTOR (lo inyecta el ensamblador de
//  Program.cs). No conoce HTTP ni el motor concreto: solo la interfaz.
//  Comunica los problemas con excepciones y el controlador las traduce:
//     ArgumentException     -> 400
//     NoEncontradoExcepcion -> 404
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Sede'.</summary>
public class ServicioSede : IServicioSede
{
    // Límite por defecto de la lista cuando el cliente no manda '?limite='.
    private const int LimitePorDefecto = 1000;

    // El servicio depende de la INTERFAZ, nunca de RepositorioSedePostgres.
    private readonly IRepositorioSede _repositorio;

    public ServicioSede(IRepositorioSede repositorio)
    {
        _repositorio = repositorio;
    }

    // ----------------------------------------------------------------------
    //  LISTAR
    // ----------------------------------------------------------------------
    public async Task<(List<Sede> Datos, int Total)> ListarAsync(int? limite)
    {
        // Regla: si no viene, 1000. Si viene, debe ser > 0.
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);

        // 'total' = cuántas filas se devuelven en esta respuesta.
        return (datos, datos.Count);
    }

    // ----------------------------------------------------------------------
    //  OBTENER POR CÓDIGO
    // ----------------------------------------------------------------------
    public async Task<Sede> ObtenerAsync(string idSede)
    {
        var sede = await _repositorio.ObtenerPorIdAsync(idSede);

        // 'null' del repositorio = no existe -> el controlador hará 404.
        if (sede is null)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return sede;
    }

    // ----------------------------------------------------------------------
    //  CREAR (POST)
    // ----------------------------------------------------------------------
    public async Task CrearAsync(SedeCrear peticion)
    {
        // La petición ya pasó la validación de forma/tipo del framework:
        // aquí id_sede, nombre, es_virtual y activo NO son null. El '!' le
        // dice al compilador "confío en que tiene valor"; '.Value' saca el
        // bool de dentro del bool?.
        var sede = new Sede
        {
            IdSede = peticion.IdSede!,
            Nombre = peticion.Nombre!,
            Direccion = peticion.Direccion,          // opcional: puede ir null
            EsVirtual = peticion.EsVirtual!.Value,
            Activo = peticion.Activo!.Value,
        };

        await _repositorio.CrearAsync(sede);
    }

    // ----------------------------------------------------------------------
    //  REEMPLAZAR (PUT) — reemplazo COMPLETO: se escriben las 4 columnas
    // ----------------------------------------------------------------------
    public async Task<int> ReemplazarAsync(string idSede, SedeReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"]     = peticion.Nombre!,
            ["direccion"]  = peticion.Direccion,     // si no vino -> NULL en la columna
            ["es_virtual"] = peticion.EsVirtual!.Value,
            ["activo"]     = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idSede, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return filas;
    }

    // ----------------------------------------------------------------------
    //  ACTUALIZAR (PATCH) — parcial: solo las columnas enviadas
    // ----------------------------------------------------------------------
    public async Task<int> ActualizarAsync(string idSede, SedeActualizar peticion)
    {
        // La petición arma el diccionario solo con los campos que llegaron.
        var datos = peticion.ACamposEnviados();

        // Regla de negocio (no de forma): body sin campos -> 400.
        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idSede, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return filas;
    }

    // ----------------------------------------------------------------------
    //  ELIMINAR (DELETE)
    // ----------------------------------------------------------------------
    public async Task<int> EliminarAsync(string idSede)
    {
        int filas = await _repositorio.EliminarAsync(idSede);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return filas;
    }
}
