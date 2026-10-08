// ==========================================================================
//  Servicios/IServicioProgramaAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'ProgramaAsistente' (ADR D16): listar, crear y
//  cambiar estado. Sin obtener-por-id, reemplazar, actualizar ni eliminar.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'ProgramaAsistente'.</summary>
public interface IServicioProgramaAsistente
{
    Task<(List<ProgramaAsistente> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite);

    /// <summary>Crea una matrícula. Devuelve la fila creada (con su estado efectivo).</summary>
    Task<ProgramaAsistente> CrearAsync(ProgramaAsistenteCrear peticion);

    /// <summary>Cambia el estado. 404 si la llave no existe; 400 si ya tenía ese estado.</summary>
    Task<ProgramaAsistente> CambiarEstadoAsync(ProgramaAsistenteCambiarEstado peticion);
}
