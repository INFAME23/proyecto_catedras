// ==========================================================================
//  Servicios/ServicioDependencia.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Dependencia'. Recibe IRepositorioDependencia por
//  constructor (lo inyecta Program.cs); no conoce HTTP ni el motor.
//
//  REGLA NUEVA DE LA v2 — AUTORREFERENCIA (3_plan.md §4.2, ADR D10 — misma regla que Facultad):
//  una dependencia no puede ser su propio padre. La BD tiene el CHECK
//  'chk_dependencia_padre', pero aquí se valida ANTES de tocar la BD para
//  responder un 400 claro (en vez de un 500 con el texto del motor):
//    · en POST se compara 'fk_dependencia_padre' con 'id_dependencia' del body;
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

/// <summary>Reglas de negocio de 'Dependencia'.</summary>
public class ServicioDependencia : IServicioDependencia
{
    // Límite por defecto de la lista cuando no llega '?limite='.
    private const int LimitePorDefecto = 1000;

    // [SOLID · D] Depende de la INTERFAZ, nunca de RepositorioDependenciaPostgres.
    private readonly IRepositorioDependencia _repositorio;

    public ServicioDependencia(IRepositorioDependencia repositorio)
    {
        _repositorio = repositorio;
    }

    // Regla de negocio compartida por POST, PUT y PATCH.
    // 'static' = no usa ningún campo de la instancia.
    private static void ValidarNoEsSuPropioPadre(string idDependencia, string? fkDependenciaPadre)
    {
        // string.Equals con Ordinal: comparación exacta, carácter a carácter
        // (igual que el '<>' de PostgreSQL sobre VARCHAR).
        if (fkDependenciaPadre is not null && string.Equals(fkDependenciaPadre, idDependencia, StringComparison.Ordinal))
            throw new ArgumentException(
                "Una dependencia no puede ser su propio padre ('fk_dependencia_padre').");
    }

    public async Task<(List<Dependencia> Datos, int Total)> ListarAsync(int? limite)
    {
        // '??' = si 'limite' es null, usa el valor de la derecha.
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Dependencia> ObtenerAsync(string idDependencia)
    {
        var dependencia = await _repositorio.ObtenerPorIdAsync(idDependencia);

        if (dependencia is null)
            throw new NoEncontradoExcepcion($"No existe la dependencia '{idDependencia}'.");

        return dependencia;
    }

    public async Task CrearAsync(DependenciaCrear peticion)
    {
        // La petición ya pasó la validación de forma: 'id_dependencia' y
        // 'nombre' NO son null (el '!' se lo asegura al compilador).
        ValidarNoEsSuPropioPadre(peticion.IdDependencia!, peticion.FkDependenciaPadre);

        var dependencia = new Dependencia
        {
            IdDependencia = peticion.IdDependencia!,
            Nombre = peticion.Nombre!,
            FkDependenciaPadre = peticion.FkDependenciaPadre, // opcional: null = raíz
            Activo = peticion.Activo ?? true,           // DEFAULT de la columna
        };

        await _repositorio.CrearAsync(dependencia);
    }

    public async Task<int> ReemplazarAsync(string idDependencia, DependenciaReemplazo peticion)
    {
        ValidarNoEsSuPropioPadre(idDependencia, peticion.FkDependenciaPadre);

        // Reemplazo COMPLETO: se escriben las 3 columnas, padre incluido
        // (si no llegó, queda NULL).
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["fk_dependencia_padre"] = peticion.FkDependenciaPadre,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idDependencia, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la dependencia '{idDependencia}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idDependencia, DependenciaActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        // Body sin campos -> regla de negocio -> 400.
        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        ValidarNoEsSuPropioPadre(idDependencia, peticion.FkDependenciaPadre);

        int filas = await _repositorio.ActualizarAsync(idDependencia, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la dependencia '{idDependencia}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idDependencia)
    {
        int filas = await _repositorio.EliminarAsync(idDependencia);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la dependencia '{idDependencia}'.");

        return filas;
    }
}
