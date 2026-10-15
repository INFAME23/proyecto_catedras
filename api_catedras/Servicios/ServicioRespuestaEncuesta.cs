// ==========================================================================
//  Servicios/ServicioRespuestaEncuesta.cs  ·  Proyecto Cátedras · v6
// --------------------------------------------------------------------------
//  Reglas de negocio de 'RespuestaEncuesta'.
//
//  'ResponderAsync' NO revalida (D9, mismo criterio que D25 de v5):
//   · que 'respuestas' no esté vacía (el `[MinLength(1)]` de la petición
//     ya la corta en 422 antes de llegar aquí; si de todos modos llegara
//     vacía, R29 la rechaza en la BD -> 500),
//   · que el registro exista, que tenga encuesta asignada, que no se haya
//     respondido antes (uq_respuesta_registro) ni que falten preguntas
//     obligatorias (RN-26): todo eso lo resuelve
//     'fn_responder_encuesta_api'/'sp_cerrar_encuesta' dentro de la misma
//     transacción — duplicar la validación aquí no agregaría nada que la
//     BD no garantice ya de forma completa.
//
//  SIN método 'CerrarAsync' (ADR D31, igual que en la interfaz del
//  repositorio): "responder" y "cerrar" son una sola operación.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'RespuestaEncuesta'.</summary>
public class ServicioRespuestaEncuesta : IServicioRespuestaEncuesta
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioRespuestaEncuesta _repositorio;

    public ServicioRespuestaEncuesta(IRepositorioRespuestaEncuesta repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<RespuestaEncuesta> Datos, int Total)> ListarAsync(long? fkEncuesta, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkEncuesta, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<RespuestaEncuesta> ObtenerAsync(long idRespuesta)
    {
        var respuesta = await _repositorio.ObtenerPorIdAsync(idRespuesta);

        if (respuesta is null)
            throw new NoEncontradoExcepcion($"No existe la respuesta de encuesta '{idRespuesta}'.");

        return respuesta;
    }

    public async Task<long> ResponderAsync(RespuestaEncuestaResponder peticion)
    {
        // 'peticion.Respuestas' nunca es null aquí: el '[Required]' +
        // '[MinLength(1)]' de la petición ya lo garantizan (422 si no).
        return await _repositorio.ResponderAsync(peticion.FkRegistro!.Value, peticion.Respuestas!);
    }
}
