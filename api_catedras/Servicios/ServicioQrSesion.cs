// ==========================================================================
//  Servicios/ServicioQrSesion.cs  ·  Cátedras · Subversión v7.1 · D53
// --------------------------------------------------------------------------
//  Reglas del QR de cierre de una sesión. La ventana de tiempo, la exigencia
//  de inscripción y la ubicación las decide la BD (migración 051 y 045); aquí
//  solo se traduce "la sesión no existe" a 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

public interface IServicioQrSesion
{
    /// <summary>Crea el QR de la sesión, o devuelve el vigente. Sesión inexistente -> NoEncontradoExcepcion.</summary>
    Task<QrSesion> EmitirAsync(QrSesionEmitir peticion);

    /// <summary>El QR vigente, o null si la sesión no tiene.</summary>
    Task<QrSesion?> ConsultarAsync(long? fkSesion);

    /// <summary>Lista de asistencia de la sesión. Sin 'fkSesion' -> ArgumentException.</summary>
    Task<List<AsistenciaSesion>> AsistenciaAsync(long? fkSesion);
}

public class ServicioQrSesion : IServicioQrSesion
{
    private readonly IRepositorioQrSesion _repositorio;

    public ServicioQrSesion(IRepositorioQrSesion repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<QrSesion> EmitirAsync(QrSesionEmitir peticion)
    {
        var qr = await _repositorio.EmitirAsync(
            peticion.FkSesion!.Value, peticion.FkUsuario, peticion.Regenerar ?? false);

        return qr ?? throw new NoEncontradoExcepcion($"No existe la sesión '{peticion.FkSesion}'.");
    }

    private static long Exigir(long? fkSesion) =>
        fkSesion is > 0 ? fkSesion.Value
            : throw new ArgumentException("El parámetro 'fkSesion' es obligatorio y debe ser mayor que 0.");

    public Task<QrSesion?> ConsultarAsync(long? fkSesion) => _repositorio.ConsultarAsync(Exigir(fkSesion));

    public Task<List<AsistenciaSesion>> AsistenciaAsync(long? fkSesion) => _repositorio.AsistenciaAsync(Exigir(fkSesion));
}
