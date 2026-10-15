// ==========================================================================
//  Repositorios/IRepositorioSede.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Una INTERFAZ es un CONTRATO: dice QUÉ operaciones existen, no CÓMO se
//  hacen. El servicio depende de esta interfaz y NO sabe qué motor hay
//  detrás (Artículo 3).
//
//  Esto es lo que "compra" la arquitectura para v3: un segundo motor será
//  otra clase con ': IRepositorioSede' y solo el ensamblador (Program.cs)
//  cambia. También habilita el criterio 6: probar el servicio con un
//  repositorio FALSO en memoria que implementa esta misma interfaz.
//
//  Todos los métodos son 'async' (Artículo 2, RNF4): devuelven Task.
//
// --------------------------------------------------------------------------
//  PRINCIPIOS EN ESTE ARCHIVO:
//   · SOLID · D (Inversión de dependencias): el servicio (alto nivel)
//     depende de ESTA abstracción, no del detalle Postgres/Dapper.
//   · SOLID · I (Segregación de interfaces): 5 operaciones de datos, todas
//     necesarias. Nada de un "IRepositorioGenerico" con 30 métodos.
//   · SOLID · L (Sustitución de Liskov): el contrato fija el comportamiento
//     observable — "devuelve null si no existe", "devuelve filas afectadas,
//     0 = no existe" — para que Postgres y el repo falso sean sustituibles.
//   · SOLID · O (Abierto/Cerrado): v3 = nueva clase ': IRepositorioSede';
//     esta interfaz y todo lo de arriba quedan intactos.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.sede'.</summary>
public interface IRepositorioSede
{
    /// <summary>Devuelve las sedes ordenadas por código, hasta 'limite' filas.</summary>
    Task<List<Sede>> ObtenerTodosAsync(int limite, bool incluirInactivos = false);

    /// <summary>Devuelve la sede con ese código, o null si no existe.</summary>
    Task<Sede?> ObtenerPorIdAsync(string idSede);

    /// <summary>Inserta una sede nueva. Si el código o el nombre ya existen,
    /// el motor lanza NpgsqlException (la API lo reporta como 500).</summary>
    Task CrearAsync(Sede sede);

    /// <summary>
    /// Actualiza las columnas indicadas en 'datos' (columna -> valor nuevo)
    /// para la sede con ese código. Sirve para PUT (llegan las 4 columnas)
    /// y para PATCH (llegan solo las enviadas). Devuelve el número de filas
    /// afectadas (0 = no existe esa sede).
    /// </summary>
    Task<int> ActualizarAsync(string idSede, Dictionary<string, object?> datos);

    /// <summary>Borra la sede con ese código. Devuelve filas eliminadas
    /// (0 = no existe).</summary>
    Task<int> EliminarAsync(string idSede);
}
