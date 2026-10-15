// ==========================================================================
//  Repositorios/IRepositorioRespuestaEncuesta.cs  ·  Cátedras · v6
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.respuesta_encuesta'.
//
//  Solo lectura + "responder" (RF172-RF174). Un hecho auditable (ADR D32):
//  NO hay Reemplazar, Actualizar ni Eliminar. Y A PROPÓSITO no hay ningún
//  método 'CerrarAsync' (ADR D31): 'fn_responder_encuesta_api' ya llama a
//  'sp_cerrar_encuesta' internamente, en la misma transacción — "responder"
//  y "cerrar" son, para la API, una sola operación.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Operaciones de datos sobre 'catedras.respuesta_encuesta'.</summary>
public interface IRepositorioRespuestaEncuesta
{
    /// <summary>Respuestas, filtradas por encuesta si se da, hasta 'limite' filas.</summary>
    Task<List<RespuestaEncuesta>> ObtenerTodosAsync(long? fkEncuesta, int limite);

    Task<RespuestaEncuesta?> ObtenerPorIdAsync(long idRespuesta);

    /// <summary>Llama a 'fn_responder_encuesta_api': inserta el maestro y TODOS
    /// los 'respuesta_item' del detalle, y además cierra la encuesta (marca
    /// 'completa'), todo en una sola transacción SQL. Devuelve el id de la
    /// respuesta nueva. Lista vacía, registro sin encuesta asignada, registro
    /// ya respondido o falta una pregunta obligatoria -> NpgsqlException (500)
    /// y NO queda nada guardado (ni el maestro ni ningún detalle).</summary>
    Task<long> ResponderAsync(long fkRegistro, IReadOnlyList<ItemRespuesta> respuestas);
}
