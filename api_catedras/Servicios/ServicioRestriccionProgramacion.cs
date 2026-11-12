// ==========================================================================
//  Servicios/ServicioRestriccionProgramacion.cs  ·  Proyecto Cátedras · v7.1 · D57
// --------------------------------------------------------------------------
//  Reglas de negocio de las ventanas de programación. Este servicio decide:
//    · que las fechas sean coherentes (hasta >= desde),
//    · que las horas vayan JUNTAS (las dos o ninguna) y la de fin sea posterior,
//    · que los días sean de 1 (lunes) a 7 (domingo), sin repetir, y los guarda ordenados.
//  Qué sesiones caben en una ventana lo decide la base de datos (disparador sobre 'sesion').
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'RestriccionProgramacion'.</summary>
public class ServicioRestriccionProgramacion : IServicioRestriccionProgramacion
{
    private const int LimitePorDefecto = 500;

    private readonly IRepositorioRestriccionProgramacion _repositorio;

    public ServicioRestriccionProgramacion(IRepositorioRestriccionProgramacion repositorio)
    {
        _repositorio = repositorio;
    }

    /// <summary>Valida lo que cruza campos y devuelve los datos listos para guardar.</summary>
    private static (string Nombre, DateOnly Desde, DateOnly Hasta, string? HoraDesde, string? HoraHasta, string? Dias, bool Activo)
        Preparar(RestriccionProgramacionEscribir p)
    {
        var desde = p.FechaDesde!.Value;
        var hasta = p.FechaHasta!.Value;
        if (hasta < desde)
            throw new ArgumentException("'fecha_hasta' no puede ser anterior a 'fecha_desde'.");

        string? horaDesde = string.IsNullOrWhiteSpace(p.HoraDesde) ? null : p.HoraDesde.Trim();
        string? horaHasta = string.IsNullOrWhiteSpace(p.HoraHasta) ? null : p.HoraHasta.Trim();
        if ((horaDesde is null) != (horaHasta is null))
            throw new ArgumentException("'hora_desde' y 'hora_hasta' van juntas: escribe las dos o ninguna.");
        if (horaDesde is not null && string.CompareOrdinal(horaHasta, horaDesde) <= 0)
            throw new ArgumentException("'hora_hasta' debe ser posterior a 'hora_desde'.");

        string? dias = null;
        if (p.DiasSemana is { Count: > 0 })
        {
            if (p.DiasSemana.Any(d => d < 1 || d > 7))
                throw new ArgumentException("Los 'dias_semana' van de 1 (lunes) a 7 (domingo).");
            dias = string.Join(",", p.DiasSemana.Distinct().Order());
        }

        // Con los 7 días marcados es lo mismo que no limitar el día.
        if (dias == "1,2,3,4,5,6,7")
            dias = null;

        return (p.Nombre!.Trim(), desde, hasta, horaDesde, horaHasta, dias, p.Activo ?? true);
    }

    public async Task<(List<RestriccionProgramacion> Datos, int Total)> ListarAsync(bool soloActivas, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;
        if (limiteEfectivo <= 0)
            throw new ArgumentException("El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(soloActivas, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<RestriccionProgramacion> ObtenerAsync(long idRestriccion)
    {
        var fila = await _repositorio.ObtenerUnoAsync(idRestriccion);

        if (fila is null)
            throw new NoEncontradoExcepcion($"No existe la restricción de programación con id '{idRestriccion}'.");

        return fila;
    }

    public async Task<RestriccionProgramacion> CrearAsync(RestriccionProgramacionEscribir peticion)
    {
        var d = Preparar(peticion);
        return await _repositorio.CrearAsync(d.Nombre, d.Desde, d.Hasta, d.HoraDesde, d.HoraHasta, d.Dias, d.Activo);
    }

    public async Task<RestriccionProgramacion> ReemplazarAsync(long idRestriccion, RestriccionProgramacionEscribir peticion)
    {
        var d = Preparar(peticion);
        var fila = await _repositorio.ReemplazarAsync(idRestriccion, d.Nombre, d.Desde, d.Hasta, d.HoraDesde, d.HoraHasta, d.Dias, d.Activo);

        if (fila is null)
            throw new NoEncontradoExcepcion($"No existe la restricción de programación con id '{idRestriccion}'.");

        return fila;
    }

    public async Task<int> EliminarAsync(long idRestriccion)
    {
        int filas = await _repositorio.EliminarAsync(idRestriccion);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la restricción de programación con id '{idRestriccion}'.");

        return filas;
    }

    public async Task<ValidacionHorario> ValidarAsync(DateTimeOffset? inicio, DateTimeOffset? fin)
    {
        if (inicio is null || fin is null)
            throw new ArgumentException("Los parámetros 'inicio' y 'fin' son obligatorios (p. ej. 2026-10-20T14:00:00-05:00).");
        if (fin <= inicio)
            throw new ArgumentException("'fin' debe ser posterior a 'inicio'.");

        return await _repositorio.ValidarAsync(inicio.Value, fin.Value);
    }
}
