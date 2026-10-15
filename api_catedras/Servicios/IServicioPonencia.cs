// ==========================================================================
//  Servicios/IServicioPonencia.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Contrato de negocio de 'Ponencia' (RF127-RF131, ADR D19): listar,
//  obtener por la llave completa, asignar, cambiar el rol (PUT) y
//  desasignar. Sin PATCH: no hay columnas fuera de la llave aparte del rol.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Ponencia'.</summary>
public interface IServicioPonencia
{
    Task<(List<Ponencia> Datos, int Total)> ListarAsync(long? fkSesion, long? fkAsistente, int? limite);

    /// <summary>La participación con esa llave exacta. Inexistente -> NoEncontradoExcepcion (404).</summary>
    Task<Ponencia> ObtenerAsync(long fkSesion, long fkAsistente, string rol);

    /// <summary>Asigna. Devuelve la fila creada (con el rol efectivo).</summary>
    Task<Ponencia> CrearAsync(PonenciaCrear peticion);

    /// <summary>Cambia el rol. Devuelve la fila con el rol nuevo. Llave inexistente -> NoEncontradoExcepcion (404).</summary>
    Task<Ponencia> ReemplazarAsync(long fkSesion, long fkAsistente, string rol, PonenciaReemplazo peticion);

    /// <summary>Desasigna. Inexistente -> NoEncontradoExcepcion (404).</summary>
    Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol);
}
