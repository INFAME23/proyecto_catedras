// ==========================================================================
//  Repositorios/RepositorioRolPorUsuarioPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 4 métodos de IRepositorioRolPorUsuario pasan por los procedimientos
//  de 'db/postgres/migraciones/007_sp_rol_por_usuario.sql' (molde: sede).
//
//  La BD tiene además sp_consultar_ / sp_actualizar_ / sp_eliminar_
//  rol_por_usuario (los 5 verbos del criterio), pero IRepositorioRolPorUsuario
//  no los pide y la regla del retrofit es NO tocar la interfaz: quedan
//  listos en la BD, sin exponer.
//
//  FECHAS: 'fecha_ini'/'fecha_fin' son DATE. El ManejadorTipoDateOnly manda
//  un DateOnly como DateTime (-> timestamp), y un CALL no aplica casts de
//  asignación: sin CAST(... AS date) PostgreSQL responde "procedure ...
//  does not exist". Por eso cada fecha va con CAST explícito.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.rol_por_usuario' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioRolPorUsuarioPostgres : IRepositorioRolPorUsuario
{
    private readonly string _cadenaConexion;

    public RepositorioRolPorUsuarioPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RolPorUsuario>> ObtenerTodosAsync(long? fkUsuario, int limite)
    {
        // Filtro opcional: NULL = todos los usuarios.
        const string sql = "CALL catedras.sp_listar_rol_por_usuario(@fkUsuario::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkUsuario, limite });

        return LectorJsonSp.Lista<RolPorUsuario>(json);
    }

    public async Task<List<string>> ObtenerVigentesAsync(long fkUsuario)
    {
        // Arreglo JSON de códigos de rol abiertos: ["ADMIN","CONSULTA"].
        const string sql = "CALL catedras.sp_listar_rol_vigente_usuario(@fkUsuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkUsuario });

        return LectorJsonSp.Lista<string>(json);
    }

    public async Task CrearAsync(RolPorUsuario rol)
    {
        // Un rol nace abierto: 'fecha_fin' queda NULL.
        const string sql = @"
            CALL catedras.sp_crear_rol_por_usuario(
                @FkUsuario, @FkRol, CAST(@FechaIni AS date), NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, new { rol.FkUsuario, rol.FkRol, rol.FechaIni });
    }

    public async Task<int> CerrarAsync(long fkUsuario, string fkRol, DateOnly fechaIni, DateOnly fechaFin)
    {
        // 'sp_cerrar_' (y no 'sp_actualizar_') conserva la guarda
        // 'fecha_fin IS NULL': no se cierra dos veces el mismo rol.
        const string sql = @"
            CALL catedras.sp_cerrar_rol_por_usuario(
                @fkUsuario, @fkRol, CAST(@fechaIni AS date), CAST(@fechaFin AS date), NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql,
            new { fkUsuario, fkRol, fechaIni, fechaFin });

        // NULL = no existe o ya estaba cerrado -> "0 filas", igual que antes.
        return json is null ? 0 : 1;
    }
}
