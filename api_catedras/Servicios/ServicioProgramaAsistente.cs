// ==========================================================================
//  Servicios/ServicioProgramaAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'ProgramaAsistente' (ADR D16). Mismo criterio que
//  ServicioVinculacionAsistente, cambiando "cerrar" por "cambiar estado":
//  'CambiarEstadoAsync' revisa ANTES de llamar al repositorio
//   1. que exista la matrícula con esa llave   -> si no, 404
//   2. que el estado nuevo sea distinto        -> si no, 400
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'ProgramaAsistente'.</summary>
public class ServicioProgramaAsistente : IServicioProgramaAsistente
{
    private const int LimitePorDefecto = 1000;

    // DEFAULT de la columna 'estado'.
    private const string EstadoPorDefecto = "ACTIVO";

    private readonly IRepositorioProgramaAsistente _repositorio;

    public ServicioProgramaAsistente(IRepositorioProgramaAsistente repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<ProgramaAsistente> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<ProgramaAsistente> CrearAsync(ProgramaAsistenteCrear peticion)
    {
        var programa = new ProgramaAsistente
        {
            FkAsistente = peticion.FkAsistente!.Value,
            FkPrograma = peticion.FkPrograma!,
            FkPeriodo = peticion.FkPeriodo!,
            Estado = peticion.Estado ?? EstadoPorDefecto,
        };

        await _repositorio.CrearAsync(programa);
        return programa;
    }

    public async Task<ProgramaAsistente> CambiarEstadoAsync(ProgramaAsistenteCambiarEstado peticion)
    {
        long fkAsistente = peticion.FkAsistente!.Value;
        string fkPrograma = peticion.FkPrograma!;
        string fkPeriodo = peticion.FkPeriodo!;
        string estadoNuevo = peticion.Estado!;

        // 1) ¿Existe la matrícula con esa llave exacta?
        var delAsistente = await _repositorio.ObtenerTodosAsync(fkAsistente, LimitePorDefecto);
        var actual = delAsistente.FirstOrDefault(p =>
            p.FkPrograma == fkPrograma && p.FkPeriodo == fkPeriodo);

        if (actual is null)
            throw new NoEncontradoExcepcion(
                $"No existe la matrícula del asistente '{fkAsistente}' en '{fkPrograma}' para el periodo '{fkPeriodo}'.");

        // 2) ¿Hay algo que cambiar?
        if (actual.Estado == estadoNuevo)
            throw new ArgumentException(
                $"La matrícula ya está en estado '{estadoNuevo}'.");

        await _repositorio.CambiarEstadoAsync(fkAsistente, fkPrograma, fkPeriodo, estadoNuevo);

        actual.Estado = estadoNuevo;
        return actual;
    }
}
