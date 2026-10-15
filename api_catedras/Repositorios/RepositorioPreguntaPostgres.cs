// ==========================================================================
//  Repositorios/RepositorioPreguntaPostgres.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  IRepositorioPregunta para PostgreSQL con Dapper. SQL a mano y
//  parametrizado; una conexión por operación (Artículo 2 de la constitución).
//
//  CrearAsync: UN 'CALL' al SP 'sp_guardar_pregunta' (maestro + opciones
//  iniciales en una sola transacción, Guía IA · Regla 1). Mismo patrón que
//  RepositorioAsistentePostgres (v3): el parámetro INOUT se manda como
//  'NULL' en el SQL, y 'QuerySingleAsync<long>' lee la única fila que
//  devuelve el CALL (el id nuevo).
//
//  EliminarAsync: 'respuesta_item.fk_pregunta' es RESTRICT y
//  'opcion_pregunta.fk_pregunta' es CASCADE. PostgreSQL evalúa ambas en la
//  MISMA sentencia: si hay respuestas, el DELETE completo falla y la
//  cascada hacia las opciones NO llega a ocurrir (ACID · A).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;   // JsonSerializer: convierte la lista de opciones en texto JSON

/// <summary>Acceso a 'catedras.pregunta' en PostgreSQL mediante Dapper.</summary>
public class RepositorioPreguntaPostgres : IRepositorioPregunta
{
    private readonly string _cadenaConexion;

    // [ACID · C] Lista blanca del UPDATE. 'id_pregunta' (la PK) no se actualiza.
    private static readonly HashSet<string> ColumnasActualizables =
        new() { "enunciado", "orden", "fk_tipo_pregunta", "obligatoria", "valor_min", "valor_max" };

    public RepositorioPreguntaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Pregunta>> ObtenerTodosAsync(long? fkEncuesta, int limite)
    {
        // '@fkEncuesta::bigint IS NULL' deja pasar TODAS las filas cuando
        // el filtro no llegó (mismo molde que RegistroAsistencia, v5).
        const string sql = @"
            SELECT id_pregunta, fk_encuesta, enunciado, orden, fk_tipo_pregunta,
                   obligatoria, valor_min, valor_max
            FROM catedras.pregunta
            WHERE (@fkEncuesta::bigint IS NULL OR fk_encuesta = @fkEncuesta)
            ORDER BY fk_encuesta, orden
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Pregunta>(sql, new { fkEncuesta, limite });
        return filas.ToList();
    }

    public async Task<Pregunta?> ObtenerPorIdAsync(long idPregunta)
    {
        const string sql = @"
            SELECT id_pregunta, fk_encuesta, enunciado, orden, fk_tipo_pregunta,
                   obligatoria, valor_min, valor_max
            FROM catedras.pregunta
            WHERE id_pregunta = @idPregunta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Pregunta>(sql, new { idPregunta });
    }

    public async Task<long> CrearAsync(Pregunta pregunta, IReadOnlyList<OpcionPregunta> opcionesIniciales)
    {
        // Guía IA, Regla 1: UN 'CALL' al SP, no dos INSERT sueltos desde
        // C#. 'CAST(@Opciones AS jsonb)': Dapper manda el JSON como TEXTO;
        // el CAST lo convierte en jsonb, el tipo del parámetro 'p_opciones'.
        // El último argumento (NULL) es el INOUT 'p_id_pregunta': entra
        // vacío y sale con el id generado.
        const string sql = @"
            CALL catedras.sp_guardar_pregunta(
                @FkEncuesta, @Enunciado, @Orden, @FkTipoPregunta,
                @Obligatoria, @ValorMin, @ValorMax,
                CAST(@Opciones AS jsonb), NULL);";

        // El "formato" que lee el SP (e ->> 'etiqueta', ...) es un detalle
        // del MOTOR, por eso el JSON se arma aquí y no en el servicio.
        // Solo viajan las columnas que pone el cliente: 'fk_pregunta' la
        // fija el SP con el id del maestro recién creado.
        string? opcionesJson = opcionesIniciales.Count == 0
            ? null    // sin opciones -> p_opciones NULL -> el SP solo crea la pregunta
            : JsonSerializer.Serialize(opcionesIniciales.Select(o => new
            {
                etiqueta = o.Etiqueta,
                valor = o.Valor,
                orden = o.Orden,
            }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // '(fk_encuesta, orden)' duplicado, tipo de pregunta inexistente o
        // un 'orden' de opción repetido -> NpgsqlException, y el motor
        // revierte el CALL completo (ni la pregunta queda creada).
        // 'CALL' con un parámetro INOUT devuelve UNA fila con ese
        // parámetro: QuerySingleAsync<long> lee su único valor (el id nuevo).
        return await conexion.QuerySingleAsync<long>(sql, new
        {
            pregunta.FkEncuesta,
            pregunta.Enunciado,
            pregunta.Orden,
            pregunta.FkTipoPregunta,
            pregunta.Obligatoria,
            pregunta.ValorMin,
            pregunta.ValorMax,
            Opciones = opcionesJson,
        });
    }

    public async Task<int> ActualizarAsync(long idPregunta, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idPregunta);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        var sql =
            $"UPDATE catedras.pregunta SET {string.Join(", ", asignaciones)} " +
            "WHERE id_pregunta = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idPregunta)
    {
        const string sql = "DELETE FROM catedras.pregunta WHERE id_pregunta = @idPregunta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // RESTRICT (respuesta_item) gana sobre CASCADE (opcion_pregunta):
        // una pregunta con respuestas de ejemplo no se puede borrar, y sus
        // opciones siguen intactas porque la cascada no llega a ocurrir.
        return await conexion.ExecuteAsync(sql, new { idPregunta });
    }
}
