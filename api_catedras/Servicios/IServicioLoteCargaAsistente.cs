// ==========================================================================
//  Servicios/IServicioLoteCargaAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de negocio de 'LoteCargaAsistente': listar, obtener, abrir y
//  cerrar. Sin reemplazar, actualizar ni eliminar (RF117).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'LoteCargaAsistente'.</summary>
public interface IServicioLoteCargaAsistente
{
    Task<(List<LoteCargaAsistente> Datos, int Total)> ListarAsync(int? limite);
    Task<LoteCargaAsistente> ObtenerAsync(long idLoteCarga);

    /// <summary>Abre un lote nuevo (contadores en 0).</summary>
    Task<LoteCargaAsistente> CrearAsync(LoteCargaAsistenteCrear peticion);

    /// <summary>Cierra el lote. 400 si los contadores no cuadran, si faltan
    /// novedades o si ya estaba cerrado; 404 si no existe.</summary>
    Task<LoteCargaAsistente> CerrarAsync(long idLoteCarga, LoteCargaAsistenteCerrar peticion);
}
