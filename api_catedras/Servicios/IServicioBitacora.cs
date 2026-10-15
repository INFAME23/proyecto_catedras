// ==========================================================================
//  Servicios/IServicioBitacora.cs  ·  Proyecto Cátedras · Versión 7
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'Bitacora' — SOLO LECTURA (ADR D36 y
//  D37-A): listar y obtener. Ningún método de escritura: la auditoría la
//  escribe la BD con sus triggers.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;

/// <summary>Consulta del log de auditoría.</summary>
public interface IServicioBitacora
{
    /// <summary>Lista entradas, las más recientes primero. 'limite' nulo ->
    /// 1000; 'limite' &lt;= 0 -> ArgumentException. 'nombreTabla' nulo o
    /// vacío = todas las tablas.</summary>
    Task<(List<Bitacora> Datos, int Total)> ListarAsync(string? nombreTabla, int? limite);

    /// <summary>Devuelve la entrada por id. No existe -> NoEncontradoExcepcion.</summary>
    Task<Bitacora> ObtenerAsync(long idBitacora);
}
