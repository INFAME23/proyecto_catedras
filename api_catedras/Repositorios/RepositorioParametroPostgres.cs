// ==========================================================================
//  Repositorios/RepositorioParametroPostgres.cs  ·  Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/013_sp_parametro.sql'. Mismo molde que
//  RepositorioSedePostgres.cs: solo arma el 'CALL', manda los parámetros y
//  convierte el JSON que devuelve el procedimiento en objetos C#.
//
//  El PATCH parcial se mezcla AQUÍ: se consulta la fila actual, se mezcla
//  encima lo que llegó y se llama a 'sp_actualizar_parametro' con el
//  conjunto completo. 'modificado_en' lo refresca el procedimiento.
//
//  'parametro' NO tiene 'activo': 'EliminarAsync' es un DELETE físico (lo
//  hace el procedimiento) y el listado trae todas las filas.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.parametro' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioParametroPostgres : IRepositorioParametro
{
    private readonly string _cadenaConexion;

    public RepositorioParametroPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Parametro>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_parametro(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        var parametros = JsonSerializer.Deserialize<List<Parametro>>(json!) ?? [];
        foreach (var p in parametros)
            p.ModificadoEn = p.ModificadoEn.ToUniversalTime();   // ver nota al final
        return parametros;
    }

    public async Task<Parametro?> ObtenerPorIdAsync(string clave)
    {
        const string sql = "CALL catedras.sp_consultar_parametro(@clave, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { clave });

        if (json is null)
            return null;   // el procedimiento no encontró la fila

        var parametro = JsonSerializer.Deserialize<Parametro>(json)!;
        parametro.ModificadoEn = parametro.ModificadoEn.ToUniversalTime();
        return parametro;
    }

    public async Task CrearAsync(Parametro parametro)
    {
        const string sql = @"
            CALL catedras.sp_crear_parametro(
                @Clave, @Valor, @Descripcion, @TipoDato, @FkUsuarioModifica, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida, tipo_dato inválido o usuario inexistente ->
        // NpgsqlException. 'modificado_en' lo pone la BD.
        await conexion.ExecuteAsync(sql, parametro);
    }

    public async Task<int> ActualizarAsync(string clave, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual.
        const string sqlConsultar = "CALL catedras.sp_consultar_parametro(@clave, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { clave });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<Parametro>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual. 'clave' (la PK) no
        //    se actualiza.
        string valor = datos.TryGetValue("valor", out var vValor) ? (string)vValor! : actual.Valor;
        string tipoDato = datos.TryGetValue("tipo_dato", out var vTipo) ? (string)vTipo! : actual.TipoDato;
        string descripcion = datos.TryGetValue("descripcion", out var vDescripcion) ? (string)vDescripcion! : actual.Descripcion;
        long? fkUsuarioModifica = datos.TryGetValue("fk_usuario_modifica", out var vUsuario)
            ? (vUsuario is null ? null : Convert.ToInt64(vUsuario)) : actual.FkUsuarioModifica;

        // 3) Procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_parametro(
                @clave, @valor, @tipoDato, @descripcion, @fkUsuarioModifica, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { clave, valor, tipoDato, descripcion, fkUsuarioModifica });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string clave)
    {
        const string sql = "CALL catedras.sp_eliminar_parametro(@clave, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { clave });

        // {"eliminado": true|false}; FÍSICO (la tabla no tiene 'activo').
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }

    // NOTA SOBRE LAS FECHAS: 'row_to_json' escribe 'modificado_en' con el
    // desfase de la zona horaria de la SESIÓN ("...-05:00") y System.Text.Json
    // lo entrega como DateTime LOCAL. 'ToUniversalTime()' lo devuelve a UTC
    // para que la respuesta de la API serialice como el resto ("...Z").
}
