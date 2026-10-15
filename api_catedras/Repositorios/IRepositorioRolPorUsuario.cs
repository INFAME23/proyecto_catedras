// ==========================================================================
//  Repositorios/IRepositorioRolPorUsuario.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.rol_por_usuario'.
//
//  LLAVE COMPUESTA (ADR D16, igual que IRepositorioVinculacionAsistente):
//  solo 3 operaciones propias de la tabla. No hay ObtenerPorIdAsync (no
//  existe una columna 'id'), ni ActualizarAsync, ni EliminarAsync: el
//  historial de roles se abre y se cierra, nunca se reescribe ni se borra.
//  Que esos métodos NO existan aquí es parte de la especificación.
//
//  Se agrega UNA lectura más, ObtenerVigentesAsync, porque el login
//  (ServicioUsuario.LoginAsync) necesita saber qué roles tiene HOY un
//  usuario. Es una consulta de lectura sobre la misma tabla, no una
//  operación nueva sobre la historia.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.rol_por_usuario'.</summary>
public interface IRepositorioRolPorUsuario
{
    /// <summary>Roles, hasta 'limite'. Con 'fkUsuario', solo los de ese usuario.</summary>
    Task<List<RolPorUsuario>> ObtenerTodosAsync(long? fkUsuario, int limite);

    /// <summary>Códigos de los roles VIGENTES (fecha_fin IS NULL) de un usuario,
    /// p. ej. ["ADMIN", "CONSULTA"]. Lista vacía si no tiene ninguno.</summary>
    Task<List<string>> ObtenerVigentesAsync(long fkUsuario);

    /// <summary>Inserta (abre) un rol. Llave repetida o FK inexistente
    /// -> NpgsqlException (500).</summary>
    Task CrearAsync(RolPorUsuario rol);

    /// <summary>Pone 'fecha_fin' a la fila con esa llave, solo si seguía abierta.
    /// Devuelve filas afectadas (0 = no existe o ya estaba cerrada).</summary>
    Task<int> CerrarAsync(long fkUsuario, string fkRol, DateOnly fechaIni, DateOnly fechaFin);
}
