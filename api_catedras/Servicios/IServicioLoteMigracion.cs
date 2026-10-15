// ==========================================================================
//  Servicios/IServicioLoteMigracion.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Contrato de negocio de 'LoteMigracion': listar, obtener, GENERAR y
//  actualizar (estado/observaciones). Sin reemplazar (PUT) ni eliminar
//  (RF183, RF185).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'LoteMigracion'.</summary>
public interface IServicioLoteMigracion
{
    Task<(List<LoteMigracion> Datos, int Total)> ListarAsync(long? fkSesion, int? limite);
    Task<LoteMigracion> ObtenerAsync(long idLoteMigracion);

    /// <summary>Genera el lote de una sesión (maestro + detalles + sesión MIGRADA).</summary>
    Task<LoteMigracion> GenerarAsync(LoteMigracionGenerar peticion);

    /// <summary>PATCH: 400 si el body no trae campos; 404 si no existe.</summary>
    Task<int> ActualizarAsync(long idLoteMigracion, LoteMigracionActualizar peticion);
}
