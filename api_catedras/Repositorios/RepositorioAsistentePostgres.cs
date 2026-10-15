// ==========================================================================
//  Repositorios/RepositorioAsistentePostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioAsistente para PostgreSQL. Los 5 verbos pasan por
//  procedimientos almacenados (PLAN_RETROFIT_SP.md, Fase 4):
//     Listar      -> sp_listar_asistente      (024_sp_asistente.sql)
//     Consultar   -> sp_consultar_asistente   (024)
//     Crear       -> sp_guardar_asistente     (002 — maestro + 4 detalles, SIN CAMBIOS)
//     Actualizar  -> sp_actualizar_asistente  (024)
//     Eliminar    -> sp_eliminar_asistente    (024 — LÓGICO: activo = false)
//  Esta clase ya no escribe SELECT/UPDATE/DELETE: arma el 'CALL', manda los
//  parámetros y convierte el JSON que devuelve el procedimiento en objetos.
//
//  LO QUE NO CAMBIÓ (SOLID · D): IRepositorioAsistente, ServicioAsistente,
//  AsistenteController y las Peticiones siguen idénticos.
//
//  LO QUE SÍ CAMBIÓ POR DENTRO (y se ve desde fuera):
//   · La búsqueda ILIKE ('%q%') y la paginación las hace el procedimiento.
//   · Listar devuelve solo activos (igual que 'sede').
//   · Eliminar ya no es DELETE: es activo = false. Ya no hay 500 por
//     RESTRICT (usuario/ponencia/registro_asistencia) ni CASCADE de los
//     4 detalles: el asistente queda inactivo con toda su historia.
//   · PATCH: consulta la fila, mezcla y llama al MISMO sp_actualizar (molde
//     de RepositorioSedePostgres.ActualizarAsync).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;   // lee el JSON de los SP y arma el JSON de los detalles

/// <summary>Acceso a 'catedras.asistente' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioAsistentePostgres : IRepositorioAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Asistente>> ObtenerTodosAsync(string? q, int limite, int desplazamiento)
    {
        // El texto 'q' viaja TAL CUAL (como parámetro, nunca pegado al SQL):
        // el procedimiento le quita espacios, lo envuelve en '%...%' y, si
        // queda vacío, no filtra (ADR D18).
        const string sql = "CALL catedras.sp_listar_asistente(@q, @limite, @desplazamiento, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { q, limite, desplazamiento });

        // Siempre un arreglo ('[]' si no hay filas): COALESCE(json_agg(...)).
        return JsonSerializer.Deserialize<List<Asistente>>(json!) ?? [];
    }

    public async Task<Asistente?> ObtenerPorIdAsync(long idAsistente)
    {
        const string sql = "CALL catedras.sp_consultar_asistente(@idAsistente, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idAsistente });

        // NULL = no existe (mismo contrato que QuerySingleOrDefaultAsync).
        return json is null ? null : JsonSerializer.Deserialize<Asistente>(json);
    }

    public async Task<long> CrearAsync(
        Asistente asistente,
        IReadOnlyList<DocumentoAsistente> documentos,
        IReadOnlyList<VinculacionAsistente> vinculaciones,
        IReadOnlyList<ProgramaAsistente> programas,
        ConsentimientoDatos? consentimiento)
    {
        // SIN CAMBIOS en el retrofit: el CREAR de 'asistente' ya era un
        // procedimiento desde la v3 (sp_guardar_asistente, maestro-detalle).
        //
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
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual: sp_actualizar_asistente reemplaza las 9
        //    columnas de negocio, así que un PATCH necesita completar lo que
        //    no llegó con lo que ya estaba guardado.
        const string sqlConsultar = "CALL catedras.sp_consultar_asistente(@idAsistente, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idAsistente });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<Asistente>(actualJson)!;

        // 2) Mezclar: lo que llegó en 'datos' PISA el valor actual. Un PUT
        //    manda las 9 claves (incluso las que van en null), así que un PUT
        //    sigue reemplazando todo. Las claves que no son de estas 9
        //    columnas se ignoran (antes: lista blanca 'ColumnasActualizables').
        string? idAsis = datos.TryGetValue("id_asis", out var vIdAsis) ? (string?)vIdAsis : actual.IdAsis;
        string nombres = datos.TryGetValue("nombres", out var vNombres) ? (string)vNombres! : actual.Nombres;
        string apellidos = datos.TryGetValue("apellidos", out var vApellidos) ? (string)vApellidos! : actual.Apellidos;
        string? nombreCompletoAsis = datos.TryGetValue("nombre_completo_asis", out var vCompleto) ? (string?)vCompleto : actual.NombreCompletoAsis;
        string? correoInst = datos.TryGetValue("correo_institucional", out var vCorreoInst) ? (string?)vCorreoInst : actual.CorreoInstitucional;
        string? correoPers = datos.TryGetValue("correo_personal", out var vCorreoPers) ? (string?)vCorreoPers : actual.CorreoPersonal;
        string? celular = datos.TryGetValue("celular", out var vCelular) ? (string?)vCelular : actual.Celular;
        bool esExterno = datos.TryGetValue("es_externo", out var vExterno) ? (bool)vExterno! : actual.EsExterno;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO. Los CHECK de
        //    la tabla (id_asis obligatorio si no es externo, al menos un
        //    correo...) ahora se evalúan sobre la fila YA mezclada -> 500.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_asistente(
                @idAsistente, @idAsis, @nombres, @apellidos, @nombreCompletoAsis,
                @correoInst, @correoPers, @celular, @esExterno, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(sqlActualizar, new
        {
            idAsistente, idAsis, nombres, apellidos, nombreCompletoAsis,
            correoInst, correoPers, celular, esExterno, activo,
        });

        // NULL = ya no existía (carrera entre el paso 1 y el 3) -> "0 filas".
        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idAsistente)
    {
        const string sql = "CALL catedras.sp_eliminar_asistente(@idAsistente, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idAsistente });

        // {"eliminado": true|false}. LÓGICO: UPDATE activo=false dentro del
        // procedimiento. Ya inactivo o inexistente -> false -> 0 -> 404.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
