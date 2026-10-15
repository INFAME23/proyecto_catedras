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
}
