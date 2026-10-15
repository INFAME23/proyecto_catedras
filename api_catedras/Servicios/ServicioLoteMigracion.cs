// ==========================================================================
//  Servicios/ServicioLoteMigracion.cs  ·  Proyecto Cátedras · v7 (RETROFIT Fase 4)
// --------------------------------------------------------------------------
//  Reglas de negocio de 'LoteMigracion'. Igual que la v7 en general, casi
//  sin reglas que crucen campos (version7/5_data_model.md §3):
//   · límite > 0                                   -> 400
//   · PATCH sin ningún campo                       -> 400
//   · lote inexistente (obtener / actualizar)      -> 404
//  Lo demás lo vigila la BD (D9): sesión sin registros migrables (RAISE de
//  sp_generar_lote_migracion), estado inexistente (FK) -> 500.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'LoteMigracion'.</summary>
public class ServicioLoteMigracion : IServicioLoteMigracion
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioLoteMigracion _repositorio;

    public ServicioLoteMigracion(IRepositorioLoteMigracion repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<LoteMigracion> Datos, int Total)> ListarAsync(long? fkSesion, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkSesion, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<LoteMigracion> ObtenerAsync(long idLoteMigracion)
    {
        var lote = await _repositorio.ObtenerPorIdAsync(idLoteMigracion);

        if (lote is null)
            throw new NoEncontradoExcepcion($"No existe el lote de migración '{idLoteMigracion}'.");

        return lote;
    }

    public async Task<LoteMigracion> GenerarAsync(LoteMigracionGenerar peticion)
    {
        // Nada que validar aquí: qué registros son migrables (con id_asis,
        // con programa, no externos) lo decide la BD (RN-08/RN-28), y si
        // no hay ninguno, la BD lanza su propia excepción (500, RF181).
        return await _repositorio.CrearAsync(peticion.FkSesion!.Value, peticion.FkUsuario);
    }

    public async Task<int> ActualizarAsync(long idLoteMigracion, LoteMigracionActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idLoteMigracion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el lote de migración '{idLoteMigracion}'.");

        return filas;
    }
}
