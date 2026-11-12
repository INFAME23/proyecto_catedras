// ==========================================================================
//  Servicios/IServicioRestriccionProgramacion.cs  ·  Proyecto Cátedras · v7.1 · D57
// --------------------------------------------------------------------------
//  Contrato de negocio de las ventanas de programación: listar, obtener, crear,
//  reemplazar, eliminar y «¿cabe este horario?».
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'RestriccionProgramacion'.</summary>
public interface IServicioRestriccionProgramacion
{
    Task<(List<RestriccionProgramacion> Datos, int Total)> ListarAsync(bool soloActivas, int? limite);

    /// <summary>Inexistente -> NoEncontradoExcepcion (404).</summary>
    Task<RestriccionProgramacion> ObtenerAsync(long idRestriccion);

    /// <summary>Fechas incoherentes, una sola hora o días repetidos/fuera de 1-7 -> ArgumentException (400).</summary>
    Task<RestriccionProgramacion> CrearAsync(RestriccionProgramacionEscribir peticion);

    Task<RestriccionProgramacion> ReemplazarAsync(long idRestriccion, RestriccionProgramacionEscribir peticion);

    /// <summary>Inexistente -> NoEncontradoExcepcion (404).</summary>
    Task<int> EliminarAsync(long idRestriccion);

    /// <summary>Falta una fecha o fin &lt;= inicio -> ArgumentException (400).</summary>
    Task<ValidacionHorario> ValidarAsync(DateTimeOffset? inicio, DateTimeOffset? fin);
}
