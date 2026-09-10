// ==========================================================================
//  Servicios/IServicioRol.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'Rol'. Mismo patrón que IServicioSede.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Rol'.</summary>
public interface IServicioRol
{
    /// <summary>Lista roles. 'limite' nulo -> 1000; 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Rol> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el rol por código. No existe -> NoEncontradoExcepcion.</summary>
    Task<Rol> ObtenerAsync(string idRol);

    /// <summary>Crea un rol a partir de la petición del POST.</summary>
    Task CrearAsync(RolCrear peticion);

    /// <summary>Reemplaza por completo el rol (PUT).</summary>
    Task<int> ReemplazarAsync(string idRol, RolReemplazo peticion);

    /// <summary>Actualiza parcialmente el rol (PATCH).</summary>
    Task<int> ActualizarAsync(string idRol, RolActualizar peticion);

    /// <summary>Elimina el rol.</summary>
    Task<int> EliminarAsync(string idRol);
}
