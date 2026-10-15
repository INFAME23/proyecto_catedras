// ==========================================================================
//  Servicios/ServicioFacultad.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Facultad'. Recibe IRepositorioFacultad por
//  constructor (lo inyecta Program.cs); no conoce HTTP ni el motor.
//
//  REGLA NUEVA DE LA v2 — AUTORREFERENCIA (3_plan.md §4.2, ADR D10):
//  una facultad no puede ser su propio padre. La BD tiene el CHECK
//  'chk_facultad_padre', pero aquí se valida ANTES de tocar la BD para
//  responder un 400 claro (en vez de un 500 con el texto del motor):
//    · en POST se compara 'fk_facultad_padre' con 'id_facultad' del body;
//    · en PUT/PATCH el id viene de la URL, así que se compara con ese.
//  Solo se valida UN nivel (A -> A). Un ciclo largo (A -> B -> A) no se
//  detecta: es un riesgo documentado en D10, no un olvido.
//
//  Lo que NO hace: comprobar que el padre exista. Eso es una FK y la vigila
//  el motor (-> NpgsqlException -> 500, ADR D9).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Facultad'.</summary>
public class ServicioFacultad : IServicioFacultad
{
    // Límite por defecto de la lista cuando no llega '?limite='.
    private const int LimitePorDefecto = 1000;

    // [SOLID · D] Depende de la INTERFAZ, nunca de RepositorioFacultadPostgres.
    private readonly IRepositorioFacultad _repositorio;

    public ServicioFacultad(IRepositorioFacultad repositorio)
    {
        _repositorio = repositorio;
    }

    // Regla de negocio compartida por POST, PUT y PATCH.
    // 'static' = no usa ningún campo de la instancia.
    private static void ValidarNoEsSuPropioPadre(string idFacultad, string? fkFacultadPadre)
    {
        // string.Equals con Ordinal: comparación exacta, carácter a carácter
        // (igual que el '<>' de PostgreSQL sobre VARCHAR).
        if (fkFacultadPadre is not null && string.Equals(fkFacultadPadre, idFacultad, StringComparison.Ordinal))
            throw new ArgumentException(
                "Una facultad no puede ser su propio padre ('fk_facultad_padre').");
    }

    public async Task<(List<Facultad> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false)
    {
        // '??' = si 'limite' es null, usa el valor de la derecha.
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo, incluirInactivos);
        return (datos, datos.Count);
    }

    public async Task<Facultad> ObtenerAsync(string idFacultad)
    {
        var facultad = await _repositorio.ObtenerPorIdAsync(idFacultad);

        if (facultad is null)
            throw new NoEncontradoExcepcion($"No existe la facultad '{idFacultad}'.");

        return facultad;
    }

    public async Task CrearAsync(FacultadCrear peticion)
    {
        // La petición ya pasó la validación de forma: 'id_facultad' y
        // 'nombre' NO son null (el '!' se lo asegura al compilador).
        ValidarNoEsSuPropioPadre(peticion.IdFacultad!, peticion.FkFacultadPadre);

        var facultad = new Facultad
        {
            IdFacultad = peticion.IdFacultad!,
            Nombre = peticion.Nombre!,
            FkFacultadPadre = peticion.FkFacultadPadre, // opcional: null = raíz
            Activo = peticion.Activo ?? true,           // DEFAULT de la columna
        };

        await _repositorio.CrearAsync(facultad);
    }

    public async Task<int> ReemplazarAsync(string idFacultad, FacultadReemplazo peticion)
    {
        ValidarNoEsSuPropioPadre(idFacultad, peticion.FkFacultadPadre);

        // Reemplazo COMPLETO: se escriben las 3 columnas, padre incluido
        // (si no llegó, queda NULL).
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["fk_facultad_padre"] = peticion.FkFacultadPadre,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idFacultad, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la facultad '{idFacultad}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idFacultad, FacultadActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        // Body sin campos -> regla de negocio -> 400.
        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        ValidarNoEsSuPropioPadre(idFacultad, peticion.FkFacultadPadre);

        int filas = await _repositorio.ActualizarAsync(idFacultad, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la facultad '{idFacultad}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idFacultad)
    {
        int filas = await _repositorio.EliminarAsync(idFacultad);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la facultad '{idFacultad}'.");

        return filas;
    }
}
