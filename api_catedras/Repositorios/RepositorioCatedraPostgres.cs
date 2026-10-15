// ==========================================================================
//  Repositorios/RepositorioCatedraPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos (y el Contar) pasan por los procedimientos de
//  'db/postgres/migraciones/010_sp_catedra.sql'. Mismo molde que
//  RepositorioSedePostgres.cs. 'IRepositorioCatedra', 'ServicioCatedra',
//  'CatedraController' y 'Peticiones/' NO cambiaron.
//
//  · 'catedra' tiene 'activo': 'EliminarAsync' es LÓGICO; el listado y el
//    conteo solo cuentan las activas (el 'total' y las filas coinciden).
//  · 'nombre_asis' es una columna GENERADA por PostgreSQL: nunca se manda
//    al procedimiento; llega en el JSON de vuelta (CrearAsync la devuelve).
//  · 'creado_en' lo pone la BD; se lee del JSON y se normaliza a UTC.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.catedra' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioCatedraPostgres : IRepositorioCatedra
{
    private readonly string _cadenaConexion;

    public RepositorioCatedraPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Catedra>> ObtenerTodosAsync(int? limite, bool incluirInactivos)
    {
        // 'limite' puede ser null (= sin límite): el procedimiento hace
        // 'LIMIT p_limite' y LIMIT NULL no recorta nada.
        const string sql = "CALL catedras.sp_listar_catedra(@limite, @incluirInactivos, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite, incluirInactivos });

        var catedras = JsonSerializer.Deserialize<List<Catedra>>(json!) ?? [];
        foreach (var c in catedras)
            c.CreadoEn = c.CreadoEn.ToUniversalTime();   // ver la nota sobre fechas al final
        return catedras;
    }

    public async Task<int> ContarAsync(bool incluirInactivos)
    {
        const string sql = "CALL catedras.sp_contar_catedra(@incluirInactivos, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { incluirInactivos });

        // {"total": n}
        using var documento = JsonDocument.Parse(json!);
        return documento.RootElement.GetProperty("total").GetInt32();
    }

    public async Task<Catedra?> ObtenerPorIdAsync(long idCatedra)
    {
        const string sql = "CALL catedras.sp_consultar_catedra(@idCatedra, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idCatedra });

        if (json is null)
            return null;   // el procedimiento no encontró la fila

        var catedra = JsonSerializer.Deserialize<Catedra>(json)!;
        catedra.CreadoEn = catedra.CreadoEn.ToUniversalTime();
        return catedra;
    }

    public async Task<(long IdCatedra, string NombreAsis)> CrearAsync(Catedra catedra)
    {
        // No se manda 'NombreAsis' (columna generada) ni 'IdCatedra' (identity).
        const string sql = @"
            CALL catedras.sp_crear_catedra(
                @IdEventoAsis, @Nombre, @FkTipoEvento, @FkDependencia, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // id_evento_asis mal formado (chk_catedra_asis), tipo de evento o
        // dependencia inexistentes -> NpgsqlException, igual que antes. El
        // id y el nombre_asis salen de la fila que devuelve el
        // procedimiento (antes venían del RETURNING).
        string? json = await conexion.QuerySingleAsync<string?>(sql, catedra);

        var creada = JsonSerializer.Deserialize<Catedra>(json!)!;
        return (creada.IdCatedra, creada.NombreAsis);
    }

    public async Task<int> ActualizarAsync(long idCatedra, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual: 'sp_actualizar_catedra' reemplaza TODAS las
        //    columnas de negocio, así que un PATCH necesita completar lo que
        //    no llegó con el valor guardado.
        const string sqlConsultar = "CALL catedras.sp_consultar_catedra(@idCatedra, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idCatedra });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<Catedra>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual.
        string idEventoAsis = datos.TryGetValue("id_evento_asis", out var vEvento) ? (string)vEvento! : actual.IdEventoAsis;
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string fkTipoEvento = datos.TryGetValue("fk_tipo_evento", out var vTipo) ? (string)vTipo! : actual.FkTipoEvento;
        string? fkDependencia = datos.TryGetValue("fk_dependencia", out var vDependencia) ? (string?)vDependencia : actual.FkDependencia;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_catedra(
                @idCatedra, @idEventoAsis, @nombre, @fkTipoEvento, @fkDependencia, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idCatedra, idEventoAsis, nombre, fkTipoEvento, fkDependencia, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idCatedra)
    {
        const string sql = "CALL catedras.sp_eliminar_catedra(@idCatedra, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idCatedra });

        // {"eliminado": true|false}; LÓGICO (activo=false).
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
