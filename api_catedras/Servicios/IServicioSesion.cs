// ==========================================================================
//  Servicios/IServicioSesion.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Contrato de negocio de 'Sesion' (RF119-RF126).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Sesion'.</summary>
public interface IServicioSesion
{
    /// <summary>Lista sesiones (de una cátedra si llega 'fkCatedra'). 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Sesion> Datos, int Total)> ListarAsync(long? fkCatedra, int? limite);

    Task<Sesion> ObtenerAsync(long idSesion);

    /// <summary>Crea y devuelve (id_sesion, numero_reunion, creado_en) que decidió la BD.
    /// 'fin' &lt;= 'inicio' -> ArgumentException (400) sin tocar el repositorio.</summary>
    Task<(long IdSesion, short NumeroReunion, DateTime CreadoEn)> CrearAsync(SesionCrear peticion);

    Task<int> ReemplazarAsync(long idSesion, SesionReemplazo peticion);
    Task<int> ActualizarAsync(long idSesion, SesionActualizar peticion);
    Task<int> EliminarAsync(long idSesion);
}
