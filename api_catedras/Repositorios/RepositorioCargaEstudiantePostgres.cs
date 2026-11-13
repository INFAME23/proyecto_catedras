// ==========================================================================
//  Repositorios/RepositorioCargaEstudiantePostgres.cs  ·  Subversión v7.2 · RF-S8
// --------------------------------------------------------------------------
//  Solo arma el 'CALL' a sp_cargar_estudiante (migración 064) y lee su
//  respuesta. El CALL devuelve UNA fila con los dos parámetros INOUT
//  (p_id_asistente, p_accion).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Carga de estudiantes en PostgreSQL, vía sp_cargar_estudiante.</summary>
public class RepositorioCargaEstudiantePostgres : IRepositorioCargaEstudiante
{
    private readonly string _cadenaConexion;

    public RepositorioCargaEstudiantePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<ResultadoCargaEstudiante> CargarAsync(
        string idAsis, string documento, string programa, string periodo, long? fkLoteCarga)
    {
        // '::character(6)' y '::bigint' le dicen a PostgreSQL el tipo exacto de los parámetros
        // (el periodo es char(6); el lote puede ser NULL). Los dos NULL finales son los INOUT.
        const string sql = @"
            CALL catedras.sp_cargar_estudiante(
                @idAsis, @documento, @programa, @periodo::character(6), @fkLoteCarga::bigint, NULL, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var fila = await conexion.QuerySingleAsync(sql, new { idAsis, documento, programa, periodo, fkLoteCarga });

        var columnas = (IDictionary<string, object?>)fila;
        return new ResultadoCargaEstudiante
        {
            IdAsistente = (long)columnas["p_id_asistente"]!,
            Accion = (string)columnas["p_accion"]!,
        };
    }
}
