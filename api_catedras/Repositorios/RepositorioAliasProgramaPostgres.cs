// ==========================================================================
//  Repositorios/RepositorioAliasProgramaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar / Consultar / Crear / Eliminar pasan por los procedimientos de
//  'db/postgres/migraciones/009_sp_alias_programa.sql'. Mismo molde que
//  RepositorioSedePostgres.cs. Interfaz, servicio, controller y peticiones
//  solo ganaron Reemplazar/Actualizar (PUT/PATCH).
//
//  CREAR SUELTO vs CREAR CON EL PROGRAMA: el alta de un alias JUNTO con su
//  programa sigue siendo 'sp_guardar_programa_academico' (migración 001, lo
//  llama RepositorioProgramaAcademicoPostgres y NO se toca). Este
//  repositorio atiende el alta SUELTA (POST /api/AliasPrograma, un alias
//  para un programa que ya existe) con 'sp_crear_alias_programa'.
//
//  'alias_programa' NO tiene 'activo': 'EliminarAsync' es un DELETE físico
//  (lo hace el procedimiento) y el listado trae todas las filas.
//
//  'ActualizarAsync': consulta la fila, mezcla lo que llegó (PATCH) con lo
//  guardado y llama 'sp_actualizar_alias_programa' con las 3 columnas
//  completas, igual que el resto de repositorios. Se agregó porque el SP
//  no tenía llamador (la v2 trataba el alias como solo-anexo).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.alias_programa' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioAliasProgramaPostgres : IRepositorioAliasPrograma
{
    private readonly string _cadenaConexion;

    public RepositorioAliasProgramaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<AliasPrograma>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_alias_programa(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        var alias = JsonSerializer.Deserialize<List<AliasPrograma>>(json!) ?? [];
        foreach (var a in alias)
            a.CreadoEn = a.CreadoEn.ToUniversalTime();   // ver la nota sobre fechas al final
        return alias;
    }

    public async Task<AliasPrograma?> ObtenerPorIdAsync(long idAlias)
    {
        const string sql = "CALL catedras.sp_consultar_alias_programa(@idAlias, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idAlias });

        if (json is null)
            return null;   // el procedimiento no encontró la fila

        var alias = JsonSerializer.Deserialize<AliasPrograma>(json)!;
        alias.CreadoEn = alias.CreadoEn.ToUniversalTime();
        return alias;
    }

    public async Task<(long IdAlias, DateTime CreadoEn)> CrearAsync(AliasPrograma alias)
    {
        const string sql = @"
            CALL catedras.sp_crear_alias_programa(
                @TextoNormalizado, @FkPrograma, @Origen, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Texto repetido (uq_alias_texto) o programa inexistente
        // (fk_alias_programa) -> NpgsqlException, igual que antes.
        // 'id_alias' y 'creado_en' los pone la BD: se leen de la fila que
        // devuelve el procedimiento (antes venían del RETURNING).
        string? json = await conexion.QuerySingleAsync<string?>(sql, alias);

        var creada = JsonSerializer.Deserialize<AliasPrograma>(json!)!;
        return (creada.IdAlias, creada.CreadoEn.ToUniversalTime());
    }

    public async Task<int> ActualizarAsync(long idAlias, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual (el procedimiento reemplaza las 3 columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_alias_programa(@idAlias, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idAlias });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<AliasPrograma>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual.
        string textoNormalizado = datos.TryGetValue("texto_normalizado", out var vTexto) ? (string)vTexto! : actual.TextoNormalizado;
        string fkPrograma = datos.TryGetValue("fk_programa", out var vPrograma) ? (string)vPrograma! : actual.FkPrograma;
        string origen = datos.TryGetValue("origen", out var vOrigen) ? (string)vOrigen! : actual.Origen;

        // 3) Procedimiento con el conjunto COMPLETO. Texto repetido
        //    (uq_alias_texto) o programa inexistente (fk_alias_programa) ->
        //    NpgsqlException.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_alias_programa(
                @idAlias, @textoNormalizado, @fkPrograma, @origen, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idAlias, textoNormalizado, fkPrograma, origen });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idAlias)
    {
        const string sql = "CALL catedras.sp_eliminar_alias_programa(@idAlias, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idAlias });

        // {"eliminado": true|false}; FÍSICO (la tabla no tiene 'activo').
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }

    // NOTA SOBRE LAS FECHAS: 'row_to_json' escribe 'creado_en' con el
    // desfase de la zona horaria de la SESIÓN ("...-05:00") y System.Text.Json
    // lo entrega como DateTime LOCAL. Antes Npgsql entregaba DateTime UTC
    // directo de la columna. 'ToUniversalTime()' lo devuelve a UTC para que
    // la respuesta de la API siga serializando igual ("...Z").
}
