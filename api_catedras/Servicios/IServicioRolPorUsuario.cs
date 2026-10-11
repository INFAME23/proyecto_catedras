// ==========================================================================
//  Servicios/IServicioRolPorUsuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Contrato de negocio de 'RolPorUsuario' (ADR D16): listar, abrir y cerrar.
//  Sin obtener-por-id, reemplazar, actualizar ni eliminar.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'RolPorUsuario'.</summary>
public interface IServicioRolPorUsuario
{
    Task<(List<RolPorUsuario> Datos, int Total)> ListarAsync(long? fkUsuario, int? limite);

    /// <summary>Abre un rol. Devuelve la fila creada (con su fecha_ini efectiva).</summary>
    Task<RolPorUsuario> CrearAsync(RolPorUsuarioCrear peticion);

    /// <summary>Cierra un rol. 404 si no existe; 400 si ya estaba cerrado
    /// o si la fecha de cierre es anterior a la de inicio.</summary>
    Task<RolPorUsuario> CerrarAsync(RolPorUsuarioCerrar peticion);
}
