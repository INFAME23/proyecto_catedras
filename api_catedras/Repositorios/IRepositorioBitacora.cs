// ==========================================================================
//  Repositorios/IRepositorioBitacora.cs  ·  Proyecto Cátedras · Versión 7
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.bitacora' — SOLO LECTURA
//  (ADR D36 y D37-A de version7/4_research.md).
//
//  La bitácora es el log de auditoría: la escriben los triggers de la BD,
//  nunca la API. Por eso esta interfaz no declara ningún método de
//  escritura (Crear / Actualizar / Eliminar). Una prueba por reflexión de
//  pruebas/Programa.cs falla si alguno reaparece.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Lectura de la tabla 'catedras.bitacora'.</summary>
public interface IRepositorioBitacora
{
    /// <summary>Devuelve las entradas más recientes primero, hasta 'limite'
    /// filas. 'nombreTabla' nulo = de todas las tablas.</summary>
    Task<List<Bitacora>> ObtenerTodosAsync(string? nombreTabla, int limite);

    /// <summary>Devuelve la entrada con ese id, o null si no existe.</summary>
    Task<Bitacora?> ObtenerPorIdAsync(long idBitacora);
}
