// ==========================================================================
//  Servicios/IServicioDetalleMigracion.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Contrato de negocio de 'DetalleMigracion': listar, obtener y actualizar
//  el resultado. Sin crear (nace al generar el lote), reemplazar ni
//  eliminar (RF189).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'DetalleMigracion'.</summary>
public interface IServicioDetalleMigracion
{
    Task<(List<DetalleMigracion> Datos, int Total)> ListarAsync(long? fkLoteMigracion, int? limite);
    Task<DetalleMigracion> ObtenerAsync(long idDetalle);

    /// <summary>PATCH del resultado: 400 si el body no trae campos; 404 si no existe.</summary>
    Task<int> ActualizarAsync(long idDetalle, DetalleMigracionActualizar peticion);
}
