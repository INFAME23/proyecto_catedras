// ==========================================================================
//  Servicios/IServicioDocumentoAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'DocumentoAsistente'. Molde completo.
//  ArgumentException -> 400 · NoEncontradoExcepcion -> 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'DocumentoAsistente'.</summary>
public interface IServicioDocumentoAsistente
{
    Task<(List<DocumentoAsistente> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite);
    Task<DocumentoAsistente> ObtenerAsync(long idDocumento);
    Task<DocumentoAsistente> CrearAsync(DocumentoAsistenteCrear peticion);
    Task<int> ReemplazarAsync(long idDocumento, DocumentoAsistenteReemplazo peticion);
    Task<int> ActualizarAsync(long idDocumento, DocumentoAsistenteActualizar peticion);
    Task<int> EliminarAsync(long idDocumento);
}
