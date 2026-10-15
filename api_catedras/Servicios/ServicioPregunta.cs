// ==========================================================================
//  Servicios/ServicioPregunta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Pregunta'.
//
//  LO QUE SÍ HACE este servicio:
//   · §4.3 de version6/3_plan.md — 'valor_min'/'valor_max' cruzan DOS
//     campos: ambos o ninguno, y si vienen, 'valor_max > valor_min' ->
//     ArgumentException (400). Mismo criterio que 'Sesion.fin/inicio' (v4)
//     y 'PeriodoAcademico.fecha_fin/fecha_ini' (v1). El CHECK
//     'chk_pregunta_rango' de la BD es la ÚLTIMA muralla, no la primera.
//   · Retrofit V6 (Guía IA, Regla 1): 'CrearAsync' arma la lista de
//     'OpcionPregunta' a partir de 'Opciones' y la entrega al repositorio
//     JUNTO con la pregunta (maestro-detalle en una sola transacción).
//
//  LO QUE NO HACE, a propósito (D9):
//   · NO revisa que existan la encuesta ni el tipo de pregunta: eso lo
//     garantiza la FK del motor (500).
//   · NO revalida '(fk_encuesta, orden)' único: lo vigila 'uq_pregunta_ord'
//     (500, RF165).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Pregunta'.</summary>
public class ServicioPregunta : IServicioPregunta
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioPregunta _repositorio;

    public ServicioPregunta(IRepositorioPregunta repositorio)
    {
        _repositorio = repositorio;
    }

    // §4.3 de version6/3_plan.md: la única regla de esta versión que cruza
    // dos campos. 'null, null' (ninguno de los dos llegó) también pasa.
    // 'internal' (antes 'private'): ServicioEncuesta la reutiliza para las
    // preguntas que nacen con la encuesta — la regla vive en UN solo lugar.
    internal static void ValidarRango(short? valorMin, short? valorMax)
    {
        if (valorMin is null && valorMax is null)
            return;

        if (valorMin is null || valorMax is null)
            throw new ArgumentException(
                "Los campos 'valor_min' y 'valor_max' deben venir juntos (ambos o ninguno).");

        if (valorMax <= valorMin)
            throw new ArgumentException(
                "El campo 'valor_max' debe ser mayor que 'valor_min'.");
    }

    public async Task<(List<Pregunta> Datos, int Total)> ListarAsync(long? fkEncuesta, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkEncuesta, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Pregunta> ObtenerAsync(long idPregunta)
    {
        var pregunta = await _repositorio.ObtenerPorIdAsync(idPregunta);

        if (pregunta is null)
            throw new NoEncontradoExcepcion($"No existe la pregunta '{idPregunta}'.");

        return pregunta;
    }

    public async Task<long> CrearAsync(PreguntaCrear peticion)
    {
        ValidarRango(peticion.ValorMin, peticion.ValorMax);

        var pregunta = new Pregunta
        {
            FkEncuesta = peticion.FkEncuesta!.Value,
            Enunciado = peticion.Enunciado!,
            Orden = peticion.Orden!.Value,
            FkTipoPregunta = peticion.FkTipoPregunta!,
            Obligatoria = peticion.Obligatoria ?? false,
            ValorMin = peticion.ValorMin,
            ValorMax = peticion.ValorMax,
        };

        // Retrofit V6 (Guía IA, Regla 1): las opciones iniciales que
        // llegaron en el mismo body se convierten en filas de
        // 'opcion_pregunta'. Regla de negocio que vive AQUÍ: una opción
        // inicial pertenece SIEMPRE a la pregunta que se está creando
        // (el cliente no puede elegir otra) — 'FkPregunta' se completa
        // con 0 porque el id real todavía no existe; el repositorio nunca
        // lo usa: el SP lo fija desde 'p_id_pregunta' (ver §4.1 de
        // version6/3_plan.md). '?? []' = sin lista -> sin opciones.
        var opcionesIniciales = (peticion.Opciones ?? [])
            .Select(o => new OpcionPregunta
            {
                FkPregunta = 0,
                Etiqueta = o.Etiqueta!,
                Valor = o.Valor,
                Orden = o.Orden!.Value,
            })
            .ToList();

        // Lo que la BD ya garantiza ('(fk_encuesta, orden)' único, tipo de
        // pregunta que exista) NO se revalida aquí: si falla, el SP
        // revierte TODO.
        return await _repositorio.CrearAsync(pregunta, opcionesIniciales);
    }

    public async Task<int> ReemplazarAsync(long idPregunta, PreguntaReemplazo peticion)
    {
        ValidarRango(peticion.ValorMin, peticion.ValorMax);

        var datos = new Dictionary<string, object?>
        {
            ["enunciado"] = peticion.Enunciado!,
            ["orden"] = peticion.Orden!.Value,
            ["fk_tipo_pregunta"] = peticion.FkTipoPregunta!,
            ["obligatoria"] = peticion.Obligatoria!.Value,
            ["valor_min"] = peticion.ValorMin,
            ["valor_max"] = peticion.ValorMax,
        };

        int filas = await _repositorio.ActualizarAsync(idPregunta, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la pregunta '{idPregunta}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idPregunta, PreguntaActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // Igual criterio que 'Sesion.fin/inicio' (v4): si llega SOLO uno de
        // los dos campos, el otro se toma de la fila guardada, para que un
        // cruce inválido responda 400 con un mensaje claro y no 500 del
        // CHECK 'chk_pregunta_rango'.
        if (peticion.ValorMin is not null || peticion.ValorMax is not null)
        {
            var actual = await ObtenerAsync(idPregunta);   // 404 si no existe

            short? valorMin = peticion.ValorMin ?? actual.ValorMin;
            short? valorMax = peticion.ValorMax ?? actual.ValorMax;
            ValidarRango(valorMin, valorMax);
        }

        int filas = await _repositorio.ActualizarAsync(idPregunta, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la pregunta '{idPregunta}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idPregunta)
    {
        int filas = await _repositorio.EliminarAsync(idPregunta);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la pregunta '{idPregunta}'.");

        return filas;
    }
}
