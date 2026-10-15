// ==========================================================================
//  Repositorios/IRepositorioClaveAcceso.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.clave_acceso'.
//
//  NO es un CRUD (ADR D24): dos lecturas (para auditoría) y dos operaciones
//  de negocio que llaman a FUNCIONES de la BD que ya existían desde la v1.
//  No hay Crear, Reemplazar, Actualizar ni Eliminar.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre 'catedras.clave_acceso'.</summary>
public interface IRepositorioClaveAcceso
{
    /// <summary>Claves, hasta 'limite'. Con 'fkAsistente', solo las de ese asistente.
    /// Nunca incluye el hash de la clave.</summary>
    Task<List<ClaveAcceso>> ObtenerTodosAsync(long? fkAsistente, int limite);

    Task<ClaveAcceso?> ObtenerPorIdAsync(long idClave);

    /// <summary>SELECT * FROM fn_solicitar_clave_api. Devuelve (id_clave, clave EN TEXTO
    /// PLANO, correo de destino). Es la única vez que la clave existe en claro (RNF12).
    /// Asistente no resuelto o enlace inválido -> NpgsqlException (500, D25).</summary>
    Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(
        string identificador, Guid token, string? ip);

    /// <summary>SELECT * FROM fn_validar_clave_api. Devuelve el id_registro creado.
    /// Clave incorrecta/usada/expirada, sin cupo o sin vinculación -> NpgsqlException (500, D25).</summary>
    Task<long> ValidarAsync(long idClave, string clave, string? ip, string? userAgent);
}
