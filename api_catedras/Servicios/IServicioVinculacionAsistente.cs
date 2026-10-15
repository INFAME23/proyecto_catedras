// ==========================================================================
//  Servicios/IServicioVinculacionAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'VinculacionAsistente' (ADR D16): listar, abrir
//  y cerrar. Sin obtener-por-id, reemplazar, actualizar ni eliminar.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'VinculacionAsistente'.</summary>
public interface IServicioVinculacionAsistente
{
    Task<(List<VinculacionAsistente> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite);

    /// <summary>Abre una vinculación. Devuelve la fila creada (con su fecha_ini efectiva).</summary>
    Task<VinculacionAsistente> CrearAsync(VinculacionAsistenteCrear peticion);

    /// <summary>Cierra una vinculación. 404 si no existe; 400 si ya estaba cerrada
    /// o si la fecha de cierre es anterior a la de inicio.</summary>
    Task<VinculacionAsistente> CerrarAsync(VinculacionAsistenteCerrar peticion);
}
