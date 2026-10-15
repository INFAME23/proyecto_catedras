// ==========================================================================
//  Repositorios/RepositorioBitacoraPostgres.cs  ·  Cátedras · Versión 7
// --------------------------------------------------------------------------
//  Lectura de la bitácora por los procedimientos de
//  'db/postgres/migraciones/014_sp_bitacora.sql' (molde de
//  RepositorioSedePostgres.cs: un CALL que devuelve json en un INOUT).
//
//  Solo se llaman sp_listar_bitacora y sp_consultar_bitacora. Los otros
//  tres (crear / actualizar / eliminar) existen en la BD pero la API no
//  los usa: la auditoría la escriben los triggers (ADR D36, D37-A).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;            // QuerySingleAsync: ejecuta el CALL y lee la fila que devuelve
using Npgsql;            // NpgsqlConnection: la conexión a PostgreSQL
using System.Text.Json;  // JsonSerializer: convierte el json del SP en objetos C#

/// <summary>Lectura de 'catedras.bitacora' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioBitacoraPostgres : IRepositorioBitacora
{
    private readonly string _cadenaConexion;

    public RepositorioBitacoraPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Bitacora>> ObtenerTodosAsync(string? nombreTabla, int limite)
    {
        // 'nombreTabla' null = todas las tablas (el procedimiento lo trata
        // como "sin filtro"). El último argumento (NULL) es el INOUT donde
        // el SP deja el arreglo json.
        const string sql = "CALL catedras.sp_listar_bitacora(@nombreTabla, @limite, NULL);";

        // 'await using' cierra la conexión al salir del método, pase lo que pase.
        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { nombreTabla, limite });

        // El SP devuelve '[]' cuando no hay filas, nunca NULL.
        var entradas = JsonSerializer.Deserialize<List<Bitacora>>(json!) ?? [];
        foreach (var b in entradas)
            b.OcurridoEn = b.OcurridoEn.ToUniversalTime();   // ver nota al final
        return entradas;
    }

    public async Task<Bitacora?> ObtenerPorIdAsync(long idBitacora)
    {
        const string sql = "CALL catedras.sp_consultar_bitacora(@idBitacora, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idBitacora });

        if (json is null)
            return null;   // el procedimiento no encontró la fila -> el servicio lanza 404

        var entrada = JsonSerializer.Deserialize<Bitacora>(json)!;
        entrada.OcurridoEn = entrada.OcurridoEn.ToUniversalTime();
        return entrada;
    }

    // NOTA SOBRE LAS FECHAS: 'row_to_json' escribe 'ocurrido_en' con el
    // desfase de la zona horaria de la SESIÓN ("...-05:00") y System.Text.Json
    // lo entrega como DateTime LOCAL. 'ToUniversalTime()' lo devuelve a UTC
    // para que la respuesta de la API serialice como el resto ("...Z").
}
