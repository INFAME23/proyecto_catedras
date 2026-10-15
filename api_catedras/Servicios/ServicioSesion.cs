// ==========================================================================
//  Servicios/ServicioSesion.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Sesion'.
//
//  LO QUE SÍ HACE este servicio:
//   · RF125 — "fin > inicio": cruza DOS campos, así que vive AQUÍ como
//     ArgumentException -> 400 (mismo criterio que PeriodoAcademico, v1).
//     Se valida en el POST, en el PUT y en el PATCH. En el PATCH, si solo
//     llega una de las dos fechas, se combina con la que ya tiene la fila.
//   · Convierte las fechas a UTC (ADR D21): el cliente manda la hora con su
//     zona; la BD y el modelo trabajan con el instante en UTC.
//
//  LO QUE NO HACE, a propósito (RNF10, ADR D20):
//   · NO calcula 'numero_reunion'. Si el cliente no lo manda, pasa 0 al
//     repositorio y el trigger de la BD pone el consecutivo. Un 'SELECT
//     MAX(...) + 1' desde aquí tendría una condición de carrera que el
//     trigger evita con su 'FOR UPDATE' sobre la cátedra.
//   · NO revisa que existan la cátedra, modalidad, sede, periodo o encuesta
//     (D9): eso lo garantiza la FK del motor (500).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Sesion'.</summary>
public class ServicioSesion : IServicioSesion
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioSesion _repositorio;

    public ServicioSesion(IRepositorioSesion repositorio)
    {
        _repositorio = repositorio;
    }

    // RF125: la única regla de esta versión que cruza dos campos.
    // 'internal' (antes 'private'): ServicioCatedra la reutiliza para las
    // sesiones que nacen con la cátedra — la regla vive en UN solo lugar.
    internal static void ValidarRango(DateTime inicio, DateTime fin)
    {
        if (fin <= inicio)
            throw new ArgumentException(
                "El campo 'fin' debe ser posterior a 'inicio'.");
    }

    public async Task<(List<Sesion> Datos, int Total)> ListarAsync(long? fkCatedra, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkCatedra, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Sesion> ObtenerAsync(long idSesion)
    {
        var sesion = await _repositorio.ObtenerPorIdAsync(idSesion);

        if (sesion is null)
            throw new NoEncontradoExcepcion($"No existe la sesión '{idSesion}'.");

        return sesion;
    }

    public async Task<(long IdSesion, short NumeroReunion, DateTime CreadoEn)> CrearAsync(SesionCrear peticion)
    {
        // '.UtcDateTime': de "14:00 en Bogotá (-05:00)" a "19:00 en UTC".
        // Es el MISMO instante, escrito en la zona que usa la BD (ADR D21).
        DateTime inicio = peticion.Inicio!.Value.UtcDateTime;
        DateTime fin = peticion.Fin!.Value.UtcDateTime;

        // Se corta ANTES de llegar al repositorio: con fechas al revés, la
        // BD ni se entera (criterio de aceptación 4).
        ValidarRango(inicio, fin);

        var sesion = new Sesion
        {
            FkCatedra = peticion.FkCatedra!.Value,
            // Ausente -> 0. NO se calcula aquí: el 0 le dice al trigger de la
            // BD "pon tú el consecutivo de esta cátedra" (ADR D20).
            NumeroReunion = peticion.NumeroReunion ?? 0,
            NumeroReunionAsis = peticion.NumeroReunionAsis,
            Titulo = peticion.Titulo,
            Inicio = inicio,
            Fin = fin,
            FkModalidad = peticion.FkModalidad!,
            FkSede = peticion.FkSede!,
            FkPeriodo = peticion.FkPeriodo!,
            FkEncuesta = peticion.FkEncuesta,
            Lugar = peticion.Lugar,
            EnlaceVirtual = peticion.EnlaceVirtual,
            Cupo = peticion.Cupo,
            Estado = peticion.Estado ?? "PROGRAMADA",
        };

        // Maestro-detalle (Guía IA, Regla 1): si llegó 'enlace', el repositorio
        // emite el primer enlace de registro en el MISMO CALL que crea la
        // sesión ('sp_guardar_sesion'). Ausente -> null -> sesión sin enlace.
        var enlace = peticion.Enlace is null
            ? ((int? MinutosAntes, int? MinutosDespues, long? FkUsuario)?)null
            : (peticion.Enlace.MinutosAntes, peticion.Enlace.MinutosDespues, peticion.Enlace.FkUsuario);

        // Lo que devuelve el repositorio ya es lo que la BD decidió.
        return await _repositorio.CrearAsync(sesion, enlace);
    }

    public async Task<int> ReemplazarAsync(long idSesion, SesionReemplazo peticion)
    {
        DateTime inicio = peticion.Inicio!.Value.UtcDateTime;
        DateTime fin = peticion.Fin!.Value.UtcDateTime;

        ValidarRango(inicio, fin);

        // Reemplazo COMPLETO: todas las columnas, las opcionales en null si
        // no llegaron.
        var datos = new Dictionary<string, object?>
        {
            ["fk_catedra"] = peticion.FkCatedra!.Value,
            ["numero_reunion"] = peticion.NumeroReunion!.Value,
            ["numero_reunion_asis"] = peticion.NumeroReunionAsis,
            ["titulo"] = peticion.Titulo,
            ["inicio"] = inicio,
            ["fin"] = fin,
            ["fk_modalidad"] = peticion.FkModalidad!,
            ["fk_sede"] = peticion.FkSede!,
            ["fk_periodo"] = peticion.FkPeriodo!,
            ["fk_encuesta"] = peticion.FkEncuesta,
            ["lugar"] = peticion.Lugar,
            ["enlace_virtual"] = peticion.EnlaceVirtual,
            ["cupo"] = peticion.Cupo,
            ["estado"] = peticion.Estado!,
        };

        int filas = await _repositorio.ActualizarAsync(idSesion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sesión '{idSesion}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idSesion, SesionActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // RF125 también en el PATCH. Si llega UNA sola fecha, la otra se toma
        // de la fila guardada: así "mover el fin antes del inicio" responde
        // 400 con un mensaje claro, no 500 del CHECK chk_sesion_fechas.
        if (peticion.Inicio is not null || peticion.Fin is not null)
        {
            // Si no existe, ObtenerAsync ya lanza NoEncontradoExcepcion (404).
            var actual = await ObtenerAsync(idSesion);

            // '??' = "si lo de la izquierda es null, usa lo de la derecha".
            DateTime inicio = peticion.Inicio?.UtcDateTime ?? actual.Inicio;
            DateTime fin = peticion.Fin?.UtcDateTime ?? actual.Fin;
            ValidarRango(inicio, fin);
        }

        int filas = await _repositorio.ActualizarAsync(idSesion, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sesión '{idSesion}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idSesion)
    {
        int filas = await _repositorio.EliminarAsync(idSesion);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sesión '{idSesion}'.");

        return filas;
    }
}
