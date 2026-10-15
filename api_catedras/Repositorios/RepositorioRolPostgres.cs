// ==========================================================================
//  Repositorios/RepositorioRolPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/010_sp_rol.sql', con el MISMO molde de
//  RepositorioSedePostgres.cs: esta clase solo arma el 'CALL', manda los
//  parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'rol' NO tiene 'activo' -> el eliminar es un DELETE físico.
//  'IRepositorioRol', 'ServicioRol', 'RolController' y las 'Peticiones/'
//  NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.rol' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioRolPostgres : IRepositorioRol
{
    private readonly string _cadenaConexion;

    public RepositorioRolPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Rol>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_rol(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<Rol>>(json!) ?? [];
    }

    public async Task<Rol?> ObtenerPorIdAsync(string idRol)
    {
        const string sql = "CALL catedras.sp_consultar_rol(@idRol, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idRol });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<Rol>(json);
    }

    public async Task CrearAsync(Rol rol)
    {
        const string sql = @"
            CALL catedras.sp_crear_rol(
                @IdRol, @Nombre, @Descripcion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, rol);
    }

    public async Task<int> ActualizarAsync(string idRol, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_rol(@idRol, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idRol });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<Rol>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string? descripcion = datos.TryGetValue("descripcion", out var vDescripcion) ? (string?)vDescripcion : actual.Descripcion;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_rol(
                @idRol, @nombre, @descripcion, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idRol, nombre, descripcion });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idRol)
    {
        const string sql = "CALL catedras.sp_eliminar_rol(@idRol, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idRol });

        // {"eliminado": true|false}. Sin 'activo': DELETE físico.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
