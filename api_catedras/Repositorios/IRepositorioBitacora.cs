// ==========================================================================
//  Repositorios/IRepositorioBitacora.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.bitacora'.
//
//  ⚠️ 'bitacora' es el log de auditoría: normalmente lo escriben los
//  triggers. A pedido del equipo (todos los procedimientos deben tener
//  llamador) esta interfaz también expone Crear / Actualizar / Eliminar,
//  que llaman a sp_crear / sp_actualizar / sp_eliminar_bitacora. Esos
//  procedimientos NUNCA reciben 'ocurrido_en' ni 'usuario_bd': los pone la
//  BD, así que nadie puede falsear cuándo ni quién (sí puede editar el resto
//  o borrar la fila: ese riesgo se documenta en AJUSTES_PATRON_SEDE_LOTE.md).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using System.Text.Json;

/// <summary>Operaciones sobre la tabla 'catedras.bitacora'.</summary>
public interface IRepositorioBitacora
{
    /// <summary>Devuelve las entradas más recientes primero, hasta 'limite'
    /// filas. 'nombreTabla' nulo = de todas las tablas.</summary>
    Task<List<Bitacora>> ObtenerTodosAsync(string? nombreTabla, int limite);

    /// <summary>Devuelve la entrada con ese id, o null si no existe.</summary>
    Task<Bitacora?> ObtenerPorIdAsync(long idBitacora);

    /// <summary>Inserta y devuelve la fila completa (con id, 'ocurrido_en' y
    /// 'usuario_bd' que puso la BD). Operación fuera de INSERT/UPDATE/DELETE o
    /// usuario inexistente -> NpgsqlException (500).</summary>
    Task<Bitacora> CrearAsync(string nombreTabla, string operacion, long? fkUsuario,
                              string? llave, JsonElement? datosAntes, JsonElement? datosDespues);

    /// <summary>Actualiza las columnas de 'datos' (clave = nombre de columna). Devuelve filas
    /// afectadas (0 = no existe). No toca 'ocurrido_en' ni 'usuario_bd'.</summary>
    Task<int> ActualizarAsync(long idBitacora, Dictionary<string, object?> datos);

    /// <summary>Borra (físico). Devuelve filas eliminadas (0 = no existe).</summary>
    Task<int> EliminarAsync(long idBitacora);
}
