// ==========================================================================
//  Repositorios/RepositorioSesionPostgres.cs  ·  Proyecto Cátedras · v4
// --------------------------------------------------------------------------
//  IRepositorioSesion para PostgreSQL con Dapper. SQL a mano y
//  parametrizado; una conexión por operación.
//
//  Lo nuevo de la v4 (3_plan.md §4.1, ADR D20): el INSERT manda
//  'numero_reunion' TAL CUAL llegó (0 si el cliente no lo fijó). El trigger
//  'fn_trg_sesion_consecutivo' (BEFORE INSERT) lo reemplaza por
//  MAX(numero_reunion) + 1 de esa cátedra ANTES de guardar la fila, y el
//  'RETURNING' nos devuelve el valor DEFINITIVO. Este archivo nunca calcula
//  el número: no hay ningún 'SELECT MAX' aquí, y es a propósito.
//
//  Las columnas 'inicio' y 'fin' son TIMESTAMPTZ. Llegan del servicio como
//  DateTime en UTC (Kind = Utc), que Npgsql escribe sin conversión (ADR D21).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Acceso a 'catedras.sesion' en PostgreSQL mediante Dapper.</summary>
public class RepositorioSesionPostgres : IRepositorioSesion
{
    private readonly string _cadenaConexion;

    // Columnas de lectura (las 16 de la tabla), escritas una sola vez.
    private const string Columnas = @"
            id_sesion, fk_catedra, numero_reunion, numero_reunion_asis, titulo,
            inicio, fin, fk_modalidad, fk_sede, fk_periodo, fk_encuesta,
            lugar, enlace_virtual, cupo, estado, creado_en";

    // [ACID · C] Lista blanca del UPDATE. NO están: 'id_sesion' (PK) ni
    // 'creado_en' (la pone la BD).
    private static readonly HashSet<string> ColumnasActualizables = new()
    {
        "fk_catedra", "numero_reunion", "numero_reunion_asis", "titulo", "inicio", "fin",
        "fk_modalidad", "fk_sede", "fk_periodo", "fk_encuesta", "lugar", "enlace_virtual",
        "cupo", "estado",
    };

    public RepositorioSesionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Sesion>> ObtenerTodosAsync(long? fkCatedra, int limite)
    {
        // '@fkCatedra::bigint IS NULL OR ...': sin filtro, la condición es
        // verdadera para todas las filas; con filtro, solo las de esa cátedra.
        // El '::bigint' le dice a PostgreSQL el tipo del parámetro incluso
        // cuando llega NULL (mismo truco que VinculacionAsistente en la v3).
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.sesion
            WHERE @fkCatedra::bigint IS NULL OR fk_catedra = @fkCatedra
            ORDER BY fk_catedra, numero_reunion
            LIMIT @limite;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Sesion>(sql, new { fkCatedra, limite });
        return filas.ToList();
    }

    public async Task<Sesion?> ObtenerPorIdAsync(long idSesion)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.sesion
            WHERE id_sesion = @idSesion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Sesion>(sql, new { idSesion });
    }

    public async Task<(long IdSesion, short NumeroReunion, DateTime CreadoEn)> CrearAsync(Sesion sesion)
    {
        // Ni id_sesion ni creado_en se envían: los pone la BD.
        // @NumeroReunion puede valer 0: el trigger lo cambia por el
        // consecutivo de la cátedra ANTES de que la fila se guarde.
        const string sql = @"
            INSERT INTO catedras.sesion
                (fk_catedra, numero_reunion, numero_reunion_asis, titulo,
                 inicio, fin, fk_modalidad, fk_sede, fk_periodo, fk_encuesta,
                 lugar, enlace_virtual, cupo, estado)
            VALUES (@FkCatedra, @NumeroReunion, @NumeroReunionAsis, @Titulo,
                    @Inicio, @Fin, @FkModalidad, @FkSede, @FkPeriodo, @FkEncuesta,
                    @Lugar, @EnlaceVirtual, @Cupo, @Estado)
            RETURNING id_sesion, numero_reunion, creado_en;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Una fila con tres columnas -> tupla (long, short, DateTime), en ese
        // orden. 'numero_reunion' llega YA calculado por el trigger.
        return await conexion.QuerySingleAsync<(long, short, DateTime)>(sql, sesion);
    }

    public async Task<int> ActualizarAsync(long idSesion, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idSesion);

        foreach (var (columna, valor) in datos)
        {
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        // OJO: el trigger de numeración es BEFORE INSERT, no se dispara en un
        // UPDATE. Por eso las peticiones PUT/PATCH exigen numero_reunion >= 1.
        var sql =
            $"UPDATE catedras.sesion SET {string.Join(", ", asignaciones)} " +
            "WHERE id_sesion = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idSesion)
    {
        const string sql = "DELETE FROM catedras.sesion WHERE id_sesion = @idSesion;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // [ACID · A] Un DELETE es UNA transacción implícita: si alguna fila de
        // 'registro_asistencia' o 'lote_migracion' (RESTRICT) apunta a esta
        // sesión, el motor lanza el error y TAMPOCO borra las ponencias ni los
        // enlaces que habría arrastrado en CASCADA. Todo o nada.
        return await conexion.ExecuteAsync(sql, new { idSesion });
    }
}
