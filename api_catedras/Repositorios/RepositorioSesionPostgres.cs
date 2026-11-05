// ==========================================================================
//  Repositorios/RepositorioSesionPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/011_sp_sesion.sql'. Mismo molde que
//  RepositorioSedePostgres.cs. 'IRepositorioSesion', 'ServicioSesion',
//  'SesionController' y 'Peticiones/' NO cambiaron.
//
//  · TRIGGER 'trg_sesion_consecutivo': si 'numero_reunion' llega en 0 (lo
//    que manda el servicio cuando el cliente no lo indicó), el trigger
//    calcula el consecutivo en el INSERT. El procedimiento devuelve la
//    fila RELEÍDA después del INSERT: de ese JSON salen el 'numero_reunion'
//    calculado, el 'id_sesion' y el 'creado_en'. Aquí no se reconstruye
//    nada a mano.
//  · 'sesion' NO tiene 'activo': 'EliminarAsync' es un DELETE físico (lo
//    hace el procedimiento) y el listado trae todas las filas.
//  · FECHAS: 'inicio' y 'fin' viajan como DateTimeOffset (se mapea siempre
//    a 'timestamptz') para que el instante sea el mismo sin importar la zona
//    horaria de la sesión de PostgreSQL.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.sesion' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioSesionPostgres : IRepositorioSesion
{
    private readonly string _cadenaConexion;

    public RepositorioSesionPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Sesion>> ObtenerTodosAsync(long? fkCatedra, int limite)
    {
        // 'fkCatedra' null = todas las cátedras (el procedimiento lo trata
        // como "sin filtro").
        const string sql = "CALL catedras.sp_listar_sesion(@fkCatedra, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkCatedra, limite });

        var sesiones = JsonSerializer.Deserialize<List<Sesion>>(json!) ?? [];
        foreach (var s in sesiones)
            Normalizar(s);
        return sesiones;
    }

    public async Task<Sesion?> ObtenerPorIdAsync(long idSesion)
    {
        const string sql = "CALL catedras.sp_consultar_sesion(@idSesion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idSesion });

        if (json is null)
            return null;   // el procedimiento no encontró la fila

        var sesion = JsonSerializer.Deserialize<Sesion>(json)!;
        Normalizar(sesion);
        return sesion;
    }

    public async Task<(long IdSesion, short NumeroReunion, DateTime CreadoEn)> CrearAsync(
        Sesion sesion, (int? MinutosAntes, int? MinutosDespues, long? FkUsuario)? enlaceInicial)
    {
        // Maestro-detalle (Guía IA, Regla 1 · migración 044): 'sp_guardar_sesion'
        // reutiliza 'sp_crear_sesion' y, si llega 'p_enlace', 'sp_emitir_enlace'
        // (del script), en el MISMO CALL. Sin enlace (NULL) hace lo mismo que antes.
        const string sql = @"
            CALL catedras.sp_guardar_sesion(
                @FkCatedra, @Inicio, @Fin, @FkModalidad, @FkSede, @FkPeriodo,
                @NumeroReunion, @NumeroReunionAsis, @Titulo, @FkEncuesta,
                @Lugar, @EnlaceVirtual, CAST(NULL AS integer), @Estado, CAST(@Enlace AS jsonb), NULL);";

        string? enlaceJson = enlaceInicial is null
            ? null
            : JsonSerializer.Serialize(new
            {
                minutos_antes = enlaceInicial.Value.MinutosAntes,
                minutos_despues = enlaceInicial.Value.MinutosDespues,
                fk_usuario = enlaceInicial.Value.FkUsuario,
            });

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Rango o cupo inválidos, reunión repetida en la cátedra o FK
        // inexistente -> NpgsqlException, igual que antes.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            sesion.FkCatedra,
            Inicio = ComoInstante(sesion.Inicio),
            Fin = ComoInstante(sesion.Fin),
            sesion.FkModalidad,
            sesion.FkSede,
            sesion.FkPeriodo,
            sesion.NumeroReunion,        // 0 = que lo calcule el trigger
            sesion.NumeroReunionAsis,
            sesion.Titulo,
            sesion.FkEncuesta,
            sesion.Lugar,
            sesion.EnlaceVirtual,
            sesion.Estado,
            Enlace = enlaceJson,
        });

        var creada = JsonSerializer.Deserialize<Sesion>(json!)!;
        return (creada.IdSesion, creada.NumeroReunion, creada.CreadoEn.ToUniversalTime());
    }

    public async Task<int> ActualizarAsync(long idSesion, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual: 'sp_actualizar_sesion' reemplaza TODAS las
        //    columnas de negocio, así que un PATCH necesita completar lo que
        //    no llegó con el valor guardado. (Es lo mismo que ya hacía
        //    ServicioSesion para cruzar 'inicio'/'fin'; el servicio sigue
        //    haciéndolo para su validación y no cambia.)
        const string sqlConsultar = "CALL catedras.sp_consultar_sesion(@idSesion, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idSesion });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<Sesion>(actualJson)!;
        Normalizar(actual);

        // 2) Mezclar: lo que llegó en 'datos' PISA el valor actual. Un PUT
        //    manda las 14 claves (con 'null' explícito donde corresponde),
        //    así que reemplaza todo igual que antes. Los valores del
        //    diccionario llegan ya tipados por el servicio/la petición
        //    (long, short, int, DateTime UTC, string, bool); 'Convert' solo
        //    se usa para no depender de si vienen como short o int, etc.
        long fkCatedra = datos.TryGetValue("fk_catedra", out var vCatedra) ? Convert.ToInt64(vCatedra) : actual.FkCatedra;
        short numeroReunion = datos.TryGetValue("numero_reunion", out var vReunion) ? Convert.ToInt16(vReunion) : actual.NumeroReunion;
        short? numeroReunionAsis = datos.TryGetValue("numero_reunion_asis", out var vAsis)
            ? (vAsis is null ? null : Convert.ToInt16(vAsis)) : actual.NumeroReunionAsis;
        string? titulo = datos.TryGetValue("titulo", out var vTitulo) ? (string?)vTitulo : actual.Titulo;
        DateTime inicio = datos.TryGetValue("inicio", out var vInicio) ? AUtc(vInicio!) : actual.Inicio;
        DateTime fin = datos.TryGetValue("fin", out var vFin) ? AUtc(vFin!) : actual.Fin;
        string fkModalidad = datos.TryGetValue("fk_modalidad", out var vModalidad) ? (string)vModalidad! : actual.FkModalidad;
        string fkSede = datos.TryGetValue("fk_sede", out var vSede) ? (string)vSede! : actual.FkSede;
        string fkPeriodo = datos.TryGetValue("fk_periodo", out var vPeriodo) ? (string)vPeriodo! : actual.FkPeriodo;
        long? fkEncuesta = datos.TryGetValue("fk_encuesta", out var vEncuesta)
            ? (vEncuesta is null ? null : Convert.ToInt64(vEncuesta)) : actual.FkEncuesta;
        string? lugar = datos.TryGetValue("lugar", out var vLugar) ? (string?)vLugar : actual.Lugar;
        string? enlaceVirtual = datos.TryGetValue("enlace_virtual", out var vEnlace) ? (string?)vEnlace : actual.EnlaceVirtual;
        string estado = datos.TryGetValue("estado", out var vEstado) ? (string)vEstado! : actual.Estado;

        // 3) Procedimiento con el conjunto COMPLETO. Rango o cupo inválidos,
        //    reunión repetida o FK inexistente -> NpgsqlException.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_sesion(
                @idSesion, @fkCatedra, @numeroReunion, @numeroReunionAsis, @titulo,
                @inicio, @fin, @fkModalidad, @fkSede, @fkPeriodo, @fkEncuesta,
                @lugar, @enlaceVirtual, CAST(NULL AS integer), @estado, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(sqlActualizar, new
        {
            idSesion,
            fkCatedra,
            numeroReunion,
            numeroReunionAsis,
            titulo,
            inicio = ComoInstante(inicio),
            fin = ComoInstante(fin),
            fkModalidad,
            fkSede,
            fkPeriodo,
            fkEncuesta,
            lugar,
            enlaceVirtual,
            estado,
        });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idSesion)
    {
        const string sql = "CALL catedras.sp_eliminar_sesion(@idSesion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Si la sesión tiene registros de asistencia o lotes de migración,
        // la FK RESTRICT hace que el procedimiento lance NpgsqlException.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idSesion });

        // {"eliminado": true|false}; FÍSICO (la tabla no tiene 'activo').
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }

    // ----------------------------------------------------------------------
    //  AYUDAS DE FECHAS
    //  'row_to_json' escribe 'inicio'/'fin'/'creado_en' con el desfase de la
    //  zona horaria de la SESIÓN de PostgreSQL ("...-05:00") y
    //  System.Text.Json los entrega como DateTime LOCAL. Antes Npgsql
    //  entregaba DateTime UTC directo de la columna. Se devuelven a UTC para
    //  que la respuesta de la API siga serializando igual ("...Z") y para que
    //  lo que se reenvía al procedimiento sea el mismo instante.
    // ----------------------------------------------------------------------
    private static void Normalizar(Sesion s)
    {
        s.Inicio = s.Inicio.ToUniversalTime();
        s.Fin = s.Fin.ToUniversalTime();
        s.CreadoEn = s.CreadoEn.ToUniversalTime();
    }

    // Valor del diccionario (DateTime o DateTimeOffset) -> DateTime en UTC.
    private static DateTime AUtc(object valor) => valor switch
    {
        DateTimeOffset o => o.UtcDateTime,
        DateTime d => d.ToUniversalTime(),
        _ => throw new ArgumentException("Fecha con formato inválido."),
    };

    // DateTime -> DateTimeOffset con desfase 0: Npgsql lo manda siempre como
    // 'timestamptz', sin pasar por la zona horaria de la sesión.
    private static DateTimeOffset ComoInstante(DateTime utc) =>
        new(DateTime.SpecifyKind(utc.ToUniversalTime(), DateTimeKind.Utc));
}
