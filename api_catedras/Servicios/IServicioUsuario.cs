// ==========================================================================
//  Servicios/IServicioUsuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Contrato de negocio de 'Usuario'. Molde completo (como Catedra) más el
//  LOGIN, que devuelve null cuando las credenciales no sirven (ADR D27).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Usuario'.</summary>
public interface IServicioUsuario
{
    /// <summary>Lista usuarios. 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Usuario> Datos, int Total)> ListarAsync(int? limite);

    Task<Usuario> ObtenerAsync(long idUsuario);

    /// <summary>Hashea la clave con BCrypt, crea y devuelve (id_usuario, creado_en)
    /// que decidió la BD. Nunca devuelve el hash.</summary>
    Task<(long IdUsuario, DateTime CreadoEn)> CrearAsync(UsuarioCrear peticion);

    Task<int> ReemplazarAsync(long idUsuario, UsuarioReemplazo peticion);
    Task<int> ActualizarAsync(long idUsuario, UsuarioActualizar peticion);
    Task<int> EliminarAsync(long idUsuario);

    /// <summary>Verifica usuario y clave. Devuelve null si el usuario no existe, está
    /// inactivo o la clave no coincide (las tres causas se tratan IGUAL, ADR D27);
    /// si son válidas, devuelve el usuario y sus roles vigentes.</summary>
    Task<(Usuario Usuario, List<string> Roles)?> LoginAsync(UsuarioLogin peticion);

    /// <summary>RF-S5 (v7.1). Cambia la clave sabiendo la actual. Devuelve false si la cuenta
    /// está inactiva o 'clave_actual' no coincide (misma respuesta, ADR D27); true si cambió.
    /// Usuario inexistente -> NoEncontradoExcepcion; clave nueva igual a la actual -> ArgumentException.</summary>
    Task<bool> CambiarClaveAsync(long idUsuario, UsuarioCambiarClave peticion);

    /// <summary>RF-S5/RF-S6 (v7.1). Pide un código de recuperación. Null si no hay una cuenta
    /// activa con ese usuario o correo (el controlador responde igual que si la hubiera).
    /// 'ip' mal escrita -> ArgumentException.</summary>
    Task<(long IdCodigo, string Codigo, string EnviadoA, string Usuario)?> SolicitarRecuperacionAsync(
        RecuperacionSolicitar peticion);

    /// <summary>RF-S5/RF-S6 (v7.1). Valida el código y cambia la clave (hash BCrypt calculado aquí).
    /// Código incorrecto/usado/vencido/agotado -> CodigoRecuperacionInvalidoExcepcion (400).</summary>
    Task RestablecerClaveAsync(RecuperacionRestablecer peticion);

    /// <summary>v7.1 · D54. Obliga a esa persona a cambiar su clave en el próximo ingreso (p. ej. tras entregarle
    /// una clave temporal). Usuario inexistente -> NoEncontradoExcepcion.</summary>
    Task ExigirCambioClaveAsync(long idUsuario);
}
