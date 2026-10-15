// ==========================================================================
//  Servicios/IServicioFacultad.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Contrato de la capa de NEGOCIO de 'Facultad'. El controlador depende de
//  esta interfaz. Problemas -> excepciones:
//     ArgumentException     -> 400 (límite inválido, PATCH vacío,
//                                   facultad que se apunta a sí misma)
//     NoEncontradoExcepcion -> 404
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Facultad'.</summary>
public interface IServicioFacultad
{
    /// <summary>Lista facultades. 'limite' nulo -> 1000; &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Facultad> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false);

    /// <summary>Devuelve la facultad o lanza NoEncontradoExcepcion.</summary>
    Task<Facultad> ObtenerAsync(string idFacultad);

    /// <summary>Crea la facultad. Padre igual al propio id -> ArgumentException.</summary>
    Task CrearAsync(FacultadCrear peticion);

    /// <summary>PUT. Autorreferencia -> 400; 0 filas -> 404.</summary>
    Task<int> ReemplazarAsync(string idFacultad, FacultadReemplazo peticion);

    /// <summary>PATCH. Body vacío o autorreferencia -> 400; 0 filas -> 404.</summary>
    Task<int> ActualizarAsync(string idFacultad, FacultadActualizar peticion);

    /// <summary>DELETE. 0 filas -> 404.</summary>
    Task<int> EliminarAsync(string idFacultad);
}
