// ==========================================================================
//  Repositorios/RepositorioOpcionPreguntaPostgres.cs  ·  Cátedras · v6
// --------------------------------------------------------------------------
//  IRepositorioOpcionPregunta para PostgreSQL con Dapper. CRUD estándar,
//  sin SP: a diferencia de 'Pregunta', 'OpcionPregunta' no tiene detalle
//  propio, así que 'CrearAsync' es un INSERT directo con RETURNING (mismo
//  molde que RepositorioSesionPostgres, v4).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.opcion_pregunta' en PostgreSQL mediante Dapper.</summary>
public class RepositorioOpcionPreguntaPostgres : IRepositorioOpcionPregunta
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "etiqueta", "valor", "orden" };

    public RepositorioOpcionPreguntaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<OpcionPregunta>> ObtenerTodosAsync(long? fkPregunta, int limite)
    {
        const string sql = @"
            SELECT id_opcion, fk_pregunta, etiqueta, valor, orden
            FROM catedras.opcion_pregunta
            WHERE (@fkPregunta::bigint IS NULL OR fk_pregunta = @fkPregunta)
            ORDER BY fk_pregunta, orden
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<OpcionPregunta>(sql, new { fkPregunta, limite });
        return filas.ToList();
    }

    public async Task<OpcionPregunta?> ObtenerPorIdAsync(long idOpcion)
    {
        const string sql = @"
            SELECT id_opcion, fk_pregunta, etiqueta, valor, orden
            FROM catedras.opcion_pregunta
            WHERE id_opcion = @idOpcion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<OpcionPregunta>(sql, new { idOpcion });
    }

    public async Task<long> CrearAsync(OpcionPregunta opcion)
    {
        // 'id_opcion' no se envía: lo pone la BD. '(fk_pregunta, orden)'
        // duplicado (uq_opcion_orden) o 'fk_pregunta' inexistente -> el
        // motor rechaza el INSERT -> NpgsqlException (500).
        const string sql = @"
            INSERT INTO catedras.opcion_pregunta (fk_pregunta, etiqueta, valor, orden)
            VALUES (@FkPregunta, @Etiqueta, @Valor, @Orden)
            RETURNING id_opcion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteScalarAsync<long>(sql, opcion);
    }

    public async Task<int> ActualizarAsync(long idOpcion, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idOpcion);

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
            $"UPDATE catedras.opcion_pregunta SET {string.Join(", ", asignaciones)} " +
            "WHERE id_opcion = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idOpcion)
    {
        const string sql = "DELETE FROM catedras.opcion_pregunta WHERE id_opcion = @idOpcion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // RESTRICT (respuesta_item.fk_opcion): una opción ya elegida en
        // alguna respuesta de ejemplo no se puede borrar -> 500.
        return await conexion.ExecuteAsync(sql, new { idOpcion });
    }
}
