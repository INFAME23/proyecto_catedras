// ==========================================================================
//  Repositorios/RepositorioRolPorUsuarioPostgres.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  IRepositorioRolPorUsuario para PostgreSQL con Dapper. Mismo patrón que
//  RepositorioVinculacionAsistentePostgres (v3): tabla de llave compuesta,
//  sin columna 'id'. El WHERE del cierre usa las TRES columnas de la llave.
//
//  Se escribe aquí (Fase 39) y no en la Fase 40 porque ServicioUsuario ya lo
//  necesita para devolver los roles vigentes en el login: sin esta clase, la
//  API no arrancaría. El servicio y el controlador de RolPorUsuario siguen
//  siendo de la Fase 40.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.rol_por_usuario' en PostgreSQL mediante Dapper.</summary>
public class RepositorioRolPorUsuarioPostgres : IRepositorioRolPorUsuario
{
    private readonly string _cadenaConexion;

    public RepositorioRolPorUsuarioPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RolPorUsuario>> ObtenerTodosAsync(long? fkUsuario, int limite)
    {
        // Orden: por usuario y, dentro de cada uno, el más reciente primero.
        const string sql = @"
            SELECT fk_usuario, fk_rol, fecha_ini, fecha_fin
            FROM catedras.rol_por_usuario
            WHERE @fkUsuario::bigint IS NULL OR fk_usuario = @fkUsuario
            ORDER BY fk_usuario, fecha_ini DESC, fk_rol
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<RolPorUsuario>(sql, new { fkUsuario, limite });
        return filas.ToList();
    }

    public async Task<List<string>> ObtenerVigentesAsync(long fkUsuario)
    {
        // Vigente = sin fecha_fin. Solo se necesita el código del rol.
        const string sql = @"
            SELECT fk_rol
            FROM catedras.rol_por_usuario
            WHERE fk_usuario = @fkUsuario
              AND fecha_fin IS NULL
            ORDER BY fk_rol;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<string>(sql, new { fkUsuario });
        return filas.ToList();
    }

    public async Task CrearAsync(RolPorUsuario rol)
    {
        // Un rol nace abierto: 'fecha_fin' queda NULL.
        const string sql = @"
            INSERT INTO catedras.rol_por_usuario (fk_usuario, fk_rol, fecha_ini)
            VALUES (@FkUsuario, @FkRol, @FechaIni);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, rol);
    }

    public async Task<int> CerrarAsync(long fkUsuario, string fkRol, DateOnly fechaIni, DateOnly fechaFin)
    {
        // La llave COMPLETA identifica UNA fila; 'AND fecha_fin IS NULL' evita
        // cerrar dos veces el mismo rol aunque lleguen dos peticiones a la vez.
        const string sql = @"
            UPDATE catedras.rol_por_usuario
               SET fecha_fin = @fechaFin
             WHERE fk_usuario = @fkUsuario
               AND fk_rol = @fkRol
               AND fecha_ini = @fechaIni
               AND fecha_fin IS NULL;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { fkUsuario, fkRol, fechaIni, fechaFin });
    }
}
