// ==========================================================================
//  Repositorios/RepositorioPonenciaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar / Consultar / Crear / Eliminar pasan por los procedimientos de
//  'db/postgres/migraciones/012_sp_ponencia.sql'. Mismo molde que
//  RepositorioSedePostgres.cs. 'IRepositorioPonencia', 'ServicioPonencia',
//  'PonenciaController' y 'Peticiones/' NO cambiaron.
//
//  LLAVE COMPUESTA: (fk_sesion, fk_asistente, rol), sin id propio. Consultar
//  y Eliminar mandan los TRES valores al procedimiento.
//
//  'ponencia' NO tiene 'activo': 'EliminarAsync' es un DELETE físico (lo hace
//  el procedimiento) y el listado trae todas las filas.
//
//  'ActualizarAsync' llama a 'sp_actualizar_ponencia': como las 3 columnas son
//  la llave, lo único que se puede reemplazar es el ROL (llave vieja + rol
//  nuevo). No hay PATCH que mezclar: no queda ningún otro campo.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.ponencia' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioPonenciaPostgres : IRepositorioPonencia
{
    private readonly string _cadenaConexion;

    public RepositorioPonenciaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Ponencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite)
    {
        // Filtros null = "sin filtro" (el procedimiento los trata así).
        const string sql = "CALL catedras.sp_listar_ponencia(@fkSesion, @fkAsistente, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkAsistente, limite });

        return JsonSerializer.Deserialize<List<Ponencia>>(json!) ?? [];
    }

    public async Task<Ponencia?> ObtenerUnoAsync(long fkSesion, long fkAsistente, string rol)
    {
        const string sql = "CALL catedras.sp_consultar_ponencia(@fkSesion, @fkAsistente, @rol, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkAsistente, rol });

        // NULL = el procedimiento no encontró la fila.
        return json is null ? null : JsonSerializer.Deserialize<Ponencia>(json);
    }

    public async Task CrearAsync(Ponencia ponencia)
    {
        const string sql = @"
            CALL catedras.sp_crear_ponencia(
                @FkSesion, @FkAsistente, @Rol, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Llave repetida, rol inválido, sesión o asistente inexistentes ->
        // NpgsqlException, igual que antes. 'CrearAsync' no devuelve nada
        // (el servicio ya tiene los 3 valores), así que no hace falta leer
        // el JSON de vuelta.
        await conexion.ExecuteAsync(sql, ponencia);
    }

    public async Task<int> ActualizarAsync(long fkSesion, long fkAsistente, string rol, string rolNuevo)
    {
        const string sql = @"
            CALL catedras.sp_actualizar_ponencia(
                @fkSesion, @fkAsistente, @rol, @rolNuevo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Rol nuevo fuera de la lista (chk_ponencia_rol) o ya ocupado por el
        // mismo asistente en la sesión (pk_ponencia) -> NpgsqlException.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkAsistente, rol, rolNuevo });

        // NULL = la llave vieja no existe.
        return json is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol)
    {
        const string sql = "CALL catedras.sp_eliminar_ponencia(@fkSesion, @fkAsistente, @rol, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkAsistente, rol });

        // {"eliminado": true|false}; FÍSICO (la tabla no tiene 'activo').
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
