// ==========================================================================
//  pruebas/Programa.cs  ·  Proyecto Cátedras · Versión 1 · Criterio 6
// --------------------------------------------------------------------------
//  "Prueba de capas": se construye ServicioSede pasándole un repositorio
//  FALSO que guarda las sedes en un diccionario en memoria (implementa
//  IRepositorioSede, sin PostgreSQL). Si el servicio funciona así, es
//  porque depende de la INTERFAZ y no del motor: las capas están bien
//  cortadas.
//
//  Ejecutar (desde la carpeta api_catedras):
//      dotnet run --project pruebas
//  Sale con código 0 si todo pasa; 1 si algo falla.
// ==========================================================================

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;
using ApiCatedras.Servicios;

// --------------------------------------------------------------------------
//  Instrumentación mínima de pruebas (sin librerías: Artículo 2).
// --------------------------------------------------------------------------
int pasadas = 0;
int fallidas = 0;

void Afirmar(string caso, bool condicion)
{
    Console.WriteLine((condicion ? "  OK    " : "  FALLA ") + caso);
    if (condicion) pasadas++;
    else fallidas++;
}

async Task AfirmarLanza<TExcepcion>(string caso, Func<Task> accion)
    where TExcepcion : Exception
{
    try
    {
        await accion();
        Afirmar(caso, false); // no lanzó: falla
    }
    catch (TExcepcion)
    {
        Afirmar(caso, true);  // lanzó lo esperado: pasa
    }
}

// --------------------------------------------------------------------------
//  El sujeto de prueba: el servicio real con el repositorio FALSO.
// --------------------------------------------------------------------------
var servicio = new ServicioSede(new RepositorioSedeFalso());

Console.WriteLine("== Prueba de capas: ServicioSede + repositorio falso en memoria ==\n");

// 1 · Listar devuelve las sedes semilla del repo falso.
var (datos, total) = await servicio.ListarAsync(null);
Afirmar("Listar() devuelve las 2 sedes semilla", total == 2 && datos.Count == 2);

// 2 · limite <= 0 -> ArgumentException (el controlador haría 400).
await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicio.ListarAsync(0));

// 3 · Obtener una sede inexistente -> NoEncontradoExcepcion (404).
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicio.ObtenerAsync("NO_VA"));

// 4 · Crear + Obtener.
await servicio.CrearAsync(new SedeCrear
{
    IdSede = "BELLO",
    Nombre = "Campus Bello",
    Direccion = "Calle 45 61-40, Bello",
    EsVirtual = false,
    Activo = true,
});
var creada = await servicio.ObtenerAsync("BELLO");
Afirmar("Crear() inserta y Obtener() la encuentra", creada.Nombre == "Campus Bello");

// 5 · PUT reemplaza los 4 campos (incluida 'direccion' a null).
int filasPut = await servicio.ReemplazarAsync("BELLO", new SedeReemplazo
{
    Nombre = "Bello (renombrada)",
    Direccion = null,
    EsVirtual = true,
    Activo = false,
});
var trasPut = await servicio.ObtenerAsync("BELLO");
Afirmar("PUT reemplaza todo",
    filasPut == 1 && trasPut.Nombre == "Bello (renombrada)"
    && trasPut.EsVirtual && !trasPut.Activo && trasPut.Direccion is null);

// 6 · PATCH toca SOLO el campo enviado.
int filasPatch = await servicio.ActualizarAsync("BELLO", new SedeActualizar { Activo = true });
var trasPatch = await servicio.ObtenerAsync("BELLO");
Afirmar("PATCH cambia solo 'activo' y deja 'nombre' igual",
    filasPatch == 1 && trasPatch.Activo && trasPatch.Nombre == "Bello (renombrada)");

// 7 · PATCH con body vacío -> ArgumentException (el controlador haría 400).
await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicio.ActualizarAsync("BELLO", new SedeActualizar()));

// 8 · PATCH sobre una sede inexistente -> NoEncontradoExcepcion (404).
await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicio.ActualizarAsync("NO_VA", new SedeActualizar { Activo = false }));

// 9 · DELETE elimina; el segundo DELETE -> NoEncontradoExcepcion (404).
int eliminadas = await servicio.EliminarAsync("BELLO");
Afirmar("DELETE elimina 1 fila", eliminadas == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicio.EliminarAsync("BELLO"));

// --------------------------------------------------------------------------
//  Resultado final -> código de salida del proceso.
// --------------------------------------------------------------------------
Console.WriteLine($"\n{pasadas} pasadas, {fallidas} fallidas.");
return fallidas == 0 ? 0 : 1;


// ==========================================================================
//  Repositorio FALSO: implementa el MISMO contrato IRepositorioSede, pero
//  con un diccionario en memoria. Ninguna línea de PostgreSQL.
//  (En C#, las declaraciones de tipos van después de las instrucciones
//   top-level.)
// ==========================================================================
class RepositorioSedeFalso : IRepositorioSede
{
    private readonly Dictionary<string, Sede> _tabla = new();

    public RepositorioSedeFalso()
    {
        // Dos sedes semilla, como en el script real.
        _tabla["SAN_BENITO"] = new Sede
        {
            IdSede = "SAN_BENITO",
            Nombre = "Campus San Benito",
            Direccion = "Carrera 56C 51-110, Medellin",
            EsVirtual = false,
            Activo = true,
        };
        _tabla["VIRTUAL"] = new Sede
        {
            IdSede = "VIRTUAL",
            Nombre = "Virtual",
            Direccion = null,
            EsVirtual = true,
            Activo = true,
        };
    }

    public Task<List<Sede>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(s => s.IdSede).Take(limite).ToList());

    public Task<Sede?> ObtenerPorIdAsync(string idSede) =>
        Task.FromResult(_tabla.TryGetValue(idSede, out var sede) ? sede : null);

    public Task CrearAsync(Sede sede)
    {
        // Simula la violación de la clave primaria (pk_sede).
        if (_tabla.ContainsKey(sede.IdSede))
            throw new InvalidOperationException($"id_sede '{sede.IdSede}' duplicado (simulado).");

        _tabla[sede.IdSede] = sede;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idSede, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idSede, out var sede))
            return Task.FromResult(0); // no existe

        // Aplica solo las columnas presentes en el diccionario.
        if (datos.TryGetValue("nombre", out var n)) sede.Nombre = (string)n!;
        if (datos.TryGetValue("direccion", out var d)) sede.Direccion = (string?)d;
        if (datos.TryGetValue("es_virtual", out var v)) sede.EsVirtual = (bool)v!;
        if (datos.TryGetValue("activo", out var a)) sede.Activo = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idSede) =>
        Task.FromResult(_tabla.Remove(idSede) ? 1 : 0);
}
