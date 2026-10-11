// ==========================================================================
//  Repositorios/IRepositorioEnlaceRegistro.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.enlace_registro'.
//
//  NO es un CRUD (ADR D24): el repositorio no hace INSERT ni DELETE propios.
//  Emitir llama a una FUNCIÓN de la BD que ya existía desde la v1
//  (fn_emitir_enlace_api); revocar es un UPDATE puntual. Lo que NO aparece
//  aquí (Crear, Reemplazar, Actualizar, Eliminar) es parte de la
//  especificación.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.enlace_registro'.</summary>
public interface IRepositorioEnlaceRegistro
{
    /// <summary>Enlaces, hasta 'limite'. Con 'fkSesion', solo los de esa sesión.</summary>
    Task<List<EnlaceRegistro>> ObtenerTodosAsync(long? fkSesion, int limite);

    /// <summary>Un enlace por su id, o null si no existe.</summary>
    Task<EnlaceRegistro?> ObtenerPorIdAsync(long idEnlace);

    /// <summary>Emite un enlace (SELECT * FROM fn_emitir_enlace_api). Devuelve el
    /// id_enlace nuevo. Revoca el vigente anterior de la sesión (RN-14).
    /// Sesión inexistente -> NpgsqlException (500).</summary>
    Task<long> EmitirAsync(long fkSesion, int? minutosAntes, int? minutosDespues, long? fkUsuario);

    /// <summary>Pone revocado_en = now() solo si seguía vigente.
    /// Devuelve filas afectadas (0 = no existe o ya estaba revocado).</summary>
    Task<int> RevocarAsync(long idEnlace);
}
