// ==========================================================================
//  Repositorios/RepositorioDependenciaPostgres.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Implementación de IRepositorioDependencia para PostgreSQL con DAPPER.
//  SQL escrito A MANO, visible y SIEMPRE parametrizado (@parametro). Una
//  conexión por operación con 'await using'. Mismo molde que
//  RepositorioSedePostgres (v1).
//
//  NOVEDAD v2 — LAS FK LAS APLICA EL MOTOR, NO ESTE CÓDIGO:
//   · Un 'fk_dependencia_padre' que no existe -> el INSERT/UPDATE viola la FK
//     'fk_dependencia_padre' y PostgreSQL lanza NpgsqlException (-> 500). Aquí
//     NO se hace un SELECT previo para comprobarlo (decisión D9).
//   · DELETE: las dependencias hijas y las cátedras que apuntaban a la
//     borrada quedan con su FK en NULL (ON DELETE SET NULL). Lo hace el
//     motor dentro de la MISMA sentencia (ACID · A).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;      // QueryAsync, ExecuteAsync, DynamicParameters
using Npgsql;      // NpgsqlConnection

/// <summary>Acceso a 'catedras.dependencia' en PostgreSQL mediante Dapper.</summary>
public class RepositorioDependenciaPostgres : IRepositorioDependencia
{
    // 'readonly': se asigna una vez en el constructor y no cambia más.
    private readonly string _cadenaConexion;

    // [ACID · C] LISTA BLANCA de columnas que un UPDATE puede tocar. La PK
    // 'id_dependencia' NO está: el código de una dependencia no se cambia.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "fk_dependencia_padre", "activo" };

    /// <summary>Recibe la cadena de conexión (la inyecta el ensamblador de Program.cs).</summary>
    public RepositorioDependenciaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Dependencia>> ObtenerTodosAsync(int limite)
    {
        // 'LIMIT @limite' = Top-N de PostgreSQL, parametrizado.
        const string sql = @"
            SELECT id_dependencia, nombre, fk_dependencia_padre, activo
            FROM catedras.dependencia
            ORDER BY id_dependencia
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Dapper mapea cada fila a un Dependencia (fk_dependencia_padre -> FkDependenciaPadre
        // gracias a MatchNamesWithUnderscores, configurado en Program.cs).
        var filas = await conexion.QueryAsync<Dependencia>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<Dependencia?> ObtenerPorIdAsync(string idDependencia)
    {
        const string sql = @"
            SELECT id_dependencia, nombre, fk_dependencia_padre, activo
            FROM catedras.dependencia
            WHERE id_dependencia = @idDependencia;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila, o null si no existe.
        return await conexion.QuerySingleOrDefaultAsync<Dependencia>(sql, new { idDependencia });
    }

    public async Task CrearAsync(Dependencia dependencia)
    {
        const string sql = @"
            INSERT INTO catedras.dependencia (id_dependencia, nombre, fk_dependencia_padre, activo)
            VALUES (@IdDependencia, @Nombre, @FkDependenciaPadre, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Dapper toma cada @Parametro de la propiedad homónima de 'dependencia'.
        // Duplicado o padre inexistente -> NpgsqlException sube tal cual.
        await conexion.ExecuteAsync(sql, dependencia);
    }

    public async Task<int> ActualizarAsync(string idDependencia, Dictionary<string, object?> datos)
    {
        // Se arma 'SET col = @col, ...' SOLO con columnas de la lista blanca.
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idDependencia);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue; // clave no permitida: nunca llega al texto SQL

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor); // el VALOR siempre como parámetro
        }

        if (asignaciones.Count == 0)
            return 0;

        var sql =
            $"UPDATE catedras.dependencia SET {string.Join(", ", asignaciones)} " +
            "WHERE id_dependencia = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Filas que cumplieron el WHERE: 0 = esa dependencia no existe.
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idDependencia)
    {
        const string sql = "DELETE FROM catedras.dependencia WHERE id_dependencia = @idDependencia;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Las FK entrantes son SET NULL: el borrado no falla por tener hijas
        // o cátedras; el motor les pone el campo en NULL en la misma sentencia.
        return await conexion.ExecuteAsync(sql, new { idDependencia });
    }
}
