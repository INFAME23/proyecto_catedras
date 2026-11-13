// ==========================================================================
//  Servicios/ServicioAsistenciaPublica.cs  ·  Subversión v7.2 · RF-S9 / RF-S10
// --------------------------------------------------------------------------
//  Reglas de la capa de negocio del registro público de asistencia. La regla
//  PESADA (enlace vigente, ubicación contra la sede, persona, duplicado) vive
//  en la base de datos, en una transacción (migración 063); aquí solo se:
//    · normaliza lo que llega (espacios, IP válida, user_agent recortado),
//    · traduce el 'estado' que devuelve la BD a un texto amable en español,
//    · convierte "el enlace no existe" en NoEncontradoExcepcion (-> 404).
//
//  El servicio NO conoce HTTP: el controlador decide qué código HTTP le toca
//  a cada estado (Artículo 3).
// ==========================================================================

namespace ApiCatedras.Servicios;

using System.Net;
using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

public interface IServicioAsistenciaPublica
{
    Task<EnlacePublico> ConsultarEnlaceAsync(Guid? token);
    Task<UbicacionVerificada> VerificarUbicacionAsync(Guid? token, decimal? latitud, decimal? longitud);
    Task<ResultadoRegistroPublico> RegistrarEstudianteAsync(AsistenciaEstudianteCrear peticion);
    Task<ResultadoRegistroPublico> RegistrarDocenteAsync(AsistenciaDocenteCrear peticion);
    Task<ResultadoRegistroPublico> RegistrarExternoAsync(AsistenciaExternoCrear peticion);
}

public class ServicioAsistenciaPublica : IServicioAsistenciaPublica
{
    // La base de datos guarda el user_agent en un varchar(300).
    private const int LargoMaximoUserAgent = 300;

    private readonly IRepositorioAsistenciaPublica _repositorio;

    public ServicioAsistenciaPublica(IRepositorioAsistenciaPublica repositorio)
    {
        _repositorio = repositorio;
    }

    // ----------------------------------------------------------------------
    //  Mensajes para la persona, uno por estado de negocio (migración 063)
    // ----------------------------------------------------------------------
    public static string MensajeDe(string estado) => estado switch
    {
        "OK" => "Asistencia registrada.",
        "ENLACE_NO_EXISTE" => "Este enlace de registro no existe. Escanea de nuevo el código QR de la sesión.",
        "ENLACE_REVOCADO" => "Este enlace ya no es válido porque se generó uno nuevo. Escanea el código QR vigente.",
        "FUERA_DE_VENTANA" => "El registro de asistencia de esta sesión no está abierto en este momento.",
        "SIN_UBICACION" => "Necesitamos tu ubicación para registrar la asistencia en una sesión presencial.",
        "SEDE_SIN_COORDENADAS" => "No se puede comprobar el lugar de la sesión porque su sede no tiene coordenadas. Avisa al organizador.",
        "FUERA_DE_RADIO" => "Parece que no estás en el lugar de la cátedra. Acércate a la sede para registrar tu asistencia.",
        "ESTUDIANTE_NO_EXISTE" => "No encontramos ese código de estudiante. Revísalo e inténtalo de nuevo.",
        "SIN_VINCULACION_VIGENTE" => "Ese código de estudiante no tiene una vinculación vigente. Comunícate con el organizador.",
        "PERSONA_INACTIVA" => "Esa persona está desactivada en el sistema. Comunícate con el organizador.",
        "YA_REGISTRADO" => "Ya registraste tu asistencia en esta sesión.",
        "DATOS_INVALIDOS" => "Revisa los datos: alguno no tiene el formato esperado.",
        _ => "No se pudo registrar la asistencia.",
    };

    // ----------------------------------------------------------------------
    //  Datos de la página del enlace
    // ----------------------------------------------------------------------
    public async Task<EnlacePublico> ConsultarEnlaceAsync(Guid? token)
    {
        var enlace = await _repositorio.ConsultarEnlaceAsync(ExigirToken(token));

        // null = no existe ese token -> 404 en el controlador.
        return enlace ?? throw new NoEncontradoExcepcion(MensajeDe("ENLACE_NO_EXISTE"));
    }

    // ----------------------------------------------------------------------
    //  Aviso temprano de ubicación (no guarda nada)
    // ----------------------------------------------------------------------
    public async Task<UbicacionVerificada> VerificarUbicacionAsync(Guid? token, decimal? latitud, decimal? longitud)
    {
        // Misma regla de coordenadas que el POST, pero en parámetros de consulta (-> 400).
        if ((latitud is null) != (longitud is null))
            throw new ArgumentException("Los parámetros 'latitud' y 'longitud' van juntos o ninguno.");
        if (latitud is < -90 or > 90)
            throw new ArgumentException("El parámetro 'latitud' debe estar entre -90 y 90.");
        if (longitud is < -180 or > 180)
            throw new ArgumentException("El parámetro 'longitud' debe estar entre -180 y 180.");

        var resultado = await _repositorio.VerificarUbicacionAsync(ExigirToken(token), latitud, longitud);

        if (resultado.Estado == "ENLACE_NO_EXISTE")
            throw new NoEncontradoExcepcion(MensajeDe("ENLACE_NO_EXISTE"));

        // Aquí «OK» no significa que se registró algo (el aviso no guarda nada): significa «ubicación correcta».
        resultado.Mensaje = resultado.Estado == "OK"
            ? (resultado.RequiereUbicacion
                ? "Estás en el lugar de la cátedra."
                : "Esta sesión no requiere ubicación.")
            : MensajeDe(resultado.Estado);
        return resultado;
    }

    // ----------------------------------------------------------------------
    //  Los tres registros
    // ----------------------------------------------------------------------
    public async Task<ResultadoRegistroPublico> RegistrarEstudianteAsync(AsistenciaEstudianteCrear peticion)
    {
        var resultado = await _repositorio.RegistrarEstudianteAsync(
            peticion.Token!.Value, peticion.Carnet!.Trim(), peticion.Latitud, peticion.Longitud,
            IpValida(peticion.Ip), UserAgentRecortado(peticion.UserAgent));
        return ConMensaje(resultado);
    }

    public async Task<ResultadoRegistroPublico> RegistrarDocenteAsync(AsistenciaDocenteCrear peticion)
    {
        var resultado = await _repositorio.RegistrarDocenteAsync(
            peticion.Token!.Value, peticion.Cedula!.Trim(), peticion.Nombre!.Trim(), peticion.Contacto!.Trim(),
            peticion.FkFacultad!.Trim(), peticion.Latitud, peticion.Longitud,
            IpValida(peticion.Ip), UserAgentRecortado(peticion.UserAgent));
        return ConMensaje(resultado);
    }

    public async Task<ResultadoRegistroPublico> RegistrarExternoAsync(AsistenciaExternoCrear peticion)
    {
        var resultado = await _repositorio.RegistrarExternoAsync(
            peticion.Token!.Value, peticion.Cedula!.Trim(), peticion.Nombre!.Trim(), peticion.Contacto!.Trim(),
            peticion.Procedencia!.Trim(), peticion.Latitud, peticion.Longitud,
            IpValida(peticion.Ip), UserAgentRecortado(peticion.UserAgent));
        return ConMensaje(resultado);
    }

    // ----------------------------------------------------------------------
    //  Ayudas privadas
    // ----------------------------------------------------------------------
    // Sin token -> 400. Un token «vacío» (00000000-...) no es un error de forma: es un enlace que no existe,
    // y la base de datos lo dirá (-> 404 como cualquier otro token desconocido).
    private static Guid ExigirToken(Guid? token) =>
        token ?? throw new ArgumentException("El parámetro 'token' es obligatorio.");

    private static ResultadoRegistroPublico ConMensaje(ResultadoRegistroPublico resultado)
    {
        resultado.Mensaje = MensajeDe(resultado.Estado);
        return resultado;
    }

    /// <summary>La IP debe ser válida (la columna es 'inet'): una mal formada sería un 500 del motor.</summary>
    private static string? IpValida(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;

        if (!IPAddress.TryParse(ip.Trim(), out var direccion))
            throw new ArgumentException("El campo 'ip' no es una dirección IP válida.");

        return direccion.ToString();
    }

    private static string? UserAgentRecortado(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return null;

        return userAgent.Length <= LargoMaximoUserAgent ? userAgent : userAgent[..LargoMaximoUserAgent];
    }
}
