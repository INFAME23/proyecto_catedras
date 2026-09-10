// ==========================================================================
//  Repositorios/RepositorioRolPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioRol para PostgreSQL con Dapper. SQL a
//  mano, SIEMPRE parametrizado; una conexión por operación. Mismo patrón
//  que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.rol' en PostgreSQL mediante Dapper.</summary>
public class RepositorioRolPostgres : IRepositorioRol
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "descripcion" };

    public RepositorioRolPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Rol>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_rol, nombre, descripcion
            FROM catedras.rol
            ORDER BY id_rol
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Rol>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<Rol?> ObtenerPorIdAsync(string idRol)
    {
        const string sql = @"
            SELECT id_rol, nombre, descripcion
            FROM catedras.rol
            WHERE id_rol = @idRol;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Rol>(sql, new { idRol });
    }

    public async Task CrearAsync(Rol rol)
    {
        const string sql = @"
            INSERT INTO catedras.rol (id_rol, nombre, descripcion)
            VALUES (@IdRol, @Nombre, @Descripcion);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, rol);
    }

    public async Task<int> ActualizarAsync(string idRol, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idRol);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        var sql =
            $"UPDATE catedras.rol SET {string.Join(", ", asignaciones)} " +
            "WHERE id_rol = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idRol)
    {
        const string sql = "DELETE FROM catedras.rol WHERE id_rol = @idRol;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el rol tiene usuarios asociados, fk_rolusr_rol
        // (ON DELETE RESTRICT) hace que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idRol });
    }
}
