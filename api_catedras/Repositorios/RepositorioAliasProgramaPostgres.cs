// ==========================================================================
//  Repositorios/RepositorioAliasProgramaPostgres.cs  ·  Cátedras · v2
// --------------------------------------------------------------------------
//  IRepositorioAliasPrograma para PostgreSQL con Dapper. Sin UPDATE.
//
//  'CrearAsync' usa 'INSERT ... RETURNING id_alias, creado_en' y lee la
//  fila devuelta con 'QuerySingleAsync<(long, DateTime)>': Dapper llena la
//  tupla con las dos columnas, EN ORDEN. Un solo viaje a la BD.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.alias_programa' en PostgreSQL mediante Dapper.</summary>
public class RepositorioAliasProgramaPostgres : IRepositorioAliasPrograma
{
    private readonly string _cadenaConexion;

    public RepositorioAliasProgramaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<AliasPrograma>> ObtenerTodosAsync(int limite)
    {
        const string sql = @"
            SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
            FROM catedras.alias_programa
            ORDER BY id_alias
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<AliasPrograma>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<AliasPrograma?> ObtenerPorIdAsync(long idAlias)
    {
        const string sql = @"
            SELECT id_alias, texto_normalizado, fk_programa, origen, creado_en
            FROM catedras.alias_programa
            WHERE id_alias = @idAlias;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<AliasPrograma>(sql, new { idAlias });
    }

    public async Task<(long IdAlias, DateTime CreadoEn)> CrearAsync(AliasPrograma alias)
    {
        // Ni 'id_alias' (IDENTITY) ni 'creado_en' (DEFAULT now()) se envían:
        // los genera la BD y RETURNING los devuelve en la misma sentencia.
        const string sql = @"
            INSERT INTO catedras.alias_programa (texto_normalizado, fk_programa, origen)
            VALUES (@TextoNormalizado, @FkPrograma, @Origen)
            RETURNING id_alias, creado_en;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila con dos columnas -> una tupla (long, DateTime).
        return await conexion.QuerySingleAsync<(long, DateTime)>(sql, alias);
    }

    public async Task<int> EliminarAsync(long idAlias)
    {
        const string sql = "DELETE FROM catedras.alias_programa WHERE id_alias = @idAlias;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { idAlias });
    }
}
