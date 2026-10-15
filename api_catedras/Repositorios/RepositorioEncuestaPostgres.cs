// ==========================================================================
//  Repositorios/RepositorioEncuestaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/015_sp_encuesta.sql', con el MISMO molde de
//  RepositorioSedePostgres.cs: esta clase solo arma el 'CALL', manda los
//  parámetros y convierte el JSON que devuelve el procedimiento.
//
//  Diferencias con sede: el id es IDENTITY (lo genera la BD), así que
//  'CrearAsync' lee 'id_encuesta' del JSON que devuelve 'sp_crear_encuesta';
//  y 'encuesta' tiene 'activa', no 'activo' -> el eliminar es FÍSICO.
//
//  'IRepositorioEncuesta', 'ServicioEncuesta', 'EncuestaController' y las
//  'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.encuesta' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioEncuestaPostgres : IRepositorioEncuesta
{
    private readonly string _cadenaConexion;

    public RepositorioEncuestaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Encuesta>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_encuesta(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<Encuesta>>(json!) ?? [];
    }

    public async Task<Encuesta?> ObtenerPorIdAsync(long idEncuesta)
    {
        const string sql = "CALL catedras.sp_consultar_encuesta(@idEncuesta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idEncuesta });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<Encuesta>(json);
    }

    public async Task<long> CrearAsync(Encuesta encuesta,
        IReadOnlyList<(Pregunta Pregunta, IReadOnlyList<OpcionPregunta> Opciones)> preguntasIniciales)
    {
        // Maestro-detalle (Guía IA, Regla 1 · migración 043): la encuesta, sus
        // preguntas y las opciones de cada pregunta en UN solo CALL.
        // 'sp_guardar_encuesta' reutiliza 'sp_guardar_pregunta' (004) para
        // cada pregunta. Sin preguntas (p_preguntas NULL) hace lo mismo que
        // 'sp_crear_encuesta'. El último argumento (NULL) es el INOUT
        // 'p_id_encuesta': el CALL responde una fila con ese valor.
        // '::date' explícito: Dapper no conoce DateOnly y lo manda como
        // 'timestamp'; un CALL resuelve el procedimiento por TIPOS y
        // 'timestamp -> date' no es implícito (sin el cast -> 42883).
        const string sql = @"
            CALL catedras.sp_guardar_encuesta(
                @Nombre, @Version, @VigenteDesde::date, @VigenteHasta::date, @Activa,
                CAST(@Preguntas AS jsonb), NULL);";

        string? preguntasJson = preguntasIniciales.Count == 0
            ? null
            : JsonSerializer.Serialize(preguntasIniciales.Select(p => new
            {
                enunciado = p.Pregunta.Enunciado,
                orden = p.Pregunta.Orden,
                fk_tipo_pregunta = p.Pregunta.FkTipoPregunta,
                obligatoria = p.Pregunta.Obligatoria,
                valor_min = p.Pregunta.ValorMin,
                valor_max = p.Pregunta.ValorMax,
                opciones = p.Opciones.Count == 0
                    ? null
                    : p.Opciones.Select(o => new { etiqueta = o.Etiqueta, valor = o.Valor, orden = o.Orden }).ToList(),
            }));

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        return await conexion.QuerySingleAsync<long>(sql, new
        {
            encuesta.Nombre,
            encuesta.Version,
            encuesta.VigenteDesde,
            encuesta.VigenteHasta,
            encuesta.Activa,
            Preguntas = preguntasJson,
        });
    }

    public async Task<int> ActualizarAsync(long idEncuesta, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_encuesta(@idEncuesta, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idEncuesta });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<Encuesta>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        short version = datos.TryGetValue("version", out var vVersion) ? (short)vVersion! : actual.Version;
        DateOnly vigenteDesde = datos.TryGetValue("vigente_desde", out var vDesde) ? (DateOnly)vDesde! : actual.VigenteDesde;
        DateOnly? vigenteHasta = datos.TryGetValue("vigente_hasta", out var vHasta) ? (DateOnly?)vHasta : actual.VigenteHasta;
        bool activa = datos.TryGetValue("activa", out var vActiva) ? (bool)vActiva! : actual.Activa;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_encuesta(
                @idEncuesta, @nombre, @version, @vigenteDesde::date, @vigenteHasta::date, @activa, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idEncuesta, nombre, version, vigenteDesde, vigenteHasta, activa });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idEncuesta)
    {
        const string sql = "CALL catedras.sp_eliminar_encuesta(@idEncuesta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idEncuesta });

        // {"eliminado": true|false}. 'encuesta' NO tiene 'activo': DELETE físico.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
