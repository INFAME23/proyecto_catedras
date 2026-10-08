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
//
//  Retrofit V2: el CREATE ya no es un INSERT suelto, sino un CALL al SP
//  'sp_guardar_programa_academico' (maestro + alias iniciales en una sola
//  transacción). El SP está en db/postgres/migraciones/001_....sql.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;   // JsonSerializer: convierte la lista de alias en texto JSON

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

    public async Task CrearAsync(ProgramaAcademico programa, IReadOnlyList<AliasPrograma> aliasIniciales)
    {
        // Retrofit V2 (Guía IA, Regla 1): ya NO son dos INSERT sueltos desde
        // C#. Es UN 'CALL' al procedimiento almacenado, que inserta maestro
        // y detalles dentro de la misma transacción del motor.
        //
        // 'CAST(@Alias AS jsonb)': Dapper manda el JSON como TEXTO; el CAST
        // lo convierte en jsonb, que es el tipo del parámetro 'p_alias'.
        // Sigue siendo SQL a mano y 100% parametrizado (Artículo 2): el
        // JSON viaja como VALOR de un parámetro, nunca pegado al SQL.
        const string sql = @"
            CALL catedras.sp_guardar_programa_academico(
                @Codigo, @Nombre, @FkFacultad, @Nivel, @Activo,
                CAST(@Alias AS jsonb));";

        // El "formato" que espera el SP es un detalle del MOTOR, por eso se
        // arma aquí y no en el servicio (Artículo 3: el servicio no sabe qué
        // motor hay). Solo viajan las 2 columnas que pone el cliente:
        // 'fk_programa' la fija el SP con el código del maestro, y
        // 'id_alias' / 'creado_en' los pone la BD.
        // 'Select(...)' transforma cada AliasPrograma en un objeto anónimo
        // 'new { ... }' con los nombres EXACTOS que lee el SP (e ->> '...').
        string? aliasJson = aliasIniciales.Count == 0
            ? null    // sin alias -> p_alias NULL -> el SP solo crea el programa
            : JsonSerializer.Serialize(aliasIniciales.Select(a => new
            {
                texto_normalizado = a.TextoNormalizado,
                origen = a.Origen,
            }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Código duplicado, 'fk_facultad' inexistente o 'texto_normalizado'
        // repetido -> NpgsqlException, y el motor revierte el CALL completo.
        await conexion.ExecuteAsync(sql, new
        {
            programa.Codigo,
            programa.Nombre,
            programa.FkFacultad,
            programa.Nivel,
            programa.Activo,
            Alias = aliasJson,
        });
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
