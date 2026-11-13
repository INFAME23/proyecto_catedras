// ==========================================================================
//  Repositorios/RepositorioAsistenciaPublicaPostgres.cs  ·  Subversión v7.2 · RF-S9
// --------------------------------------------------------------------------
//  Solo arma el 'CALL'/'SELECT', manda los parámetros y convierte lo que
//  devuelve la base de datos en objetos C# (migración 063). Nada de reglas
//  aquí: si el enlace sirve, si la persona existe o si está dentro del radio
//  lo decide el procedimiento.
//
//  Un 'CALL' con parámetros INOUT devuelve UNA fila con esos parámetros; aquí
//  se lee como 'dynamic' (cada columna se llama como su parámetro).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;

/// <summary>Registro público de asistencia en PostgreSQL, vía funciones y procedimientos.</summary>
public class RepositorioAsistenciaPublicaPostgres : IRepositorioAsistenciaPublica
{
    private readonly string _cadenaConexion;

    public RepositorioAsistenciaPublicaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<EnlacePublico?> ConsultarEnlaceAsync(Guid token)
    {
        // La función devuelve 'json'; '::text' lo entrega como cadena para que Dapper la lea.
        const string sql = "SELECT catedras.fn_consultar_enlace_publico(@token)::text;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { token });

        // NULL = el token no existe.
        return LectorJsonSp.Fila<EnlacePublico>(json);
    }

    public async Task<UbicacionVerificada> VerificarUbicacionAsync(Guid token, decimal? latitud, decimal? longitud)
    {
        const string sql = "SELECT catedras.fn_verificar_ubicacion(@token, @latitud, @longitud)::text;";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { token, latitud, longitud });

        return LectorJsonSp.Fila<UbicacionVerificada>(json)!;
    }

    public async Task<ResultadoRegistroPublico> RegistrarEstudianteAsync(
        Guid token, string carnet, decimal? latitud, decimal? longitud, string? ip, string? userAgent)
    {
        // Los 4 NULL finales son los parámetros INOUT (estado, id_registro, distancia, radio).
        // '@ip::inet' convierte el texto en el tipo inet de PostgreSQL (NULL si no hay IP).
        const string sql = @"
            CALL catedras.sp_registrar_asistencia_estudiante(
                @token, @carnet, @latitud, @longitud, @ip::inet, @userAgent, NULL, NULL, NULL, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var fila = await conexion.QuerySingleAsync(sql, new { token, carnet, latitud, longitud, ip, userAgent });
        return Convertir(fila);
    }

    public async Task<ResultadoRegistroPublico> RegistrarDocenteAsync(
        Guid token, string cedula, string nombre, string contacto, string fkFacultad,
        decimal? latitud, decimal? longitud, string? ip, string? userAgent)
    {
        const string sql = @"
            CALL catedras.sp_registrar_asistencia_docente(
                @token, @cedula, @nombre, @contacto, @fkFacultad,
                @latitud, @longitud, @ip::inet, @userAgent, NULL, NULL, NULL, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var fila = await conexion.QuerySingleAsync(sql,
            new { token, cedula, nombre, contacto, fkFacultad, latitud, longitud, ip, userAgent });
        return Convertir(fila);
    }

    public async Task<ResultadoRegistroPublico> RegistrarExternoAsync(
        Guid token, string cedula, string nombre, string contacto, string procedencia,
        decimal? latitud, decimal? longitud, string? ip, string? userAgent)
    {
        const string sql = @"
            CALL catedras.sp_registrar_asistencia_externo(
                @token, @cedula, @nombre, @contacto, @procedencia,
                @latitud, @longitud, @ip::inet, @userAgent, NULL, NULL, NULL, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        var fila = await conexion.QuerySingleAsync(sql,
            new { token, cedula, nombre, contacto, procedencia, latitud, longitud, ip, userAgent });
        return Convertir(fila);
    }

    // La fila de un CALL llega como 'dynamic'; se pasa a diccionario para leer cada columna por su nombre.
    private static ResultadoRegistroPublico Convertir(dynamic fila)
    {
        var columnas = (IDictionary<string, object?>)fila;
        return new ResultadoRegistroPublico
        {
            Estado = (string)columnas["p_estado"]!,
            IdRegistro = columnas["p_id_registro"] as long?,
            DistanciaMetros = columnas["p_distancia_metros"] as decimal?,
            RadioMetros = columnas["p_radio_metros"] as int?,
        };
    }
}
