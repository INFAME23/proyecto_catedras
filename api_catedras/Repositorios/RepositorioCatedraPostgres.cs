// ==========================================================================
//  Repositorios/RepositorioCatedraPostgres.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  IRepositorioCatedra para PostgreSQL con Dapper.
//
//  Lo nuevo (3_plan.md §4.3, ADR D11): el INSERT devuelve DOS valores que
//  la BD calcula — 'id_catedra' (IDENTITY) y 'nombre_asis' (columna
//  GENERADA, left(nombre, 30)) — con un solo RETURNING. Así 'nombre_asis'
//  tiene una única fuente de verdad: la BD. El C# nunca la calcula.
//
//  'LIMIT @limite' con @limite = NULL equivale en PostgreSQL a "sin
//  límite" (LIMIT ALL): por eso el mismo SQL sirve con y sin límite.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.catedra' en PostgreSQL mediante Dapper.</summary>
public class RepositorioCatedraPostgres : IRepositorioCatedra
{
    private readonly string _cadenaConexion;

    // [ACID · C] Lista blanca del UPDATE. NO están: 'id_catedra' (PK),
    // 'nombre_asis' (generada: PostgreSQL rechazaría escribirla) ni
    // 'creado_en' (la pone la BD).
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "id_evento_asis", "nombre", "fk_tipo_evento", "fk_dependencia", "activo" };

    // Columnas de lectura, escritas una sola vez para los dos SELECT.
    private const string ColumnasSelect =
        "id_catedra, id_evento_asis, nombre, nombre_asis, fk_tipo_evento, fk_dependencia, activo, creado_en";

    public RepositorioCatedraPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Catedra>> ObtenerTodosAsync(int? limite)
    {
        // Interpolación '$"..."' SOLO con la constante de columnas (texto
        // nuestro, nunca del cliente). El valor del límite va como @parámetro.
        string sql = $@"
            SELECT {ColumnasSelect}
            FROM catedras.catedra
            ORDER BY id_catedra
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Catedra>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<int> ContarAsync()
    {
        const string sql = "SELECT COUNT(*) FROM catedras.catedra;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // COUNT(*) devuelve BIGINT; Dapper lo convierte al 'int' pedido.
        return await conexion.ExecuteScalarAsync<int>(sql);
    }

    public async Task<Catedra?> ObtenerPorIdAsync(long idCatedra)
    {
        string sql = $@"
            SELECT {ColumnasSelect}
            FROM catedras.catedra
            WHERE id_catedra = @idCatedra;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Catedra>(sql, new { idCatedra });
    }

    public async Task<(long IdCatedra, string NombreAsis)> CrearAsync(Catedra catedra)
    {
        // Ni id_catedra, ni nombre_asis, ni creado_en se envían: los pone la BD.
        const string sql = @"
            INSERT INTO catedras.catedra
                (id_evento_asis, nombre, fk_tipo_evento, fk_dependencia, activo)
            VALUES (@IdEventoAsis, @Nombre, @FkTipoEvento, @FkDependencia, @Activo)
            RETURNING id_catedra, nombre_asis;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila con dos columnas -> tupla (long, string), en ese orden.
        return await conexion.QuerySingleAsync<(long, string)>(sql, catedra);
    }

    public async Task<int> ActualizarAsync(long idCatedra, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idCatedra);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        // Si cambia 'nombre', PostgreSQL recalcula 'nombre_asis' en el mismo UPDATE.
        var sql =
            $"UPDATE catedras.catedra SET {string.Join(", ", asignaciones)} " +
            "WHERE id_catedra = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idCatedra)
    {
        const string sql = "DELETE FROM catedras.catedra WHERE id_catedra = @idCatedra;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // ⚠ CASCADE hacia 'sesion': el motor borra también sus sesiones.
        return await conexion.ExecuteAsync(sql, new { idCatedra });
    }
}
