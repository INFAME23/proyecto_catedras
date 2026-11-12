// ==========================================================================
//  Servicios/ServicioSolicitudPonente.cs  ·  Proyecto Cátedras · v7.1 · D56
// --------------------------------------------------------------------------
//  Reglas de negocio de la bandeja de ponentes. Decisiones de este servicio:
//    · Limpia los textos de la postulación (recorta y junta espacios) y el
//      correo (minúsculas) ANTES de guardar, para que "Ana  Pérez " y "Ana Pérez"
//      sean la misma persona.
//    · Exige el motivo al rechazar (la base de datos también lo exige).
//    · v7.1 D58: la postulación propone una CÁTEDRA NUEVA con fecha y hora: el fin debe ser posterior al
//      inicio y no durar más de 24 horas; que la fecha sea futura y quepa en los periodos y horarios de
//      programación lo revisa la base de datos (409).
//    · La postulación trae también la modalidad, la sede y el periodo que el ponente elige; que el periodo
//      esté activo y contenga la fecha, y que la sede esté activa, lo revisa la base de datos (409).
//    · Al APROBAR una solicitud con fecha propuesta exige la modalidad, la sede y el periodo de la
//      cátedra que se va a crear: los del administrador o, si no escribe, los del ponente (la base de datos
//      también lo exige).
//  Lo demás —que aprobar cree al asistente, su documento, la cátedra y la ponencia, que
//  una solicitud resuelta no se resuelva dos veces— lo decide la base de datos
//  en un solo procedimiento (ACID).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;
using System.Text.RegularExpressions;

/// <summary>Reglas de negocio de 'SolicitudPonente'.</summary>
public class ServicioSolicitudPonente : IServicioSolicitudPonente
{
    private const int LimitePorDefecto = 200;
    private static readonly TimeSpan DuracionMaxima = TimeSpan.FromHours(24);

    private static readonly string[] Estados = ["PENDIENTE", "APROBADA", "RECHAZADA"];
    private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);

    private readonly IRepositorioSolicitudPonente _repositorio;

    public ServicioSolicitudPonente(IRepositorioSolicitudPonente repositorio)
    {
        _repositorio = repositorio;
    }

    /// <summary>"  Ana   Pérez " -> "Ana Pérez".</summary>
    private static string Limpio(string? texto) => Espacios.Replace((texto ?? "").Trim(), " ");

    /// <summary>Como Limpio, pero respeta los saltos de línea del resumen (solo recorta los extremos).</summary>
    private static string LimpioConSaltos(string? texto) => (texto ?? "").Trim();

    public async Task<(List<SolicitudPonente> Datos, int Total)> ListarAsync(string? estado, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;
        if (limiteEfectivo <= 0)
            throw new ArgumentException("El parámetro 'limite' debe ser un entero mayor que 0.");

        string? estadoEfectivo = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
        if (estadoEfectivo is not null && !Estados.Contains(estadoEfectivo))
            throw new ArgumentException("El parámetro 'estado' debe ser PENDIENTE, APROBADA o RECHAZADA.");

        var datos = await _repositorio.ObtenerTodosAsync(estadoEfectivo, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<SolicitudPonente> ObtenerAsync(long idSolicitud)
    {
        var solicitud = await _repositorio.ObtenerUnoAsync(idSolicitud);

        if (solicitud is null)
            throw new NoEncontradoExcepcion($"No existe la solicitud de ponente con id '{idSolicitud}'.");

        return solicitud;
    }

    public async Task<SolicitudPonente> CrearAsync(SolicitudPonenteCrear peticion)
    {
        var limpia = new SolicitudPonenteCrear
        {
            Nombres = Limpio(peticion.Nombres),
            Apellidos = Limpio(peticion.Apellidos),
            FkTipoDocumento = peticion.FkTipoDocumento!.Trim().ToUpperInvariant(),
            NumeroDocumento = peticion.NumeroDocumento!.Trim(),
            PerfilProfesional = Limpio(peticion.PerfilProfesional),
            Direccion = Limpio(peticion.Direccion),
            Telefono = Limpio(peticion.Telefono),
            CorreoInstitucional = peticion.CorreoInstitucional!.Trim().ToLowerInvariant(),
            TituloPonencia = Limpio(peticion.TituloPonencia),
            ResumenPonencia = LimpioConSaltos(peticion.ResumenPonencia),
            Inicio = peticion.Inicio,
            Fin = peticion.Fin,
            FkModalidad = peticion.FkModalidad!.Trim().ToUpperInvariant(),
            FkSede = peticion.FkSede!.Trim(),
            FkPeriodo = peticion.FkPeriodo!.Trim(),
        };

        if (limpia.Fin <= limpia.Inicio)
            throw new ArgumentException("La hora de fin debe ser posterior a la de inicio.");
        if (limpia.Fin - limpia.Inicio > DuracionMaxima)
            throw new ArgumentException("La cátedra no puede durar más de 24 horas: ajusta el inicio y el fin.");

        // Las anotaciones ya validaron la forma de los datos CRUDOS; si al recortar
        // algo quedó por debajo del mínimo (p. ej. "   ab   "), se avisa aquí.
        if (limpia.ResumenPonencia.Length < 30)
            throw new ArgumentException("El resumen de la ponencia debe tener al menos 30 caracteres (sin contar espacios sobrantes).");
        if (limpia.TituloPonencia.Length < 5)
            throw new ArgumentException("El título de la ponencia debe tener al menos 5 caracteres (sin contar espacios sobrantes).");

        return await _repositorio.CrearAsync(limpia);
    }

    /// <summary>"  Ana " -> "Ana"; vacío -> null.</summary>
    private static string? Texto(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    public async Task<SolicitudPonente> ResolverAsync(long idSolicitud, SolicitudPonenteResolver peticion)
    {
        bool aprobar = peticion.Aprobar!.Value;
        string? observacion = Texto(peticion.Observacion);

        if (!aprobar && observacion is null)
            throw new ArgumentException("Para rechazar la solicitud hay que escribir el motivo en 'observacion'.");

        var datos = new SolicitudPonenteResolver
        {
            Aprobar = aprobar,
            Observacion = observacion,
            ResueltoPor = peticion.ResueltoPor,
            NombreCatedra = Texto(peticion.NombreCatedra),
            FkTipoEvento = Texto(peticion.FkTipoEvento)?.ToUpperInvariant(),
            IdEventoAsis = Texto(peticion.IdEventoAsis),
            FkModalidad = Texto(peticion.FkModalidad)?.ToUpperInvariant(),
            FkSede = Texto(peticion.FkSede),
            FkPeriodo = Texto(peticion.FkPeriodo),
            Lugar = Texto(peticion.Lugar),
            EnlaceVirtual = Texto(peticion.EnlaceVirtual),
            Inicio = peticion.Inicio,
            Fin = peticion.Fin,
        };

        if (aprobar)
        {
            var solicitud = await _repositorio.ObtenerUnoAsync(idSolicitud)
                ?? throw new NoEncontradoExcepcion($"No existe la solicitud de ponente con id '{idSolicitud}'.");

            // Solo las solicitudes con fecha propuesta crean una cátedra (las anteriores a la 057 se aprueban como antes).
            if (solicitud.InicioPropuesto is not null)
            {
                // Lo que el administrador no escribe se toma de lo que el ponente eligió (solicitudes desde la 058).
                if ((datos.FkModalidad ?? solicitud.FkModalidad) is null || (datos.FkSede ?? solicitud.FkSede) is null
                    || (datos.FkPeriodo ?? solicitud.FkPeriodo?.Trim()) is null)
                    throw new ArgumentException(
                        "Para aprobar hay que indicar la modalidad, la sede y el periodo de la cátedra nueva ('fk_modalidad', 'fk_sede', 'fk_periodo').");

                var inicio = datos.Inicio ?? solicitud.InicioPropuesto;
                var fin = datos.Fin ?? solicitud.FinPropuesto;
                if (fin <= inicio)
                    throw new ArgumentException("La hora de fin debe ser posterior a la de inicio.");
                if (fin - inicio > DuracionMaxima)
                    throw new ArgumentException("La cátedra no puede durar más de 24 horas: ajusta el inicio y el fin.");
            }
        }

        var resuelta = await _repositorio.ResolverAsync(idSolicitud, datos);

        if (resuelta is null)
            throw new NoEncontradoExcepcion($"No existe la solicitud de ponente con id '{idSolicitud}'.");

        return resuelta;
    }
}
