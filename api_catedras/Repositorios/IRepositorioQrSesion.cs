// ==========================================================================
//  Repositorios/IRepositorioQrSesion.cs  ·  Cátedras · Subversión v7.1 · D53
// --------------------------------------------------------------------------
//  Acceso al QR de cierre de una sesión y a su lista de asistencia (migración 051).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

public interface IRepositorioQrSesion
{
    /// <summary>CALL sp_emitir_qr_sesion. Null si la sesión no existe.</summary>
    Task<QrSesion?> EmitirAsync(long fkSesion, long? fkUsuario, bool regenerar);

    /// <summary>CALL sp_consultar_qr_sesion. Null si la sesión no tiene un QR vigente.</summary>
    Task<QrSesion?> ConsultarAsync(long fkSesion);

    /// <summary>CALL sp_asistencia_sesion: inscritos de la cátedra y quién asistió a esa sesión.</summary>
    Task<List<AsistenciaSesion>> AsistenciaAsync(long fkSesion);
}
