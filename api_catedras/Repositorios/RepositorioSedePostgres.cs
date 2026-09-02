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
//
// --------------------------------------------------------------------------
//  PRINCIPIOS EN ESTE ARCHIVO:
//   · SOLID · S (Responsabilidad única): SOLO acceso a datos. No valida
//     reglas de negocio ni conoce HTTP. Traduce método -> SQL -> objeto.
//   · SOLID · L (Sustitución de Liskov): cumple IRepositorioSede al pie de
//     la letra (null si no hay fila; nº de filas afectadas), así que el
//     servicio no nota la diferencia con el repo falso de 'pruebas/'.
//   · SOLID · O (Abierto/Cerrado): esta clase ES la unidad que se
//     reemplaza/añade para otro motor; el resto del sistema no cambia.
//
//  ACID — DÓNDE VIVE CADA LETRA EN LA v1:
//   · A (Atomicidad): cada 'ExecuteAsync' manda UNA sentencia. PostgreSQL en
//     autocommit la envuelve en su propia transacción implícita: se aplica
//     completa o no se aplica. El UPDATE de varias columnas es un solo
//     comando -> todas las columnas cambian juntas.
//   · C (Consistencia): la ponen las RESTRICCIONES de la tabla (pk_sede,
//     uq_sede_nombre, NOT NULL, FK fk_sesion_sede ON DELETE RESTRICT) MÁS
//     la lista blanca 'ColumnasActualizables' de aquí abajo. Si algo las
//     viola, el motor lanza NpgsqlException y NO cambia nada.
//   · I (Aislamiento): 'await using' abre una conexión propia por operación;
//     PostgreSQL usa READ COMMITTED por defecto, así una petición no ve los
//     cambios a medio confirmar de otra.
//   · D (Durabilidad): cuando 'ExecuteAsync' regresa sin excepción, el COMMIT
//     implícito ya está en el WAL en disco: sobrevive a un reinicio.
//   · RNF3 (parámetros @) también protege la Consistencia: el valor del
//     cliente nunca se concatena en el texto SQL.
//
//  FLUJO DE UNA PETICIÓN — PASOS 6 y 7:
//   [FLUJO 6] Abre conexión, ejecuta el SQL parametrizado con Dapper y mapea
//             fila -> Sede (o cuenta filas afectadas).
//   [FLUJO 7] Devuelve el resultado hacia el servicio; si el motor falla,
//             sube NpgsqlException sin capturarla.
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

    // [ACID · C] Columnas que el UPDATE puede tocar. LISTA BLANCA: aunque el
    // diccionario venga del servicio, nunca se arma SQL con claves ajenas
    // -> ninguna escritura puede salirse de estas 4 columnas conocidas.
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

        // [FLUJO 6][ACID · I] Conexión propia de esta operación; se cierra
        // sola al salir del bloque ('await using').
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // QueryAsync<Sede> ejecuta el SELECT y devuelve un Sede por fila.
        var filas = await conexion.QueryAsync<Sede>(sql, new { limite });
        return filas.ToList(); // [FLUJO 7] vuelve al servicio
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
        // el mismo nombre. [ACID · C] Si el id_sede o el nombre ya existen,
        // PostgreSQL lanza NpgsqlException (viola pk_sede o uq_sede_nombre)
        // y [ACID · A] no inserta nada; la excepción sube tal cual hasta el
        // controlador, que responde 500. [ACID · D] Si NO hay excepción, el
        // INSERT quedó confirmado y en disco.
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
            // [ACID · C] Filtro por lista blanca: una clave no permitida se
            // ignora, nunca llega al texto SQL.
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor); // [RNF3] valor como @parametro
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

        // [ACID · A] Un solo UPDATE: todas las asignaciones del SET se
        // aplican juntas o ninguna. ExecuteAsync devuelve el número de filas
        // afectadas; en PostgreSQL cuenta las que CUMPLIERON el WHERE (aunque
        // el valor nuevo sea igual al viejo): 0 = "esa sede no existe".
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
