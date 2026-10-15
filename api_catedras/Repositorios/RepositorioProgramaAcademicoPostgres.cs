// ==========================================================================
//  Repositorios/RepositorioProgramaAcademicoPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar / Consultar / Actualizar / Eliminar pasan por los procedimientos
//  de 'db/postgres/migraciones/008_sp_programa_academico.sql'. Mismo molde
//  que RepositorioSedePostgres.cs: solo arma el 'CALL', manda los
//  parámetros y convierte el JSON de vuelta. 'IRepositorioProgramaAcademico',
//  'ServicioProgramaAcademico', 'ProgramaAcademicoController' y
//  'Peticiones/' NO cambiaron; el PATCH parcial se mezcla AQUÍ.
//
//  CREAR NO CAMBIA: sigue siendo el 'CALL' a 'sp_guardar_programa_academico'
//  (migración 001, maestro + alias iniciales en una sola transacción). No
//  existe un 'sp_crear_programa_academico' a propósito.
//
//  'programa_academico' tiene 'activo': 'EliminarAsync' es un borrado
//  LÓGICO (lo hace el procedimiento) y el listado solo trae los activos.
//  Ya no hay DELETE, así que la FK RESTRICT desde 'programa_asistente' (y
//  la cascada a 'alias_programa') dejan de entrar en juego al eliminar.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;   // JsonSerializer: lista de alias -> JSON (Crear) y JSON de los SP -> objetos

/// <summary>Acceso a 'catedras.programa_academico' en PostgreSQL mediante Dapper.</summary>
public class RepositorioProgramaAcademicoPostgres : IRepositorioProgramaAcademico
{
    private readonly string _cadenaConexion;

    public RepositorioProgramaAcademicoPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ProgramaAcademico>> ObtenerTodosAsync(int limite, bool incluirInactivos)
    {
        const string sql = "CALL catedras.sp_listar_programa_academico(@limite, @incluirInactivos, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite, incluirInactivos });

        // Siempre un arreglo ('[]' si no hay filas, nunca NULL).
        return JsonSerializer.Deserialize<List<ProgramaAcademico>>(json!) ?? [];
    }

    public async Task<ProgramaAcademico?> ObtenerPorIdAsync(string codigo)
    {
        const string sql = "CALL catedras.sp_consultar_programa_academico(@codigo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { codigo });

        // NULL = el procedimiento no encontró la fila.
        return json is null ? null : JsonSerializer.Deserialize<ProgramaAcademico>(json);
    }

    public async Task CrearAsync(ProgramaAcademico programa, IReadOnlyList<AliasPrograma> aliasIniciales)
    {
        // Retrofit V2 (Guía IA, Regla 1): ya NO son dos INSERT sueltos desde
        // C#. Es UN 'CALL' al procedimiento almacenado, que inserta maestro
        // y detalles dentro de la misma transacción del motor.
        //
        // 'CAST(@Alias AS jsonb)': Dapper manda el JSON como TEXTO; el CAST
        // lo convierte en jsonb, que es el tipo del parámetro 'p_alias'.
        // Sigue siendo SQL a mano y 100% parametrizado (Artículo 2): el
        // JSON viaja como VALOR de un parámetro, nunca pegado al SQL.
        const string sql = @"
            CALL catedras.sp_guardar_programa_academico(
                @Codigo, @Nombre, @FkFacultad, @Nivel, @Activo,
                CAST(@Alias AS jsonb));";

        // El "formato" que espera el SP es un detalle del MOTOR, por eso se
        // arma aquí y no en el servicio (Artículo 3: el servicio no sabe qué
        // motor hay). Solo viajan las 2 columnas que pone el cliente:
        // 'fk_programa' la fija el SP con el código del maestro, y
        // 'id_alias' / 'creado_en' los pone la BD.
        // 'Select(...)' transforma cada AliasPrograma en un objeto anónimo
        // 'new { ... }' con los nombres EXACTOS que lee el SP (e ->> '...').
        string? aliasJson = aliasIniciales.Count == 0
            ? null    // sin alias -> p_alias NULL -> el SP solo crea el programa
            : JsonSerializer.Serialize(aliasIniciales.Select(a => new
            {
                texto_normalizado = a.TextoNormalizado,
                origen = a.Origen,
            }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Código duplicado, 'fk_facultad' inexistente o 'texto_normalizado'
        // repetido -> NpgsqlException, y el motor revierte el CALL completo.
        await conexion.ExecuteAsync(sql, new
        {
            programa.Codigo,
            programa.Nombre,
            programa.FkFacultad,
            programa.Nivel,
            programa.Activo,
            Alias = aliasJson,
        });
    }

    public async Task<int> ActualizarAsync(string codigo, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual: 'sp_actualizar_programa_academico' reemplaza TODAS
        //    las columnas, así que un PATCH necesita completar lo que no llegó.
        const string sqlConsultar = "CALL catedras.sp_consultar_programa_academico(@codigo, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { codigo });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<ProgramaAcademico>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual. 'codigo' (la PK) no
        //    se actualiza.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string? fkFacultad = datos.TryGetValue("fk_facultad", out var vFacultad) ? (string?)vFacultad : actual.FkFacultad;
        string nivel = datos.TryGetValue("nivel", out var vNivel) ? (string)vNivel! : actual.Nivel;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_programa_academico(
                @codigo, @nombre, @fkFacultad, @nivel, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { codigo, nombre, fkFacultad, nivel, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string codigo)
    {
        const string sql = "CALL catedras.sp_eliminar_programa_academico(@codigo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { codigo });

        // {"eliminado": true|false}; LÓGICO (activo=false).
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
