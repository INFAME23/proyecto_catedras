// ==========================================================================
//  Servicios/ServicioDetalleMigracion.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Reglas de negocio de 'DetalleMigracion'. Mismas reglas comunes que
//  NovedadCarga (límite > 0, inexistente -> 404) más las del PATCH (body
//  vacío -> 400). "Un registro no queda ACEPTADO en dos lotes" depende de
//  OTRAS filas: lo vigila el índice único parcial de la BD
//  (ux_detmig_aceptado -> 500, D9), aquí no se simula.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'DetalleMigracion'.</summary>
public class ServicioDetalleMigracion : IServicioDetalleMigracion
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioDetalleMigracion _repositorio;

    public ServicioDetalleMigracion(IRepositorioDetalleMigracion repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<DetalleMigracion> Datos, int Total)> ListarAsync(long? fkLoteMigracion, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkLoteMigracion, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<DetalleMigracion> ObtenerAsync(long idDetalle)
    {
        var detalle = await _repositorio.ObtenerPorIdAsync(idDetalle);

        if (detalle is null)
            throw new NoEncontradoExcepcion($"No existe el detalle de migración '{idDetalle}'.");

        return detalle;
    }

    public async Task<int> ActualizarAsync(long idDetalle, DetalleMigracionActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idDetalle, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el detalle de migración '{idDetalle}'.");

        return filas;
    }
}
