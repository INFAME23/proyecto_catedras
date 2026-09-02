// ==========================================================================
//  Repositorios/RepositorioSedePostgres.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Implementación de IRepositorioSede para PostgreSQL con DAPPER.
//
//  Reglas de la constitución que se ven aquí:
//   · Art. 2  -> el SQL se escribe A MANO y queda VISIBLE; Dapper solo lo
//               ejecuta y mapea fila -> objeto (NO genera SQL).
//   · RNF3    -> el SQL SIEMPRE va parametrizado con @parametro; JAMÁS se
//               concatenan valores del cliente.
//   · RNF4    -> todo el acceso a datos es async/await.
//
//  Una conexión por operación, con 'await using' (se abre, se usa, se
//  cierra sola). El código de la v1 SOLO nombra la tabla 'catedras.sede'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;      // QueryAsync, ExecuteAsync, DynamicParameters
using Npgsql;      // NpgsqlConnection

/// <summary>Acceso a 'catedras.sede' en PostgreSQL mediante Dapper.</summary>
public class RepositorioSedePostgres : IRepositorioSede
{
    // 'readonly': se asigna una vez en el constructor y no cambia más.
    private readonly string _cadenaConexion;

    // Columnas que el UPDATE puede tocar. LISTA BLANCA: aunque el
    // diccionario venga del servicio, nunca se arma SQL con claves ajenas.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "direccion", "es_virtual", "activo" };

    /// <summary>Recibe la cadena de conexión (la inyecta el ensamblador).</summary>
    public RepositorioSedePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    // ----------------------------------------------------------------------
    //  LISTAR
    // ----------------------------------------------------------------------
    public async Task<List<Sede>> ObtenerTodosAsync(int limite)
    {
        // SQL a mano. 'LIMIT @limite' es el Top-N de PostgreSQL: va al final
        // y acepta parámetro. El mapeo columna->propiedad lo hace Dapper por
        // nombre (es_virtual -> EsVirtual gracias a MatchNamesWithUnderscores,
        // configurado en Program.cs).
        const string sql = @"
            SELECT id_sede, nombre, direccion, es_virtual, activo
            FROM catedras.sede
            ORDER BY id_sede
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // QueryAsync<Sede> ejecuta el SELECT y devuelve un Sede por fila.
        var filas = await conexion.QueryAsync<Sede>(sql, new { limite });
        return filas.ToList();
    }

    // ----------------------------------------------------------------------
    //  OBTENER POR CÓDIGO
    // ----------------------------------------------------------------------
    public async Task<Sede?> ObtenerPorIdAsync(string idSede)
    {
        const string sql = @"
            SELECT id_sede, nombre, direccion, es_virtual, activo
            FROM catedras.sede
            WHERE id_sede = @idSede;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // QuerySingleOrDefaultAsync: exactamente una fila, o null si no hay.
        return await conexion.QuerySingleOrDefaultAsync<Sede>(sql, new { idSede });
    }

    // ----------------------------------------------------------------------
    //  CREAR
    // ----------------------------------------------------------------------
    public async Task CrearAsync(Sede sede)
    {
        const string sql = @"
            INSERT INTO catedras.sede (id_sede, nombre, direccion, es_virtual, activo)
            VALUES (@IdSede, @Nombre, @Direccion, @EsVirtual, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Dapper toma cada @parametro de la propiedad del objeto 'sede' con
        // el mismo nombre. Si el id_sede o el nombre ya existen, PostgreSQL
        // lanza NpgsqlException (viola pk_sede o uq_sede_nombre) y sube tal
        // cual hasta el controlador, que responde 500.
        await conexion.ExecuteAsync(sql, sede);
    }

    // ----------------------------------------------------------------------
    //  ACTUALIZAR (sirve para PUT y para PATCH)
    // ----------------------------------------------------------------------
    public async Task<int> ActualizarAsync(string idSede, Dictionary<string, object?> datos)
    {
        // Se arma el 'SET col = @col, ...' SOLO con columnas de la lista
        // blanca. El nombre de la columna nunca viene del cliente.
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idSede);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue; // se ignora cualquier clave no permitida

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        // Sin columnas válidas no hay nada que ejecutar.
        if (asignaciones.Count == 0)
            return 0;

        // string.Join une las asignaciones con comas. El WHERE va
        // parametrizado con @id_clave.
        var sql =
            $"UPDATE catedras.sede SET {string.Join(", ", asignaciones)} " +
            "WHERE id_sede = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // ExecuteAsync devuelve el número de filas afectadas. En PostgreSQL
        // cuenta las filas que CUMPLIERON el WHERE (aunque el valor nuevo
        // sea igual al viejo): 0 significa "esa sede no existe".
        return await conexion.ExecuteAsync(sql, parametros);
    }

    // ----------------------------------------------------------------------
    //  ELIMINAR
    // ----------------------------------------------------------------------
    public async Task<int> EliminarAsync(string idSede)
    {
        const string sql = "DELETE FROM catedras.sede WHERE id_sede = @idSede;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Filas eliminadas (0 = no existía). Si la sede tiene sesiones
        // asociadas, la FK fk_sesion_sede (ON DELETE RESTRICT) hace que
        // PostgreSQL lance NpgsqlException -> el controlador responde 500.
        return await conexion.ExecuteAsync(sql, new { idSede });
    }
}
