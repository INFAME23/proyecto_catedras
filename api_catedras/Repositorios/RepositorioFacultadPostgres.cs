// ==========================================================================
//  Repositorios/RepositorioFacultadPostgres.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Implementación de IRepositorioFacultad para PostgreSQL con DAPPER.
//  SQL escrito A MANO, visible y SIEMPRE parametrizado (@parametro). Una
//  conexión por operación con 'await using'. Mismo molde que
//  RepositorioSedePostgres (v1).
//
//  NOVEDAD v2 — LAS FK LAS APLICA EL MOTOR, NO ESTE CÓDIGO:
//   · Un 'fk_facultad_padre' que no existe -> el INSERT/UPDATE viola la FK
//     'fk_facultad_padre' y PostgreSQL lanza NpgsqlException (-> 500). Aquí
//     NO se hace un SELECT previo para comprobarlo (decisión D9).
//   · DELETE: las facultades hijas y los programas que apuntaban a la
//     borrada quedan con su FK en NULL (ON DELETE SET NULL). Lo hace el
//     motor dentro de la MISMA sentencia (ACID · A).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;      // QueryAsync, ExecuteAsync, DynamicParameters
using Npgsql;      // NpgsqlConnection

/// <summary>Acceso a 'catedras.facultad' en PostgreSQL mediante Dapper.</summary>
public class RepositorioFacultadPostgres : IRepositorioFacultad
{
    // 'readonly': se asigna una vez en el constructor y no cambia más.
    private readonly string _cadenaConexion;

    // [ACID · C] LISTA BLANCA de columnas que un UPDATE puede tocar. La PK
    // 'id_facultad' NO está: el código de una facultad no se cambia.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "fk_facultad_padre", "activo" };

    /// <summary>Recibe la cadena de conexión (la inyecta el ensamblador de Program.cs).</summary>
    public RepositorioFacultadPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Facultad>> ObtenerTodosAsync(int limite)
    {
        // 'LIMIT @limite' = Top-N de PostgreSQL, parametrizado.
        const string sql = @"
            SELECT id_facultad, nombre, fk_facultad_padre, activo
            FROM catedras.facultad
            ORDER BY id_facultad
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Dapper mapea cada fila a un Facultad (fk_facultad_padre -> FkFacultadPadre
        // gracias a MatchNamesWithUnderscores, configurado en Program.cs).
        var filas = await conexion.QueryAsync<Facultad>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<Facultad?> ObtenerPorIdAsync(string idFacultad)
    {
        const string sql = @"
            SELECT id_facultad, nombre, fk_facultad_padre, activo
            FROM catedras.facultad
            WHERE id_facultad = @idFacultad;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila, o null si no existe.
        return await conexion.QuerySingleOrDefaultAsync<Facultad>(sql, new { idFacultad });
    }

    public async Task CrearAsync(Facultad facultad)
    {
        const string sql = @"
            INSERT INTO catedras.facultad (id_facultad, nombre, fk_facultad_padre, activo)
            VALUES (@IdFacultad, @Nombre, @FkFacultadPadre, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Dapper toma cada @Parametro de la propiedad homónima de 'facultad'.
        // Duplicado o padre inexistente -> NpgsqlException sube tal cual.
        await conexion.ExecuteAsync(sql, facultad);
    }

    public async Task<int> ActualizarAsync(string idFacultad, Dictionary<string, object?> datos)
    {
        // Se arma 'SET col = @col, ...' SOLO con columnas de la lista blanca.
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idFacultad);

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
            $"UPDATE catedras.facultad SET {string.Join(", ", asignaciones)} " +
            "WHERE id_facultad = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Filas que cumplieron el WHERE: 0 = esa facultad no existe.
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idFacultad)
    {
        const string sql = "DELETE FROM catedras.facultad WHERE id_facultad = @idFacultad;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Las FK entrantes son SET NULL: el borrado no falla por tener hijas
        // o programas; el motor les pone el campo en NULL en la misma sentencia.
        return await conexion.ExecuteAsync(sql, new { idFacultad });
    }
}
