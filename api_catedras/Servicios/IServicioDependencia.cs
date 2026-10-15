// ==========================================================================
//  Servicios/IServicioDependencia.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Contrato de la capa de NEGOCIO de 'Dependencia'. El controlador depende de
//  esta interfaz. Problemas -> excepciones:
//     ArgumentException     -> 400 (límite inválido, PATCH vacío,
//                                   dependencia que se apunta a sí misma)
//     NoEncontradoExcepcion -> 404
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Dependencia'.</summary>
public interface IServicioDependencia
{
    /// <summary>Lista dependencias. 'limite' nulo -> 1000; &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Dependencia> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false);

    /// <summary>Devuelve la dependencia o lanza NoEncontradoExcepcion.</summary>
    Task<Dependencia> ObtenerAsync(string idDependencia);

    /// <summary>Crea la dependencia. Padre igual al propio id -> ArgumentException.</summary>
    Task CrearAsync(DependenciaCrear peticion);

    /// <summary>PUT. Autorreferencia -> 400; 0 filas -> 404.</summary>
    Task<int> ReemplazarAsync(string idDependencia, DependenciaReemplazo peticion);

    /// <summary>PATCH. Body vacío o autorreferencia -> 400; 0 filas -> 404.</summary>
    Task<int> ActualizarAsync(string idDependencia, DependenciaActualizar peticion);

    /// <summary>DELETE. 0 filas -> 404.</summary>
    Task<int> EliminarAsync(string idDependencia);
}
