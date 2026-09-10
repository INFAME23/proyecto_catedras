// ==========================================================================
//  Servicios/ServicioPeriodoAcademico.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'PeriodoAcademico'. Mismo patrón que
//  ServicioEncuesta: 'fecha_fin > fecha_ini' cruza dos campos -> vive
//  AQUÍ, como ArgumentException -> 400 (3_plan.md §4.8). A diferencia de
//  Encuesta, ninguna fecha tiene valor por defecto: en POST/PUT SIEMPRE
//  llegan las dos (la petición las exige con [Required]), así que el
//  servicio SIEMPRE valida; en PATCH solo cuando el cliente manda las dos
//  juntas — si manda una sola, el CHECK de la BD (chk_periodo_fechas)
//  queda como respaldo.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'PeriodoAcademico'.</summary>
public class ServicioPeriodoAcademico : IServicioPeriodoAcademico
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioPeriodoAcademico _repositorio;

    public ServicioPeriodoAcademico(IRepositorioPeriodoAcademico repositorio)
    {
        _repositorio = repositorio;
    }

    private static void ValidarRango(DateOnly fechaIni, DateOnly fechaFin)
    {
        if (fechaFin <= fechaIni)
            throw new ArgumentException(
                "El campo 'fecha_fin' debe ser posterior a 'fecha_ini'.");
    }

    public async Task<(List<PeriodoAcademico> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<PeriodoAcademico> ObtenerAsync(string idPeriodo)
    {
        var periodo = await _repositorio.ObtenerPorIdAsync(idPeriodo);

        if (periodo is null)
            throw new NoEncontradoExcepcion($"No existe el periodo académico '{idPeriodo}'.");

        return periodo;
    }

    public async Task CrearAsync(PeriodoAcademicoCrear peticion)
    {
        var fechaIni = peticion.FechaIni!.Value;
        var fechaFin = peticion.FechaFin!.Value;

        ValidarRango(fechaIni, fechaFin);

        var periodo = new PeriodoAcademico
        {
            IdPeriodo = peticion.IdPeriodo!,
            FechaIni = fechaIni,
            FechaFin = fechaFin,
            Activo = peticion.Activo ?? true,
        };

        await _repositorio.CrearAsync(periodo);
    }

    public async Task<int> ReemplazarAsync(string idPeriodo, PeriodoAcademicoReemplazo peticion)
    {
        var fechaIni = peticion.FechaIni!.Value;
        var fechaFin = peticion.FechaFin!.Value;

        ValidarRango(fechaIni, fechaFin);

        var datos = new Dictionary<string, object?>
        {
            ["fecha_ini"] = fechaIni,
            ["fecha_fin"] = fechaFin,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idPeriodo, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el periodo académico '{idPeriodo}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string idPeriodo, PeriodoAcademicoActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        if (peticion.FechaIni is not null && peticion.FechaFin is not null)
            ValidarRango(peticion.FechaIni.Value, peticion.FechaFin.Value);

        int filas = await _repositorio.ActualizarAsync(idPeriodo, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el periodo académico '{idPeriodo}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string idPeriodo)
    {
        int filas = await _repositorio.EliminarAsync(idPeriodo);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el periodo académico '{idPeriodo}'.");

        return filas;
    }
}
