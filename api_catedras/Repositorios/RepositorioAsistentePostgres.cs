// ==========================================================================
//  Repositorios/RepositorioAsistentePostgres.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  IRepositorioAsistente para PostgreSQL con Dapper. SQL a mano y
//  parametrizado; una conexión por operación.
//
//  Tres puntos nuevos de la v3:
//   1. BÚSQUEDA EN SQL (ADR D18): la tabla tiene 14.811 filas de ejemplo.
//      El texto 'q' viaja como PARÁMETRO a un 'ILIKE' (comparación sin
//      distinguir mayúsculas); nunca se pega al SQL.
//   2. PAGINACIÓN: 'LIMIT @limite OFFSET @desplazamiento' devuelve solo la
//      página pedida.
//   3. MAESTRO-DETALLE (Guía IA, Regla 1): el CREATE es UN 'CALL' al SP
//      'sp_guardar_asistente' con 4 parámetros jsonb. El SP devuelve el id
//      nuevo en su parámetro INOUT; Dapper lo lee como la columna de una fila.
//
//  El DELETE no simula nada: los 4 detalles se borran solos (CASCADE) y
//  'usuario'/'ponencia'/'registro_asistencia' lo bloquean (RESTRICT -> 500).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;   // JsonSerializer: convierte cada lista de detalles en texto JSON

/// <summary>Acceso a 'catedras.asistente' en PostgreSQL mediante Dapper.</summary>
public class RepositorioAsistentePostgres : IRepositorioAsistente
{
    private readonly string _cadenaConexion;

    // Columnas que devuelve toda lectura (las 12 de la tabla).
    private const string Columnas = @"
            id_asistente, id_asis, nombres, apellidos, nombre_completo_asis,
            correo_institucional, correo_personal, celular,
            es_externo, activo, creado_en, fk_lote_carga";

    // [ACID · C] Lista blanca del UPDATE: solo estas columnas se pueden
    // cambiar. 'id_asistente', 'creado_en' y 'fk_lote_carga' NO.
    private static readonly HashSet<string> ColumnasActualizables = new()
    {
        "id_asis", "nombres", "apellidos", "nombre_completo_asis",
        "correo_institucional", "correo_personal", "celular", "es_externo", "activo",
    };

    public RepositorioAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Asistente>> ObtenerTodosAsync(string? q, int limite, int desplazamiento)
    {
        // '@patron IS NULL OR ...': si no hay búsqueda, la condición es
        // verdadera para todas las filas; si la hay, se aplica el ILIKE.
        // Se busca en "nombres apellidos" juntos (así "Ana Garcia" encuentra
        // a Ana García) y en id_asis.
        // '::text' en '@patron' le dice a PostgreSQL el tipo del parámetro
        // incluso cuando llega NULL (si no, no sabría de qué tipo es).
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.asistente
            WHERE @patron::text IS NULL
               OR (nombres || ' ' || apellidos) ILIKE @patron
               OR id_asis ILIKE @patron
            ORDER BY id_asistente
            LIMIT @limite OFFSET @desplazamiento;";

        // Los '%' de ILIKE significan "cualquier texto antes/después".
        // 'string.IsNullOrWhiteSpace' = null, vacío o solo espacios -> sin filtro.
        string? patron = string.IsNullOrWhiteSpace(q) ? null : $"%{q.Trim()}%";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var filas = await conexion.QueryAsync<Asistente>(sql, new { patron, limite, desplazamiento });
        return filas.ToList();
    }

    public async Task<Asistente?> ObtenerPorIdAsync(long idAsistente)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM catedras.asistente
            WHERE id_asistente = @idAsistente;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.QuerySingleOrDefaultAsync<Asistente>(sql, new { idAsistente });
    }

    public async Task<long> CrearAsync(
        Asistente asistente,
        IReadOnlyList<DocumentoAsistente> documentos,
        IReadOnlyList<VinculacionAsistente> vinculaciones,
        IReadOnlyList<ProgramaAsistente> programas,
        ConsentimientoDatos? consentimiento)
    {
        // Guía IA, Regla 1: UN 'CALL' que guarda maestro + 4 detalles en la
        // misma transacción del motor. 'CAST(@X AS jsonb)': Dapper manda el
        // JSON como TEXTO y el CAST lo convierte al tipo del parámetro del
        // SP. El último argumento (NULL) es el INOUT 'p_id_asistente': entra
        // vacío y sale con el id generado.
        const string sql = @"
            CALL catedras.sp_guardar_asistente(
                @IdAsis, @Nombres, @Apellidos, @NombreCompletoAsis,
                @CorreoInstitucional, @CorreoPersonal, @Celular,
                @EsExterno, @Activo, @FkLoteCarga,
                CAST(@Documentos AS jsonb), CAST(@Vinculaciones AS jsonb),
                CAST(@Programas AS jsonb), CAST(@Consentimiento AS jsonb),
                NULL);";

        // El "formato" que lee el SP (e ->> 'numero', ...) es un detalle del
        // MOTOR, por eso el JSON se arma aquí y no en el servicio. Solo viajan
        // las columnas que pone el cliente: 'fk_asistente' la fija el SP.
        // Lista vacía -> NULL (contrato v2/v3: "no venía el detalle").
        string? documentosJson = documentos.Count == 0 ? null
            : JsonSerializer.Serialize(documentos.Select(d => new
            {
                fk_tipo_documento = d.FkTipoDocumento,
                numero = d.Numero,
                vigente = d.Vigente,
            }));

        string? vinculacionesJson = vinculaciones.Count == 0 ? null
            : JsonSerializer.Serialize(vinculaciones.Select(v => new
            {
                fk_tipo_vinculacion = v.FkTipoVinculacion,
                fecha_ini = v.FechaIni,   // DateOnly -> "2026-01-20"
            }));

        string? programasJson = programas.Count == 0 ? null
            : JsonSerializer.Serialize(programas.Select(p => new
            {
                fk_programa = p.FkPrograma,
                fk_periodo = p.FkPeriodo,
                estado = p.Estado,
            }));

        // El consentimiento es UN objeto (no un arreglo).
        string? consentimientoJson = consentimiento is null ? null
            : JsonSerializer.Serialize(new
            {
                version_politica = consentimiento.VersionPolitica,
                canal = consentimiento.Canal,
                ip = consentimiento.Ip,
            });

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 'CALL' con un parámetro INOUT devuelve UNA fila con ese parámetro:
        // QuerySingleAsync<long> lee su único valor (el id nuevo).
        // id_asis o correo institucional duplicado, un CHECK que no se
        // cumple o un detalle inválido -> NpgsqlException y el motor
        // revierte el CALL completo.
        return await conexion.QuerySingleAsync<long>(sql, new
        {
            asistente.IdAsis,
            asistente.Nombres,
            asistente.Apellidos,
            asistente.NombreCompletoAsis,
            asistente.CorreoInstitucional,
            asistente.CorreoPersonal,
            asistente.Celular,
            asistente.EsExterno,
            asistente.Activo,
            asistente.FkLoteCarga,
            Documentos = documentosJson,
            Vinculaciones = vinculacionesJson,
            Programas = programasJson,
            Consentimiento = consentimientoJson,
        });
    }

    public async Task<int> ActualizarAsync(long idAsistente, Dictionary<string, object?> datos)
    {
        var asignaciones = new List<string>();
        var parametros = new DynamicParameters();
        parametros.Add("id_clave", idAsistente);

        foreach (var (columna, valor) in datos)
        {
            // Solo columnas de la lista blanca: el nombre de columna SÍ se
            // pega al SQL, por eso nunca puede venir libre del cliente.
            if (!ColumnasActualizables.Contains(columna))
                continue;

            asignaciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        if (asignaciones.Count == 0)
            return 0;

        var sql =
            $"UPDATE catedras.asistente SET {string.Join(", ", asignaciones)} " +
            "WHERE id_asistente = @id_clave;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        return await conexion.ExecuteAsync(sql, parametros);
    }

    public async Task<int> EliminarAsync(long idAsistente)
    {
        const string sql = "DELETE FROM catedras.asistente WHERE id_asistente = @idAsistente;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // RESTRICT (usuario, ponencia, registro_asistencia) gana sobre
        // CASCADE (sus 4 detalles): si hay alguna fila que lo bloquee, la
        // sentencia completa falla y la cascada NO llega a ocurrir.
        return await conexion.ExecuteAsync(sql, new { idAsistente });
    }
}
