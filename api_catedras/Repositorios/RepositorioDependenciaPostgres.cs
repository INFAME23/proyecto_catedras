// ==========================================================================
//  Repositorios/RepositorioDependenciaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/007_sp_dependencia.sql'. Mismo molde que
//  RepositorioSedePostgres.cs: solo arma el 'CALL', manda los parámetros y
//  convierte el JSON de vuelta. 'IRepositorioDependencia',
//  'ServicioDependencia', 'DependenciaController' y 'Peticiones/' NO
//  cambiaron; el PATCH parcial se mezcla AQUÍ con la fila actual.
//
//  'dependencia' tiene 'activo': 'EliminarAsync' es LÓGICO y el listado solo
//  trae las activas.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.dependencia' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioDependenciaPostgres : IRepositorioDependencia
{
    private readonly string _cadenaConexion;

    public RepositorioDependenciaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Dependencia>> ObtenerTodosAsync(int limite, bool incluirInactivos)
    {
        const string sql = "CALL catedras.sp_listar_dependencia(@limite, @incluirInactivos, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite, incluirInactivos });

        return JsonSerializer.Deserialize<List<Dependencia>>(json!) ?? [];
    }

    public async Task<Dependencia?> ObtenerPorIdAsync(string idDependencia)
    {
        const string sql = "CALL catedras.sp_consultar_dependencia(@idDependencia, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idDependencia });

        return json is null ? null : JsonSerializer.Deserialize<Dependencia>(json);
    }

    public async Task CrearAsync(Dependencia dependencia)
    {
        const string sql = @"
            CALL catedras.sp_crear_dependencia(
                @IdDependencia, @Nombre, @FkDependenciaPadre, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave o nombre repetido, padre inexistente o igual a sí misma ->
        // NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, dependencia);
    }

    public async Task<int> ActualizarAsync(string idDependencia, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual.
        const string sqlConsultar = "CALL catedras.sp_consultar_dependencia(@idDependencia, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idDependencia });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<Dependencia>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string? fkDependenciaPadre = datos.TryGetValue("fk_dependencia_padre", out var vPadre) ? (string?)vPadre : actual.FkDependenciaPadre;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_dependencia(
                @idDependencia, @nombre, @fkDependenciaPadre, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idDependencia, nombre, fkDependenciaPadre, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idDependencia)
    {
        const string sql = "CALL catedras.sp_eliminar_dependencia(@idDependencia, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idDependencia });

        // {"eliminado": true|false}; LÓGICO (activo=false).
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
