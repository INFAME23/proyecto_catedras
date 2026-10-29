// ==========================================================================
//  Servicios/ServicioEncuesta.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Encuesta'. Mismo patrón que ServicioSede: recibe
//  IRepositorioEncuesta por constructor.
//
//  Regla de coherencia de fechas (chk_encuesta_vig en la BD):
//  'vigente_hasta' no puede ser anterior a 'vigente_desde'. Cruza dos
//  campos -> vive AQUÍ, como ArgumentException -> 400 (3_plan.md §4.8). En
//  POST/PUT el servicio SIEMPRE tiene las dos fechas en la mano (llegan o
//  toman su valor por defecto) y valida siempre; en PATCH solo puede
//  validar cuando el cliente manda las dos fechas juntas — si manda una
//  sola, la coherencia con el valor ya guardado la vigila el CHECK de la BD.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Encuesta'.</summary>
public class ServicioEncuesta : IServicioEncuesta
{
    private const int LimitePorDefecto = 1000;
    private const short VersionPorDefecto = 1;

    private readonly IRepositorioEncuesta _repositorio;

    public ServicioEncuesta(IRepositorioEncuesta repositorio)
    {
        _repositorio = repositorio;
    }

    // Regla de negocio compartida: la vigencia no puede terminar antes de empezar.
    private static void ValidarVigencia(DateOnly vigenteDesde, DateOnly? vigenteHasta)
    {
        if (vigenteHasta is not null && vigenteHasta.Value < vigenteDesde)
            throw new ArgumentException(
                "El campo 'vigente_hasta' no puede ser anterior a 'vigente_desde'.");
    }

    public async Task<(List<Encuesta> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Encuesta> ObtenerAsync(long idEncuesta)
    {
        var encuesta = await _repositorio.ObtenerPorIdAsync(idEncuesta);

        if (encuesta is null)
            throw new NoEncontradoExcepcion($"No existe la encuesta '{idEncuesta}'.");

        return encuesta;
    }

    public async Task<long> CrearAsync(EncuestaCrear peticion)
    {
        // Los valores por defecto reflejan los DEFAULT de la columna:
        // version=1, vigente_desde=hoy, activa=true.
        short version = peticion.Version ?? VersionPorDefecto;
        DateOnly vigenteDesde = peticion.VigenteDesde ?? DateOnly.FromDateTime(DateTime.Now);
        bool activa = peticion.Activa ?? true;

        ValidarVigencia(vigenteDesde, peticion.VigenteHasta);

        var encuesta = new Encuesta
        {
            Nombre = peticion.Nombre!,
            Version = version,
            VigenteDesde = vigenteDesde,
            VigenteHasta = peticion.VigenteHasta,
            Activa = activa,
        };

        // Maestro-detalle (Guía IA, Regla 1): preguntas iniciales, cada una con
        // sus opciones (dos niveles). Viajan JUNTO con la encuesta al
        // repositorio: un solo CALL a 'sp_guardar_encuesta'. El rango
        // valor_min/valor_max se valida AQUÍ con la misma regla de
        // ServicioPregunta (400 antes de tocar la BD). 'FkEncuesta' y
        // 'FkPregunta' van en 0: los ids reales los fija el SP.
        var preguntasIniciales = new List<(Pregunta Pregunta, IReadOnlyList<OpcionPregunta> Opciones)>();
        foreach (var (p, i) in (peticion.Preguntas ?? []).Select((elemento, posicion) => (elemento, posicion)))
        {
            try
            {
                ServicioPregunta.ValidarRango(p.ValorMin, p.ValorMax);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Pregunta {i + 1}: {ex.Message}");
            }

            var pregunta = new Pregunta
            {
                FkEncuesta = 0,
                Enunciado = p.Enunciado!,
                Orden = p.Orden!.Value,
                FkTipoPregunta = p.FkTipoPregunta!,
                Obligatoria = p.Obligatoria ?? false,
                ValorMin = p.ValorMin,
                ValorMax = p.ValorMax,
            };
            var opciones = (p.Opciones ?? [])
                .Select(o => new OpcionPregunta
                {
                    FkPregunta = 0,
                    Etiqueta = o.Etiqueta!,
                    Valor = o.Valor,
                    Orden = o.Orden!.Value,
                })
                .ToList();
            preguntasIniciales.Add((pregunta, opciones));
        }

        // [FLUJO 5] El repositorio inserta y devuelve el id que generó la BD.
        return await _repositorio.CrearAsync(encuesta, preguntasIniciales);
    }

    public async Task<int> ReemplazarAsync(long idEncuesta, EncuestaReemplazo peticion)
    {
        DateOnly vigenteDesde = peticion.VigenteDesde!.Value;

        ValidarVigencia(vigenteDesde, peticion.VigenteHasta);

        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["version"] = peticion.Version!.Value,
            ["vigente_desde"] = vigenteDesde,
            ["vigente_hasta"] = peticion.VigenteHasta,
            ["activa"] = peticion.Activa!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idEncuesta, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la encuesta '{idEncuesta}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idEncuesta, EncuestaActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // Solo se puede validar la coherencia cuando llegan LAS DOS fechas
        // juntas en esta petición (3_plan.md §4.8); si llega una sola, el
        // CHECK de la BD queda como respaldo.
        if (peticion.VigenteDesde is not null && peticion.VigenteHasta is not null)
            ValidarVigencia(peticion.VigenteDesde.Value, peticion.VigenteHasta.Value);

        int filas = await _repositorio.ActualizarAsync(idEncuesta, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la encuesta '{idEncuesta}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idEncuesta)
    {
        int filas = await _repositorio.EliminarAsync(idEncuesta);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la encuesta '{idEncuesta}'.");

        return filas;
    }
}
