// ==========================================================================
//  Servicios/IServicioCatedra.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Contrato de negocio de 'Catedra'.
//  ListarAsync: 'limite' null = TODAS las filas (contrato del catálogo
//  público); 'Total' = total real de la tabla, no solo las devueltas.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Catedra'.</summary>
public interface IServicioCatedra
{
    /// <summary>Lista cátedras. 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<Catedra> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false);

    Task<Catedra> ObtenerAsync(long idCatedra);

    /// <summary>Crea y devuelve (id_catedra, nombre_asis) que calculó la BD.</summary>
    Task<(long IdCatedra, string NombreAsis)> CrearAsync(CatedraCrear peticion);

    Task<int> ReemplazarAsync(long idCatedra, CatedraReemplazo peticion);
    Task<int> ActualizarAsync(long idCatedra, CatedraActualizar peticion);
    Task<int> EliminarAsync(long idCatedra);
}
