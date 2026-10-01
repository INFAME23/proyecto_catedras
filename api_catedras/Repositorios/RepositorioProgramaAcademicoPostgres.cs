// ==========================================================================
//  Repositorios/RepositorioProgramaAcademicoPostgres.cs  ·  Cátedras · v2
// --------------------------------------------------------------------------
//  IRepositorioProgramaAcademico para PostgreSQL con Dapper. SQL a mano y
//  parametrizado; una conexión por operación.
//
//  El DELETE es el más interesante de la v2 (5_data_model.md §2.3): la
//  tabla tiene DOS FK entrantes con reglas distintas —alias_programa
//  (CASCADE) y programa_asistente (RESTRICT)—. PostgreSQL las evalúa en la
//  MISMA sentencia: si hay asistentes, el DELETE completo falla y la
//  cascada a los alias NO llega a ocurrir (ACID · A: todo o nada). Este
//  repositorio no simula nada de eso: solo deja subir la NpgsqlException.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.programa_academico' en PostgreSQL mediante Dapper.</summary>
public class RepositorioProgramaAcademicoPostgres : IRepositorioProgramaAcademico
{
    private readonly string _cadenaConexion;

    // [ACID · C] Lista blanca del UPDATE. 'codigo' (la PK) no se actualiza.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "nombre", "fk_facultad", "nivel", "activo" };

    public RepositorioProgramaAcademicoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ProgramaAcademico>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT codigo, nombre, fk_facultad, nivel, activo
            FROM catedras.programa_academico
            ORDER BY codigo
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<ProgramaAcademico>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<ProgramaAcademico?> ObtenerPorIdAsync(string codigo)
    {
        const string sql = @"
            SELECT codigo, nombre, fk_facultad, nivel, activo
            FROM catedras.programa_academico
            WHERE codigo = @codigo;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<ProgramaAcademico>(sql, new { codigo });
    }

    public async Task CrearAsync(ProgramaAcademico programa)
    {
        const string sql = @"
            INSERT INTO catedras.programa_academico (codigo, nombre, fk_facultad, nivel, activo)
            VALUES (@Codigo, @Nombre, @FkFacultad, @Nivel, @Activo);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Código duplicado o 'fk_facultad' inexistente -> NpgsqlException.
        await conexion.ExecuteAsync(sql, programa);
    }

    public async Task<int> ActualizarAsync(string codigo, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", codigo);

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
            $"UPDATE catedras.programa_academico SET {string.Join(", ", asignaciones)} " +
            "WHERE codigo = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(string codigo)
    {
        const string sql = "DELETE FROM catedras.programa_academico WHERE codigo = @codigo;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // RESTRICT (programa_asistente) gana sobre CASCADE (alias_programa).
        return await conexion.ExecuteAsync(sql, new { codigo });
    }
}
