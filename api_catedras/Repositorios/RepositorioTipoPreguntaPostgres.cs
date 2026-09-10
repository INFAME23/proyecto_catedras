// ==========================================================================
//  Repositorios/RepositorioTipoPreguntaPostgres.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Implementación de IRepositorioTipoPregunta para PostgreSQL con Dapper.
//  SQL a mano, SIEMPRE parametrizado; una conexión por operación. Mismo
//  patrón que RepositorioSedePostgres.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.tipo_pregunta' en PostgreSQL mediante Dapper.</summary>
public class RepositorioTipoPreguntaPostgres : IRepositorioTipoPregunta
{
    private readonly string _cadenaConexion;

    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "guarda_numero", "guarda_texto", "guarda_opcion" };

    public RepositorioTipoPreguntaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoPregunta>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion
            FROM catedras.tipo_pregunta
            ORDER BY id_tipo_pregunta
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<TipoPregunta>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<TipoPregunta?> ObtenerPorIdAsync(string idTipoPregunta)
    {
        const string sql = @"
            SELECT id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion
            FROM catedras.tipo_pregunta
            WHERE id_tipo_pregunta = @idTipoPregunta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<TipoPregunta>(sql, new { idTipoPregunta });
    }

    public async Task CrearAsync(TipoPregunta tipoPregunta)
    {
        const string sql = @"
            INSERT INTO catedras.tipo_pregunta
                (id_tipo_pregunta, nombre, guarda_numero, guarda_texto, guarda_opcion)
            VALUES (@IdTipoPregunta, @Nombre, @GuardaNumero, @GuardaTexto, @GuardaOpcion);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, tipoPregunta);
    }

    public async Task<int> ActualizarAsync(string idTipoPregunta, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idTipoPregunta);

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
            $"UPDATE catedras.tipo_pregunta SET {string.Join(", ", asignaciones)} " +
            "WHERE id_tipo_pregunta = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string idTipoPregunta)
    {
        const string sql = "DELETE FROM catedras.tipo_pregunta WHERE id_tipo_pregunta = @idTipoPregunta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Si el tipo de pregunta tiene preguntas asociadas, fk_pregunta_tipo
        // (ON DELETE RESTRICT) hace que PostgreSQL lance NpgsqlException.
        return await conexion.ExecuteAsync(sql, new { idTipoPregunta });
    }
}
