// ==========================================================================
//  Repositorios/RepositorioLoteCargaAsistentePostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioLoteCargaAsistente para PostgreSQL. Todo pasa por
//  procedimientos almacenados:
//     ObtenerTodosAsync -> sp_listar_lote_carga_asistente     (029)
//     ObtenerPorIdAsync -> sp_consultar_lote_carga_asistente  (029)
//     CrearAsync (abrir)-> sp_crear_lote_carga_asistente      (029, NUEVO)
//     CerrarAsync       -> sp_guardar_lote_carga              (003, SIN CAMBIOS)
//  El CIERRE ya era un procedimiento desde la v3 (maestro-detalle:
//  contadores + novedades, ADR D15) y no se toca. Lo que pasó a
//  procedimiento en el retrofit es el ABRIR, que antes era un INSERT suelto.
//
//  sp_actualizar_ y sp_eliminar_lote_carga_asistente existen en la BD pero
//  este contrato no los usa: RF117, el lote es la bitácora de una ejecución
//  ya ocurrida (sin PUT/PATCH/DELETE).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.lote_carga_asistente' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioLoteCargaAsistentePostgres : IRepositorioLoteCargaAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioLoteCargaAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<LoteCargaAsistente>> ObtenerTodosAsync(int limite)
    {
        // Del más reciente al más antiguo (lo ordena el procedimiento).
        const string sql = "CALL catedras.sp_listar_lote_carga_asistente(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // 'cerrado' NO viene en el JSON (no es columna): LoteCargaAsistente
        // lo recalcula solo a partir de los contadores, como antes.
        return JsonSerializer.Deserialize<List<LoteCargaAsistente>>(json!) ?? [];
    }

    public async Task<LoteCargaAsistente?> ObtenerPorIdAsync(long idLoteCarga)
    {
        const string sql = "CALL catedras.sp_consultar_lote_carga_asistente(@idLoteCarga, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idLoteCarga });

        return json is null ? null : JsonSerializer.Deserialize<LoteCargaAsistente>(json);
    }

    public async Task<(long IdLoteCarga, DateTime CargadoEn)> CrearAsync(LoteCargaAsistente lote)
    {
        // Los 3 contadores NO se mandan: nacen en 0 (DEFAULT de la columna).
        // 'fk_usuario' viaja null en v3 (salvedad de la FK circular, mapa §3).
        const string sql = "CALL catedras.sp_crear_lote_carga_asistente(@NombreArchivo, @FkUsuario, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { lote.NombreArchivo, lote.FkUsuario });

        var guardado = JsonSerializer.Deserialize<LoteCargaAsistente>(json!)!;
        return (guardado.IdLoteCarga, guardado.CargadoEn);
    }

    public async Task CerrarAsync(LoteCargaAsistente lote, IReadOnlyList<NovedadCarga> novedades)
    {
        // SIN CAMBIOS en el retrofit. UN 'CALL': contadores (maestro) +
        // novedades (detalle), todo o nada.
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
