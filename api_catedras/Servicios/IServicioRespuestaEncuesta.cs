// ==========================================================================
//  Servicios/IServicioRespuestaEncuesta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de negocio de 'RespuestaEncuesta'. Solo lectura + "responder"
//  (ADR D31/D32) — sin método de "cerrar": no existe ese verbo.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'RespuestaEncuesta'.</summary>
public interface IServicioRespuestaEncuesta
{
    Task<(List<RespuestaEncuesta> Datos, int Total)> ListarAsync(long? fkEncuesta, int? limite);
    Task<RespuestaEncuesta> ObtenerAsync(long idRespuesta);
    Task<long> ResponderAsync(RespuestaEncuestaResponder peticion);
}
