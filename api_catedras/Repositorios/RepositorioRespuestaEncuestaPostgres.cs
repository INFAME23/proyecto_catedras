// ==========================================================================
//  Repositorios/RepositorioRespuestaEncuestaPostgres.cs  ·  Cátedras · v6
// --------------------------------------------------------------------------
//  IRepositorioRespuestaEncuesta para PostgreSQL con Dapper.
//
//  ResponderAsync NO es un INSERT: llama a la función
//  'fn_responder_encuesta_api' (ADR D30, D31), que YA existía desde la v1
//  del script provisto — igual mecánica que RepositorioEnlaceRegistroPostgres
//  (v5) con 'fn_emitir_enlace_api': 'SELECT * FROM' sobre una FUNCTION que
//  devuelve un escalar, 'ExecuteScalarAsync<long>' lo lee.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.respuesta_encuesta' en PostgreSQL mediante Dapper.</summary>
public class RepositorioRespuestaEncuestaPostgres : IRepositorioRespuestaEncuesta
{
    private readonly string _cadenaConexion;

    private const string Columnas =
        "id_respuesta, fk_registro, fk_encuesta, respondida_en, completa";

    public RepositorioRespuestaEncuestaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RespuestaEncuesta>> ObtenerTodosAsync(long? fkEncuesta, int limite)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.respuesta_encuesta
            WHERE (@fkEncuesta::bigint IS NULL OR fk_encuesta = @fkEncuesta)
            ORDER BY id_respuesta
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<RespuestaEncuesta>(sql, new { fkEncuesta, limite });
        return filas.ToList();
    }

    public async Task<RespuestaEncuesta?> ObtenerPorIdAsync(long idRespuesta)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.respuesta_encuesta
            WHERE id_respuesta = @idRespuesta;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<RespuestaEncuesta>(sql, new { idRespuesta });
    }

    public async Task<long> ResponderAsync(long fkRegistro, IReadOnlyList<ItemRespuesta> respuestas)
    {
        // La función ya existe desde la v1 (ADR D30): inserta el maestro,
        // TODOS los 'respuesta_item' del detalle y además llama ella misma
        // a 'sp_cerrar_encuesta', todo dentro de la misma transacción SQL
        // del 'SELECT'. Si falta una pregunta obligatoria, el RAISE
        // EXCEPTION de 'sp_cerrar_encuesta' revierte TODO: ni el maestro
        // (ya insertado unas líneas antes dentro de la función) queda.
        const string sql = @"
            SELECT * FROM catedras.fn_responder_encuesta_api(
                @FkRegistro, CAST(@Respuestas AS jsonb));";

        // El "formato" que lee la función (e ->> 'pregunta', ...) es un
        // detalle del MOTOR: se arma aquí, no en el servicio. Los nombres
        // de propiedad son EXACTOS a los del contrato (version6/6_contracts.md
        // §3): 'pregunta', 'numero', 'texto', 'opcion'.
        string respuestasJson = JsonSerializer.Serialize(respuestas.Select(r => new
        {
            pregunta = r.Pregunta,
            numero = r.Numero,
            texto = r.Texto,
            opcion = r.Opcion,
        }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Lista vacía (R29), registro sin 'fk_encuesta' asignado, registro
        // ya respondido (uq_respuesta_registro) o falta una obligatoria
        // (RN-26) -> NpgsqlException, y el motor revierte el CALL completo.
        return await conexion.ExecuteScalarAsync<long>(sql,
            new { FkRegistro = fkRegistro, Respuestas = respuestasJson });
    }
}
