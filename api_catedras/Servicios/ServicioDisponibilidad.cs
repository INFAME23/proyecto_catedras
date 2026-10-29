// ==========================================================================
//  Servicios/ServicioDisponibilidad.cs  ·  Cátedras · Subversión v7.1 · D52
// --------------------------------------------------------------------------
//  Valida los parámetros de la consulta de disponibilidad y delega en la BD.
//  Reglas de FORMA que se revisan aquí (400): el rango debe ser válido
//  (fin > inicio) y no pasarse de un tamaño razonable, para que nadie pida el
//  calendario de años enteros. Qué es un "choque" lo decide la BD.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Repositorios;

public interface IServicioDisponibilidad
{
    /// <summary>Sesiones que chocan con el horario. Rango inválido o mayor de 24 h -> ArgumentException.</summary>
    Task<List<SesionOcupada>> ConflictosAsync(
        DateTimeOffset? inicio, DateTimeOffset? fin, string? fkSede, string? lugar,
        long? fkAsistente, long? excluirSesion);

    /// <summary>Sesiones que tocan el rango. Rango inválido o mayor de 31 días -> ArgumentException.</summary>
    Task<List<SesionOcupada>> AgendaAsync(DateTimeOffset? desde, DateTimeOffset? hasta, string? fkSede, int? limite);
}

public class ServicioDisponibilidad : IServicioDisponibilidad
{
    private const int LimiteAgenda = 500;
    private static readonly TimeSpan MaximoSesion = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaximoAgenda = TimeSpan.FromDays(31);

    private readonly IRepositorioDisponibilidad _repositorio;

    public ServicioDisponibilidad(IRepositorioDisponibilidad repositorio)
    {
        _repositorio = repositorio;
    }

    private static (DateTimeOffset, DateTimeOffset) Rango(DateTimeOffset? a, DateTimeOffset? b, string nombreA, string nombreB, TimeSpan maximo)
    {
        if (a is null || b is null)
            throw new ArgumentException($"Los parámetros '{nombreA}' y '{nombreB}' son obligatorios (p. ej. 2026-10-20T14:00:00-05:00).");
        if (b <= a)
            throw new ArgumentException($"'{nombreB}' debe ser posterior a '{nombreA}'.");
        if (b - a > maximo)
            throw new ArgumentException($"El rango no puede pasar de {(int)maximo.TotalHours} horas.");
        return (a.Value, b.Value);
    }

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    public async Task<List<SesionOcupada>> ConflictosAsync(
        DateTimeOffset? inicio, DateTimeOffset? fin, string? fkSede, string? lugar,
        long? fkAsistente, long? excluirSesion)
    {
        var (i, f) = Rango(inicio, fin, "inicio", "fin", MaximoSesion);
        return await _repositorio.ConflictosAsync(i, f, Limpio(fkSede), Limpio(lugar), fkAsistente, excluirSesion);
    }

    public async Task<List<SesionOcupada>> AgendaAsync(DateTimeOffset? desde, DateTimeOffset? hasta, string? fkSede, int? limite)
    {
        var (d, h) = Rango(desde, hasta, "desde", "hasta", MaximoAgenda);
        int limiteEfectivo = limite ?? LimiteAgenda;
        if (limiteEfectivo <= 0)
            throw new ArgumentException("El parámetro 'limite' debe ser un entero mayor que 0.");
        return await _repositorio.AgendaAsync(d, h, Limpio(fkSede), limiteEfectivo);
    }
}
