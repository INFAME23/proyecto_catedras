// ==========================================================================
//  Repositorios/RepositorioBitacoraPostgres.cs  ·  Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/014_sp_bitacora.sql'. Mismo molde que
//  RepositorioSedePostgres.cs.
//
//  jsonb: 'datos_antes' / 'datos_despues' viajan como TEXTO (el
//  JsonElement.GetRawText()) con 'CAST(@x AS jsonb)' en el CALL, para no
//  depender de cómo Dapper/Npgsql mapean un JsonElement.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

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
        // como "sin filtro").
        const string sql = "CALL catedras.sp_listar_bitacora(@nombreTabla, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { nombreTabla, limite });

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
            return null;   // el procedimiento no encontró la fila

        var entrada = JsonSerializer.Deserialize<Bitacora>(json)!;
        entrada.OcurridoEn = entrada.OcurridoEn.ToUniversalTime();
        return entrada;
    }

    public async Task<Bitacora> CrearAsync(string nombreTabla, string operacion, long? fkUsuario,
                                           string? llave, JsonElement? datosAntes, JsonElement? datosDespues)
    {
        // El procedimiento NO recibe 'ocurrido_en' ni 'usuario_bd'.
        const string sql = @"
            CALL catedras.sp_crear_bitacora(
                @nombreTabla, @operacion, @fkUsuario, @llave,
                CAST(@datosAntes AS jsonb), CAST(@datosDespues AS jsonb), NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Operación inválida (chk_bitacora_operacion) o usuario inexistente
        // (fk_bitacora_usuario) -> NpgsqlException.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            nombreTabla,
            operacion,
            fkUsuario,
            llave,
            datosAntes = ATexto(datosAntes),
            datosDespues = ATexto(datosDespues),
        });

        var creada = JsonSerializer.Deserialize<Bitacora>(json!)!;
        creada.OcurridoEn = creada.OcurridoEn.ToUniversalTime();
        return creada;
    }

    public async Task<int> ActualizarAsync(long idBitacora, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual (el procedimiento reemplaza las 6 columnas de negocio).
        const string sqlConsultar = "CALL catedras.sp_consultar_bitacora(@idBitacora, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idBitacora });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<Bitacora>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual (un 'null' explícito
        //    en un PUT sí borra la columna).
        long? fkUsuario = datos.TryGetValue("fk_usuario", out var vUsuario)
            ? (vUsuario is null ? null : Convert.ToInt64(vUsuario)) : actual.FkUsuario;
        string nombreTabla = datos.TryGetValue("nombre_tabla", out var vTabla) ? (string)vTabla! : actual.NombreTabla;
        string operacion = datos.TryGetValue("operacion", out var vOperacion) ? (string)vOperacion! : actual.Operacion;
        string? llave = datos.TryGetValue("llave", out var vLlave) ? (string?)vLlave : actual.Llave;
        string? datosAntes = datos.TryGetValue("datos_antes", out var vAntes) ? ATexto(vAntes as JsonElement?) : ATexto(actual.DatosAntes);
        string? datosDespues = datos.TryGetValue("datos_despues", out var vDespues) ? ATexto(vDespues as JsonElement?) : ATexto(actual.DatosDespues);

        // 3) Procedimiento con el conjunto COMPLETO. NO toca ocurrido_en ni usuario_bd.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_bitacora(
                @idBitacora, @fkUsuario, @nombreTabla, @operacion, @llave,
                CAST(@datosAntes AS jsonb), CAST(@datosDespues AS jsonb), NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(sqlActualizar, new
        {
            idBitacora,
            fkUsuario,
            nombreTabla,
            operacion,
            llave,
            datosAntes,
            datosDespues,
        });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idBitacora)
    {
        const string sql = "CALL catedras.sp_eliminar_bitacora(@idBitacora, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idBitacora });

        // {"eliminado": true|false}; FÍSICO (la tabla no tiene 'activo').
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }

    // JsonElement? -> texto JSON (o null = SQL NULL).
    private static string? ATexto(JsonElement? valor) =>
        valor is null || valor.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : valor.Value.GetRawText();

    // NOTA SOBRE LAS FECHAS: 'row_to_json' escribe 'ocurrido_en' con el
    // desfase de la zona horaria de la SESIÓN ("...-05:00") y System.Text.Json
    // lo entrega como DateTime LOCAL. 'ToUniversalTime()' lo devuelve a UTC
    // para que la respuesta de la API serialice como el resto ("...Z").
}
