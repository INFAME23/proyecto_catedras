// ==========================================================================
//  Repositorios/RepositorioLoteCargaAsistentePostgres.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioLoteCargaAsistente para PostgreSQL con Dapper.
//
//  El CIERRE es el segundo maestro-detalle de la v3 (Guía IA, Regla 1):
//  un 'CALL' a 'sp_guardar_lote_carga' que actualiza los contadores del
//  lote e inserta sus novedades en la misma transacción (ADR D15).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.lote_carga_asistente' en PostgreSQL mediante Dapper.</summary>
public class RepositorioLoteCargaAsistentePostgres : IRepositorioLoteCargaAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioLoteCargaAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<LoteCargaAsistente>> ObtenerTodosAsync(int limite)
    {
        // Del más reciente al más antiguo: lo útil es ver la última carga.
        const string sql = @"
            SELECT id_lote_carga, nombre_archivo, cargado_en, fk_usuario,
                   filas_leidas, filas_aceptadas, filas_rechazadas, observaciones
            FROM catedras.lote_carga_asistente
            ORDER BY id_lote_carga DESC
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<LoteCargaAsistente>(sql, new { limite });
        return filas.ToList();
    }

    public async Task<LoteCargaAsistente?> ObtenerPorIdAsync(long idLoteCarga)
    {
        const string sql = @"
            SELECT id_lote_carga, nombre_archivo, cargado_en, fk_usuario,
                   filas_leidas, filas_aceptadas, filas_rechazadas, observaciones
            FROM catedras.lote_carga_asistente
            WHERE id_lote_carga = @idLoteCarga;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<LoteCargaAsistente>(sql, new { idLoteCarga });
    }

    public async Task<(long IdLoteCarga, DateTime CargadoEn)> CrearAsync(LoteCargaAsistente lote)
    {
        // Los 3 contadores NO se mandan: nacen en 0 (DEFAULT de la columna).
        // 'fk_usuario' viaja null en v3 (salvedad de la FK circular, mapa §3).
        const string sql = @"
            INSERT INTO catedras.lote_carga_asistente (nombre_archivo, fk_usuario)
            VALUES (@NombreArchivo, @FkUsuario)
            RETURNING id_lote_carga, cargado_en;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleAsync<(long, DateTime)>(sql, lote);
    }

    public async Task CerrarAsync(LoteCargaAsistente lote, IReadOnlyList<NovedadCarga> novedades)
    {
        // UN 'CALL': contadores (maestro) + novedades (detalle), todo o nada.
        const string sql = @"
            CALL catedras.sp_guardar_lote_carga(
                @IdLoteCarga, @FilasLeidas, @FilasAceptadas, @FilasRechazadas,
                @Observaciones, CAST(@Novedades AS jsonb));";

        // Solo viajan las columnas que lee el SP: 'fk_lote_carga' la fija
        // el SP con el lote que se cierra; 'id_novedad' lo genera la BD.
        string? novedadesJson = novedades.Count == 0 ? null
            : JsonSerializer.Serialize(novedades.Select(n => new
            {
                numero_fila = n.NumeroFila,
                contenido_crudo = n.ContenidoCrudo,
                motivo = n.Motivo,
            }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Contadores que no cuadran (CHECK chk_lote_carga_filas) o una
        // novedad inválida -> NpgsqlException y el CALL se revierte entero.
        await conexion.ExecuteAsync(sql, new
        {
            lote.IdLoteCarga,
            lote.FilasLeidas,
            lote.FilasAceptadas,
            lote.FilasRechazadas,
            lote.Observaciones,
            Novedades = novedadesJson,
        });
    }
}
