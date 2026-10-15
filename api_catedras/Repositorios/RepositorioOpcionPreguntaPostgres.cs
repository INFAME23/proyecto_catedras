// ==========================================================================
//  Repositorios/RepositorioOpcionPreguntaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/012_sp_opcion_pregunta.sql'. Es la tabla del
//  lote más parecida a 'sede': CRUD estándar, borrado FÍSICO (no tiene
//  'activo'). Diferencia: 'id_opcion' lo genera la BD, así que CrearAsync
//  lo lee de la fila que devuelve el procedimiento.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.opcion_pregunta' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioOpcionPreguntaPostgres : IRepositorioOpcionPregunta
{
    private readonly string _cadenaConexion;

    public RepositorioOpcionPreguntaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<OpcionPregunta>> ObtenerTodosAsync(long? fkPregunta, int limite)
    {
        const string sql = "CALL catedras.sp_listar_opcion_pregunta(@fkPregunta::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkPregunta, limite });

        return LectorJsonSp.Lista<OpcionPregunta>(json);
    }

    public async Task<OpcionPregunta?> ObtenerPorIdAsync(long idOpcion)
    {
        const string sql = "CALL catedras.sp_consultar_opcion_pregunta(@idOpcion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idOpcion });

        return LectorJsonSp.Fila<OpcionPregunta>(json);
    }

    public async Task<long> CrearAsync(OpcionPregunta opcion)
    {
        // Orden de parámetros del SP: obligatorios primero, 'valor' (con
        // DEFAULT NULL) al final. '(fk_pregunta, orden)' duplicado o
        // pregunta inexistente -> NpgsqlException, igual que antes.
        const string sql = @"
            CALL catedras.sp_crear_opcion_pregunta(
                @FkPregunta, @Etiqueta, @Orden, @Valor, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            opcion.FkPregunta,
            opcion.Etiqueta,
            opcion.Orden,
            opcion.Valor,
        });

        return LectorJsonSp.Fila<OpcionPregunta>(json)!.IdOpcion;
    }

    public async Task<int> ActualizarAsync(long idOpcion, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual.
        const string sqlConsultar = "CALL catedras.sp_consultar_opcion_pregunta(@idOpcion, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idOpcion });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = LectorJsonSp.Fila<OpcionPregunta>(actualJson)!;

        // 2) Mezclar. 'fk_pregunta' no se cambia (igual que la lista blanca de antes).
        string etiqueta = datos.TryGetValue("etiqueta", out var vEtiqueta) ? (string)vEtiqueta! : actual.Etiqueta;
        short? valor = datos.TryGetValue("valor", out var vValor) ? (short?)vValor : actual.Valor;
        short orden = datos.TryGetValue("orden", out var vOrden) ? (short)vOrden! : actual.Orden;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_opcion_pregunta(
                @idOpcion, @etiqueta, @valor, @orden, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(sqlActualizar,
            new { idOpcion, etiqueta, valor, orden });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idOpcion)
    {
        const string sql = "CALL catedras.sp_eliminar_opcion_pregunta(@idOpcion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idOpcion });

        // FÍSICO. Opción ya elegida en una respuesta -> RESTRICT -> NpgsqlException.
        return LectorJsonSp.Bandera(json, "eliminado") ? 1 : 0;
    }
}
