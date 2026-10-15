// ==========================================================================
//  Servicios/IServicioClaveAcceso.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Contrato de negocio de 'ClaveAcceso' (ADR D24): dos lecturas de auditoría
//  y dos verbos de negocio, SOLICITAR y VALIDAR.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'ClaveAcceso'.</summary>
public interface IServicioClaveAcceso
{
    Task<(List<ClaveAcceso> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite);

    Task<ClaveAcceso> ObtenerAsync(long idClave);

    /// <summary>Pide una clave. Devuelve (id_clave, clave EN TEXTO PLANO, correo). Es la
    /// única vez que la clave existe en claro (RNF12). 'token' o 'ip' mal escritos
    /// -> ArgumentException (400).</summary>
    Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(ClaveAccesoSolicitar peticion);

    /// <summary>Valida la clave y registra la asistencia. Devuelve el id_registro creado.
    /// 'ip' mal escrita -> ArgumentException (400).</summary>
    Task<long> ValidarAsync(ClaveAccesoValidar peticion);
}
