// ==========================================================================
//  Repositorios/RepositorioEnlaceRegistroPostgres.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  IRepositorioEnlaceRegistro para PostgreSQL con Dapper.
//
//  Dos detalles propios de esta tabla:
//
//  1) 'ventana' es un tstzrange y Dapper no sabe leerlo. El SELECT lo parte
//     en dos columnas planas: lower(ventana) AS ventana_desde y
//     upper(ventana) AS ventana_hasta.
//
//  2) EMITIR no es un INSERT: llama a la función fn_emitir_enlace_api (ADR
//     D24), que internamente hace CALL a sp_emitir_enlace. La función calcula
//     token, url, canal y ventana, y revoca el enlace vigente anterior
//     (RN-14). Si la sesión no existe hace RAISE EXCEPTION -> NpgsqlException
//     -> el controlador responde 500 (ADR D25).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.enlace_registro' en PostgreSQL mediante Dapper.</summary>
public class RepositorioEnlaceRegistroPostgres : IRepositorioEnlaceRegistro
{
    private readonly string _cadenaConexion;

    // Columnas de lectura, escritas una sola vez. 'ventana' se parte en dos.
    private const string Columnas = @"
            id_enlace, fk_sesion, token, url_publica, ruta_imagen_qr, canal,
            lower(ventana) AS ventana_desde, upper(ventana) AS ventana_hasta,
            usos_maximos, revocado_en, fk_usuario_crea, creado_en";

    public RepositorioEnlaceRegistroPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<EnlaceRegistro>> ObtenerTodosAsync(long? fkSesion, int limite)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.enlace_registro
            WHERE @fkSesion::bigint IS NULL OR fk_sesion = @fkSesion
            ORDER BY id_enlace
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<EnlaceRegistro>(sql, new { fkSesion, limite });
        return filas.ToList();
    }

    public async Task<EnlaceRegistro?> ObtenerPorIdAsync(long idEnlace)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.enlace_registro
            WHERE id_enlace = @idEnlace;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<EnlaceRegistro>(sql, new { idEnlace });
    }

    public async Task<long> EmitirAsync(long fkSesion, int? minutosAntes, int? minutosDespues, long? fkUsuario)
    {
        // Los parámetros opcionales viajan como NULL y la función aplica sus
        // valores por defecto (tabla 'parametro' o 30 minutos).
        const string sql = @"
            SELECT * FROM catedras.fn_emitir_enlace_api(
                @fkSesion, @minutosAntes, @minutosDespues, @fkUsuario);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteScalarAsync<long>(sql,
            new { fkSesion, minutosAntes, minutosDespues, fkUsuario });
    }

    public async Task<int> RevocarAsync(long idEnlace)
    {
        // 'AND revocado_en IS NULL': un segundo intento afecta 0 filas y el
        // servicio lo traduce a 400, en vez de un UPDATE "exitoso" que no
        // cambió nada.
        const string sql = @"
            UPDATE catedras.enlace_registro
               SET revocado_en = now()
             WHERE id_enlace = @idEnlace
               AND revocado_en IS NULL;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, new { idEnlace });
    }
}
