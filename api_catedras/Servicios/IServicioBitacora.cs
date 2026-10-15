// ==========================================================================
//  Servicios/IServicioBitacora.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Contrato de la capa de negocio de 'Bitacora'. Listar y obtener; y, a
//  pedido (todos los procedimientos con llamador), crear / reemplazar /
//  actualizar / eliminar. 'ocurrido_en' y 'usuario_bd' nunca los manda el
//  cliente.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del log de auditoría.</summary>
public interface IServicioBitacora
{
    /// <summary>Lista entradas, las más recientes primero. 'limite' nulo ->
    /// 1000; 'limite' &lt;= 0 -> ArgumentException. 'nombreTabla' nulo o
    /// vacío = todas las tablas.</summary>
    Task<(List<Bitacora> Datos, int Total)> ListarAsync(string? nombreTabla, int? limite);

    /// <summary>Devuelve la entrada por id. No existe -> NoEncontradoExcepcion.</summary>
    Task<Bitacora> ObtenerAsync(long idBitacora);

    /// <summary>Crea la entrada y devuelve la fila completa (con id y fecha de la BD).</summary>
    Task<Bitacora> CrearAsync(BitacoraCrear peticion);

    /// <summary>PUT: reemplaza las 6 columnas de negocio. 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> ReemplazarAsync(long idBitacora, BitacoraReemplazo peticion);

    /// <summary>PATCH: cambia solo lo enviado. Body vacío -> ArgumentException; 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> ActualizarAsync(long idBitacora, BitacoraActualizar peticion);

    /// <summary>Borra (físico). 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> EliminarAsync(long idBitacora);
}
