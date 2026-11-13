// ==========================================================================
//  Repositorios/IRepositorioAsistenciaPublica.cs  ·  Subversión v7.2 · RF-S9
// --------------------------------------------------------------------------
//  Contrato del acceso a datos del registro público de asistencia. No hay
//  tabla propia: todo pasa por funciones y procedimientos de la migración 063
//  (la regla de negocio vive en la base de datos, en UNA transacción: Regla 1).
//  El servicio solo conoce esta interfaz (Artículo 3).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

public interface IRepositorioAsistenciaPublica
{
    /// <summary>Datos de la página pública del enlace; null si el token no existe.</summary>
    Task<EnlacePublico?> ConsultarEnlaceAsync(Guid token);

    /// <summary>Aviso temprano: ¿la ubicación cae dentro del radio de la sede? No guarda nada.</summary>
    Task<UbicacionVerificada> VerificarUbicacionAsync(Guid token, decimal? latitud, decimal? longitud);

    /// <summary>Registra a un estudiante por carnet. El 'estado' dice si se guardó (OK) o por qué no.</summary>
    Task<ResultadoRegistroPublico> RegistrarEstudianteAsync(
        Guid token, string carnet, decimal? latitud, decimal? longitud, string? ip, string? userAgent);

    /// <summary>Registra a un docente (crea o reutiliza a la persona por cédula).</summary>
    Task<ResultadoRegistroPublico> RegistrarDocenteAsync(
        Guid token, string cedula, string nombre, string contacto, string fkFacultad,
        decimal? latitud, decimal? longitud, string? ip, string? userAgent);

    /// <summary>Registra a un externo (crea o reutiliza a la persona por cédula).</summary>
    Task<ResultadoRegistroPublico> RegistrarExternoAsync(
        Guid token, string cedula, string nombre, string contacto, string procedencia,
        decimal? latitud, decimal? longitud, string? ip, string? userAgent);
}
