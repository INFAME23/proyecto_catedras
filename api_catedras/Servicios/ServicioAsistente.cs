// ==========================================================================
//  Servicios/ServicioAsistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Asistente'.
//
//  Reglas que CRUZAN campos del mismo body (-> 400, ArgumentException),
//  revisadas aquí antes de tocar la BD (criterio de aceptación 8: se
//  prueban sin PostgreSQL):
//   · Si NO es externo, 'id_asis' es obligatorio (chk_asistente_externo).
//   · Debe llegar al menos un correo (chk_asistente_correo).
//  Los CHECK de la BD siguen siendo la última muralla (p. ej. en un PATCH,
//  donde el servicio no ve la fila completa).
//
//  Maestro-detalle (Guía IA, Regla 1): 'CrearAsync' convierte los 4
//  bloques de detalle del body en Modelos y los entrega al repositorio
//  JUNTO con el asistente. Cuando el repositorio devuelve el id generado,
//  el servicio lo fija como 'FkAsistente' en cada detalle: un detalle
//  inicial pertenece SIEMPRE al asistente que se está creando.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Asistente'.</summary>
public class ServicioAsistente : IServicioAsistente
{
    private const int LimitePorDefecto = 1000;

    // Valores por defecto de las columnas de los detalles (los mismos DEFAULT de la BD).
    private const string EstadoProgramaPorDefecto = "ACTIVO";
    private const string CanalPorDefecto = "WEB";

    // Mientras el asistente no existe, sus detalles aún no tienen dueño:
    // se arman con 0 y el id real se les pone tras el CALL.
    private const long SinAsistenteTodavia = 0;

    private readonly IRepositorioAsistente _repositorio;

    public ServicioAsistente(IRepositorioAsistente repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Asistente> Datos, int Total)> ListarAsync(string? q, int? limite, int? pagina)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;
        int paginaEfectiva = pagina ?? 1;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        if (paginaEfectiva <= 0)
            throw new ArgumentException(
                "El parámetro 'pagina' debe ser un entero mayor que 0.");

        // Página 1 -> salta 0 filas; página 2 -> salta 'limite' filas; ...
        int desplazamiento = (paginaEfectiva - 1) * limiteEfectivo;

        var datos = await _repositorio.ObtenerTodosAsync(q, limiteEfectivo, desplazamiento);
        return (datos, datos.Count);
    }

    public async Task<Asistente> ObtenerAsync(long idAsistente)
    {
        var asistente = await _repositorio.ObtenerPorIdAsync(idAsistente);

        if (asistente is null)
            throw new NoEncontradoExcepcion($"No existe el asistente '{idAsistente}'.");

        return asistente;
    }

    public async Task<Asistente> CrearAsync(AsistenteCrear peticion)
    {
        bool esExterno = peticion.EsExterno ?? false;   // DEFAULT false

        ValidarIdentidad(peticion.IdAsis, esExterno,
                         peticion.CorreoInstitucional, peticion.CorreoPersonal);

        var asistente = new Asistente
        {
            IdAsis = peticion.IdAsis,
            Nombres = peticion.Nombres!,
            Apellidos = peticion.Apellidos!,
            NombreCompletoAsis = peticion.NombreCompletoAsis,
            CorreoInstitucional = peticion.CorreoInstitucional,
            CorreoPersonal = peticion.CorreoPersonal,
            Celular = peticion.Celular,
            EsExterno = esExterno,
            Activo = peticion.Activo ?? true,              // DEFAULT true
            FkLoteCarga = peticion.FkLoteCarga,
        };

        // Los 4 detalles: '?? []' = si el bloque no llegó, lista vacía.
        // Cada uno toma los DEFAULT de su columna si el campo no llegó.
        var documentos = (peticion.Documentos ?? [])
            .Select(d => new DocumentoAsistente
            {
                FkAsistente = SinAsistenteTodavia,
                FkTipoDocumento = d.FkTipoDocumento!,
                Numero = d.Numero!,
                Vigente = d.Vigente ?? true,
            })
            .ToList();

        var vinculaciones = (peticion.Vinculaciones ?? [])
            .Select(v => new VinculacionAsistente
            {
                FkAsistente = SinAsistenteTodavia,
                FkTipoVinculacion = v.FkTipoVinculacion!,
                // Sin fecha -> hoy (DateOnly.FromDateTime recorta la hora).
                FechaIni = v.FechaIni ?? DateOnly.FromDateTime(DateTime.Today),
            })
            .ToList();

        var programas = (peticion.Programas ?? [])
            .Select(p => new ProgramaAsistente
            {
                FkAsistente = SinAsistenteTodavia,
                FkPrograma = p.FkPrograma!,
                FkPeriodo = p.FkPeriodo!,
                Estado = p.Estado ?? EstadoProgramaPorDefecto,
            })
            .ToList();

        // El consentimiento es un solo objeto: null si no llegó.
        ConsentimientoDatos? consentimiento = peticion.Consentimiento is null
            ? null
            : new ConsentimientoDatos
            {
                FkAsistente = SinAsistenteTodavia,
                VersionPolitica = peticion.Consentimiento.VersionPolitica!,
                Canal = peticion.Consentimiento.Canal ?? CanalPorDefecto,
                Ip = peticion.Consentimiento.Ip,
            };

        // UN solo viaje al repositorio: maestro + detalles en la misma
        // transacción. Lo que la BD ya garantiza (FK, únicos, índice de
        // vigente) NO se revalida aquí: si falla, el SP revierte TODO.
        long idAsistente = await _repositorio.CrearAsync(
            asistente, documentos, vinculaciones, programas, consentimiento);

        // Ahora que existe el id, cada detalle queda apuntando a su dueño.
        asistente.IdAsistente = idAsistente;
        documentos.ForEach(d => d.FkAsistente = idAsistente);
        vinculaciones.ForEach(v => v.FkAsistente = idAsistente);
        programas.ForEach(p => p.FkAsistente = idAsistente);
        if (consentimiento is not null)
            consentimiento.FkAsistente = idAsistente;

        return asistente;
    }

    public async Task<int> ReemplazarAsync(long idAsistente, AsistenteReemplazo peticion)
    {
        // En el PUT llegan TODAS las columnas: aquí sí se pueden revisar
        // las mismas reglas cruzadas que en el POST.
        ValidarIdentidad(peticion.IdAsis, peticion.EsExterno!.Value,
                         peticion.CorreoInstitucional, peticion.CorreoPersonal);

        var datos = new Dictionary<string, object?>
        {
            ["id_asis"] = peticion.IdAsis,
            ["nombres"] = peticion.Nombres!,
            ["apellidos"] = peticion.Apellidos!,
            ["nombre_completo_asis"] = peticion.NombreCompletoAsis,
            ["correo_institucional"] = peticion.CorreoInstitucional,
            ["correo_personal"] = peticion.CorreoPersonal,
            ["celular"] = peticion.Celular,
            ["es_externo"] = peticion.EsExterno!.Value,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idAsistente, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el asistente '{idAsistente}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idAsistente, AsistenteActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // Las reglas cruzadas NO se revisan aquí: el PATCH trae solo una
        // parte de la fila. Si el resultado las rompe, el CHECK de la BD
        // lo rechaza (500).
        int filas = await _repositorio.ActualizarAsync(idAsistente, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el asistente '{idAsistente}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idAsistente)
    {
        int filas = await _repositorio.EliminarAsync(idAsistente);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el asistente '{idAsistente}'.");

        return filas;
    }

    // Reglas que cruzan campos del mismo body. 'static' = no usa nada del
    // objeto (ni el repositorio): solo mira los valores que recibe.
    private static void ValidarIdentidad(string? idAsis, bool esExterno,
                                         string? correoInstitucional, string? correoPersonal)
    {
        if (!esExterno && string.IsNullOrWhiteSpace(idAsis))
            throw new ArgumentException(
                "El campo 'id_asis' es obligatorio cuando el asistente no es externo ('es_externo': false).");

        if (string.IsNullOrWhiteSpace(correoInstitucional) && string.IsNullOrWhiteSpace(correoPersonal))
            throw new ArgumentException(
                "Debe llegar al menos un correo: 'correo_institucional' o 'correo_personal'.");
    }
}
