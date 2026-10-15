// ==========================================================================
//  Repositorios/RepositorioRespuestaEncuestaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Listar y Consultar pasan por los procedimientos de
//  'db/postgres/migraciones/013_sp_respuesta_encuesta.sql' (molde: sede).
//
//  RESPONDER NO CAMBIÓ: sigue llamando a fn_responder_encuesta_api (ADR
//  D30, D31), que hace CALL a sp_responder_encuesta: maestro + ítems +
//  sp_cerrar_encuesta en una sola transacción.
//
//  ⚠️ La BD tiene además sp_actualizar_ / sp_eliminar_respuesta_encuesta
//  (criterio al pie de la letra), que se NIEGAN sobre una respuesta
//  cerrada. NO se exponen: una respuesta ya dada es un hecho auditable
//  (D31/D32) e IRepositorioRespuestaEncuesta no los pide.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.respuesta_encuesta' en PostgreSQL, vía procedimientos y funciones.</summary>
public class RepositorioRespuestaEncuestaPostgres : IRepositorioRespuestaEncuesta
{
    private readonly string _cadenaConexion;

    public RepositorioRespuestaEncuestaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<RespuestaEncuesta>> ObtenerTodosAsync(long? fkEncuesta, int limite)
    {
        const string sql = "CALL catedras.sp_listar_respuesta_encuesta(@fkEncuesta::bigint, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkEncuesta, limite });

        return LectorJsonSp.Lista<RespuestaEncuesta>(json);
    }

    public async Task<RespuestaEncuesta?> ObtenerPorIdAsync(long idRespuesta)
    {
        const string sql = "CALL catedras.sp_consultar_respuesta_encuesta(@idRespuesta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idRespuesta });

        return LectorJsonSp.Fila<RespuestaEncuesta>(json);
    }

    public async Task<long> ResponderAsync(long fkRegistro, IReadOnlyList<ItemRespuesta> respuestas)
    {
        // SIN CAMBIOS: función de negocio del script provisto (ADR D30).
        const string sql = @"
            SELECT * FROM catedras.fn_responder_encuesta_api(
                @FkRegistro, CAST(@Respuestas AS jsonb));";

        // Nombres EXACTOS del contrato (version6/6_contracts.md §3).
        string respuestasJson = JsonSerializer.Serialize(respuestas.Select(r => new
        {
            pregunta = r.Pregunta,
            numero = r.Numero,
            texto = r.Texto,
            opcion = r.Opcion,
        }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        return await conexion.ExecuteScalarAsync<long>(sql,
            new { FkRegistro = fkRegistro, Respuestas = respuestasJson });
    }
}
