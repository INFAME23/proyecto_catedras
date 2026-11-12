// ==========================================================================
//  Servicios/IServicioSolicitudPonente.cs  ·  Proyecto Cátedras · v7.1 · D56
// --------------------------------------------------------------------------
//  Contrato de negocio de la bandeja de ponentes: listar, consultar, recibir
//  una postulación y resolverla (aprobar o rechazar).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'SolicitudPonente'.</summary>
public interface IServicioSolicitudPonente
{
    /// <summary>'estado' vacío = todas; si trae valor debe ser PENDIENTE, APROBADA o RECHAZADA (ArgumentException).</summary>
    Task<(List<SolicitudPonente> Datos, int Total)> ListarAsync(string? estado, int? limite);

    /// <summary>La solicitud. Inexistente -> NoEncontradoExcepcion (404).</summary>
    Task<SolicitudPonente> ObtenerAsync(long idSolicitud);

    /// <summary>Recibe la postulación (texto ya recortado) y la deja PENDIENTE.</summary>
    Task<SolicitudPonente> CrearAsync(SolicitudPonenteCrear peticion);

    /// <summary>Aprueba o rechaza. Rechazar sin motivo -> ArgumentException (400); inexistente -> 404.</summary>
    Task<SolicitudPonente> ResolverAsync(long idSolicitud, SolicitudPonenteResolver peticion);
}
