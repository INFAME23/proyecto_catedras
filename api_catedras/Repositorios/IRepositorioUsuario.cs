// ==========================================================================
//  Repositorios/IRepositorioUsuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.usuario'. Molde completo (como
//  Catedra en la v2) más dos operaciones propias del login:
//   · 'ObtenerPorLoginAsync': busca por el campo 'usuario' (no por id).
//   · 'ActualizarUltimoAccesoAsync': marca la hora del último login.
//
//  REGLA DE ORO (ADR D23, RNF13): este contrato NO sabe qué es BCrypt.
//  'ClaveHash' entra y sale como una cadena opaca. Quien calcula el hash y
//  quien lo verifica es ServicioUsuario; aquí nunca se ve la clave en texto
//  plano. Por eso ninguna firma de esta interfaz recibe una 'clave'.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.usuario'.</summary>
public interface IRepositorioUsuario
{
    /// <summary>Usuarios ordenados por id, hasta 'limite'.</summary>
    Task<List<Usuario>> ObtenerTodosAsync(int limite);

    /// <summary>El usuario con ese id, o null si no existe.</summary>
    Task<Usuario?> ObtenerPorIdAsync(long idUsuario);

    /// <summary>El usuario con ese nombre de usuario (el campo 'usuario'), o null
    /// si no existe. Trae también el hash, para que el servicio lo verifique.</summary>
    Task<Usuario?> ObtenerPorLoginAsync(string usuario);

    /// <summary>Inserta y devuelve (id_usuario, creado_en) tal como los dejó la BD.
    /// 'usuario' o 'fk_asistente' repetidos, o asistente inexistente
    /// -> NpgsqlException (500).</summary>
    Task<(long IdUsuario, DateTime CreadoEn)> CrearAsync(Usuario usuario);

    /// <summary>Actualiza las columnas de 'datos' (la columna de la clave es
    /// 'clave_hash', ya hasheada por el servicio). Devuelve filas afectadas
    /// (0 = no existe).</summary>
    Task<int> ActualizarAsync(long idUsuario, Dictionary<string, object?> datos);

    /// <summary>Pone 'ultimo_acceso' = ahora. Se llama tras un login exitoso.</summary>
    Task ActualizarUltimoAccesoAsync(long idUsuario);

    /// <summary>Borra. ⚠ 'rol_por_usuario' se borra en CASCADA; las referencias de
    /// 'enlace_registro' y otras tablas pasan a NULL (SET NULL).</summary>
    Task<int> EliminarAsync(long idUsuario);

    /// <summary>v7.1 · D54. Estudiante vigente con ese CORREO INSTITUCIONAL que aún no tiene cuenta:
    /// (id_asistente, id_asis), o null si no hay (otra persona, ya tiene cuenta, no es estudiante vigente...).</summary>
    Task<(long IdAsistente, string IdAsis)?> BuscarEstudianteSinCuentaAsync(string correo);

    /// <summary>v7.1 · D54. Crea la cuenta del estudiante de ese correo (nombre corto sacado del correo, rol
    /// ESTUDIANTE, debe_cambiar_clave = true) con el hash que le llega ya calculado. Null si ya no corresponde.</summary>
    Task<Usuario?> ProvisionarCuentaEstudianteAsync(string correo, string claveHash);

    /// <summary>v7.1 · D54. Marca o desmarca la exigencia de cambiar la clave en el próximo ingreso.</summary>
    Task FijarDebeCambiarClaveAsync(long idUsuario, bool valor);
}
