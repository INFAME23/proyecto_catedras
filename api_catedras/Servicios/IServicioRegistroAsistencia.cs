// ==========================================================================
//  Servicios/IServicioRegistroAsistencia.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Contrato de negocio de 'RegistroAsistencia': listar, obtener y el alta
//  MANUAL. Un registro es un hecho auditable: no se reemplaza ni se borra.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'RegistroAsistencia'.</summary>
public interface IServicioRegistroAsistencia
{
    Task<(List<RegistroAsistencia> Datos, int Total)> ListarAsync(long? fkSesion, long? fkAsistente, int? limite);

    Task<RegistroAsistencia> ObtenerAsync(long idRegistro);

    /// <summary>Alta manual. 'origen' distinto de MANUAL/IMPORTADO -> ArgumentException (400, D26).
    /// Devuelve la fila con las instantáneas que calculó la BD.</summary>
    Task<RegistroAsistencia> CrearAsync(RegistroAsistenciaCrear peticion);
}
