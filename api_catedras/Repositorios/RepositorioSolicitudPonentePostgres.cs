// ==========================================================================
//  Repositorios/RepositorioSolicitudPonentePostgres.cs  ·  Cátedras · v7.1 · D56
// --------------------------------------------------------------------------
//  Todo pasa por los procedimientos de 'db/postgres/migraciones/055_solicitud_ponente.sql'
//  (mismo molde que RepositorioPonenciaPostgres): la API no escribe SQL de
//  tablas. Aprobar es UNA llamada: la base crea al asistente, su documento y
//  la ponencia en una sola transacción.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.solicitud_ponente' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioSolicitudPonentePostgres : IRepositorioSolicitudPonente
{
    private readonly string _cadenaConexion;

    public RepositorioSolicitudPonentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<SolicitudPonente>> ObtenerTodosAsync(string? estado, int limite)
    {
        const string sql = "CALL catedras.sp_listar_solicitud_ponente(@estado, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { estado, limite });

        return JsonSerializer.Deserialize<List<SolicitudPonente>>(json!) ?? [];
    }

    public async Task<SolicitudPonente?> ObtenerUnoAsync(long idSolicitud)
    {
        const string sql = "CALL catedras.sp_consultar_solicitud_ponente(@idSolicitud, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idSolicitud });

        // NULL = el procedimiento no encontró la fila.
        return json is null ? null : JsonSerializer.Deserialize<SolicitudPonente>(json);
    }

    public async Task<SolicitudPonente> CrearAsync(SolicitudPonenteCrear datos)
    {
        const string sql = @"
            CALL catedras.sp_crear_solicitud_ponente(
                @Nombres, @Apellidos, @FkTipoDocumento, @NumeroDocumento,
                @PerfilProfesional, @Direccion, @Telefono, @CorreoInstitucional,
                @TituloPonencia, @ResumenPonencia, @Inicio, @Fin, @FkModalidad, @FkSede, @FkPeriodo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        // Npgsql solo escribe 'timestamptz' con desfase 0: los instantes viajan en UTC.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            datos.Nombres, datos.Apellidos, datos.FkTipoDocumento, datos.NumeroDocumento,
            datos.PerfilProfesional, datos.Direccion, datos.Telefono, datos.CorreoInstitucional,
            datos.TituloPonencia, datos.ResumenPonencia,
            Inicio = datos.Inicio!.Value.UtcDateTime, Fin = datos.Fin!.Value.UtcDateTime,
            datos.FkModalidad, datos.FkSede, datos.FkPeriodo,
        });

        return JsonSerializer.Deserialize<SolicitudPonente>(json!)!;
    }

    public async Task<SolicitudPonente?> ResolverAsync(long idSolicitud, SolicitudPonenteResolver datos)
    {
        const string sql = @"
            CALL catedras.sp_resolver_solicitud_ponente(
                @idSolicitud, @aprobar, @observacion, @resueltoPor,
                @nombreCatedra, @fkTipoEvento, @idEventoAsis, @fkModalidad, @fkSede, @fkPeriodo, @lugar,
                @inicio, @fin, @enlaceVirtual, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            idSolicitud,
            aprobar = datos.Aprobar!.Value,
            observacion = datos.Observacion,
            resueltoPor = datos.ResueltoPor,
            nombreCatedra = datos.NombreCatedra,
            fkTipoEvento = datos.FkTipoEvento,
            idEventoAsis = datos.IdEventoAsis,
            fkModalidad = datos.FkModalidad,
            fkSede = datos.FkSede,
            fkPeriodo = datos.FkPeriodo,
            lugar = datos.Lugar,
            inicio = datos.Inicio?.UtcDateTime,
            fin = datos.Fin?.UtcDateTime,
            enlaceVirtual = datos.EnlaceVirtual,
        });

        // NULL = la solicitud no existe.
        return json is null ? null : JsonSerializer.Deserialize<SolicitudPonente>(json);
    }
}
