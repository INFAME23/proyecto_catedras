// ==========================================================================
//  Repositorios/IRepositorioCatedra.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.catedra'. Dos diferencias con
//  el molde de la v1:
//   · 'ObtenerTodosAsync' acepta 'int?': null = SIN límite (las 5502 filas,
//     que es lo que consume el catálogo público, 6_contracts.md §9), y
//     'ContarAsync' da el total REAL de la tabla (criterio 6:
//     '?limite=10' responde 10 filas con total 5502).
//   · 'CrearAsync' devuelve una TUPLA con los dos valores que la BD pone en
//     el mismo INSERT: el id (IDENTITY) y 'nombre_asis' (columna generada).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.catedra'.</summary>
public interface IRepositorioCatedra
{
    /// <summary>Cátedras ordenadas por id; 'limite' null = todas.</summary>
    Task<List<Catedra>> ObtenerTodosAsync(int? limite);

    /// <summary>Cuántas filas tiene la tabla en total.</summary>
    Task<int> ContarAsync();

    /// <summary>La cátedra con ese id, o null si no existe.</summary>
    Task<Catedra?> ObtenerPorIdAsync(long idCatedra);

    /// <summary>Inserta y devuelve (id_catedra, nombre_asis) calculados por la BD.
    /// Código ASIS duplicado o FK inexistente -> NpgsqlException (500).</summary>
    Task<(long IdCatedra, string NombreAsis)> CrearAsync(Catedra catedra);

    /// <summary>Actualiza las columnas de 'datos'. Devuelve filas afectadas (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idCatedra, Dictionary<string, object?> datos);

    /// <summary>Borra. ⚠ 'sesion.fk_catedra' es ON DELETE CASCADE: borra también
    /// sus sesiones (tabla de la v4 ya sembrada). Devuelve filas eliminadas.</summary>
    Task<int> EliminarAsync(long idCatedra);
}
