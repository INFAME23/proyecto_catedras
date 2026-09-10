// ==========================================================================
//  Repositorios/RepositorioModalidadPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioModalidad para PostgreSQL con Dapper.
//  SQL a mano, SIEMPRE parametrizado (@parametro); una conexión por
//  operación con 'await using'. Mismo patrón que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.modalidad' en PostgreSQL mediante Dapper.</summary>
public class RepositorioModalidadPostgres : IRepositorioModalidad
{
    private readonly string _cadenaConexion;

    // [ACID · C] Lista blanca de columnas que el UPDATE puede tocar.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "canal_difusion", "activo" };

    public RepositorioModalidadPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Modalidad>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_modalidad, nombre, canal_difusion, activo
            FROM catedras.modalidad
            ORDER BY id_modalidad
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Modalidad>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<Modalidad?> ObtenerPorIdAsync(string idModalidad)
    {
        const string sql = @"
            SELECT id_modalidad, nombre, canal_difusion, activo
            FROM catedras.modalidad
            WHERE id_modalidad = @idModalidad;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Modalidad>(sql, new { idModalidad });
    }

    public async Task CrearAsync(Modalidad modalidad)
    {
        const string sql = @"
            INSERT INTO catedras.modalidad (id_modalidad, nombre, canal_difusion, activo)
            VALUES (@IdModalidad, @Nombre, @CanalDifusion, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, modalidad);
    }

    public async Task<int> ActualizarAsync(string idModalidad, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idModalidad);

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
            $"UPDATE catedras.modalidad SET {string.Join(", ", asignaciones)} " +
            "WHERE id_modalidad = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idModalidad)
    {
        const string sql = "DELETE FROM catedras.modalidad WHERE id_modalidad = @idModalidad;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si la modalidad tiene sesiones asociadas, fk_sesion_modalidad
        // (ON DELETE RESTRICT) hace que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idModalidad });
    }
}
