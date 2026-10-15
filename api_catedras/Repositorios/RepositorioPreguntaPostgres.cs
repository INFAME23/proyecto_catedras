// ==========================================================================
//  Repositorios/RepositorioPreguntaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar, Consultar, Actualizar y Eliminar pasan por los procedimientos
//  de 'db/postgres/migraciones/038_sp_pregunta.sql' (molde: sede).
//
//  CREAR NO CAMBIÓ: sigue siendo UN 'CALL' a 'sp_guardar_pregunta'
//  (migración 004): maestro + opciones iniciales en una sola transacción
//  (Guía IA · Regla 1). El INOUT 'p_id_pregunta' sale con el id nuevo.
//
//  ACTUALIZAR: un solo 'sp_actualizar_pregunta' con todas las columnas que
//  la API deja cambiar; el PATCH se mezcla aquí (consultar + mezclar +
//  llamar completo), copia de RepositorioSedePostgres.ActualizarAsync.
//
//  ELIMINAR: respuesta_item.fk_pregunta es RESTRICT y
//  opcion_pregunta.fk_pregunta es CASCADE. Si hay respuestas, el DELETE
//  completo falla y la cascada a las opciones no llega a ocurrir.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;   // JsonSerializer: convierte la lista de opciones en texto JSON

/// <summary>Acceso a 'catedras.pregunta' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioPreguntaPostgres : IRepositorioPregunta
{
    private readonly string _cadenaConexion;

    public RepositorioPreguntaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Pregunta>> ObtenerTodosAsync(long? fkEncuesta, int limite)
    {
        const string sql = "CALL catedras.sp_listar_pregunta(@fkEncuesta::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkEncuesta, limite });

        return LectorJsonSp.Lista<Pregunta>(json);
    }

    public async Task<Pregunta?> ObtenerPorIdAsync(long idPregunta)
    {
        const string sql = "CALL catedras.sp_consultar_pregunta(@idPregunta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idPregunta });

        return LectorJsonSp.Fila<Pregunta>(json);
    }

    public async Task<long> CrearAsync(Pregunta pregunta, IReadOnlyList<OpcionPregunta> opcionesIniciales)
    {
        // SIN CAMBIOS (migración 004). 'CAST(@Opciones AS jsonb)': Dapper
        // manda el JSON como TEXTO; el CAST lo convierte en jsonb. El último
        // argumento (NULL) es el INOUT 'p_id_pregunta'.
        const string sql = @"
            CALL catedras.sp_guardar_pregunta(
                @FkEncuesta, @Enunciado, @Orden, @FkTipoPregunta,
                @Obligatoria, @ValorMin, @ValorMax,
                CAST(@Opciones AS jsonb), NULL);";

        string? opcionesJson = opcionesIniciales.Count == 0
            ? null    // sin opciones -> p_opciones NULL -> el SP solo crea la pregunta
            : JsonSerializer.Serialize(opcionesIniciales.Select(o => new
            {
                etiqueta = o.Etiqueta,
                valor = o.Valor,
                orden = o.Orden,
            }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

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
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual.
        const string sqlConsultar = "CALL catedras.sp_consultar_pregunta(@idPregunta, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idPregunta });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = LectorJsonSp.Fila<Pregunta>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual. 'fk_encuesta' no
        //    está (tampoco estaba en la lista blanca de antes).
        string enunciado = datos.TryGetValue("enunciado", out var vEnunciado) ? (string)vEnunciado! : actual.Enunciado;
        short orden = datos.TryGetValue("orden", out var vOrden) ? (short)vOrden! : actual.Orden;
        string fkTipoPregunta = datos.TryGetValue("fk_tipo_pregunta", out var vTipo) ? (string)vTipo! : actual.FkTipoPregunta;
        bool obligatoria = datos.TryGetValue("obligatoria", out var vObligatoria) ? (bool)vObligatoria! : actual.Obligatoria;
        short? valorMin = datos.TryGetValue("valor_min", out var vMin) ? (short?)vMin : actual.ValorMin;
        short? valorMax = datos.TryGetValue("valor_max", out var vMax) ? (short?)vMax : actual.ValorMax;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_pregunta(
                @idPregunta, @enunciado, @orden, @fkTipoPregunta,
                @obligatoria, @valorMin, @valorMax, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(sqlActualizar,
            new { idPregunta, enunciado, orden, fkTipoPregunta, obligatoria, valorMin, valorMax });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idPregunta)
    {
        const string sql = "CALL catedras.sp_eliminar_pregunta(@idPregunta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idPregunta });

        // FÍSICO ('pregunta' no tiene 'activo'). Con respuestas -> RESTRICT
        // -> NpgsqlException, igual que antes.
        return LectorJsonSp.Bandera(json, "eliminado") ? 1 : 0;
    }
}
