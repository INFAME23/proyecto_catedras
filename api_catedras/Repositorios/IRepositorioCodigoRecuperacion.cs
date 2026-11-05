// ==========================================================================
//  Repositorios/IRepositorioCodigoRecuperacion.cs  ·  Cátedras · Subversión v7.1
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.codigo_recuperacion' (migración
//  047). Como 'ClaveAcceso' (ADR D24) NO es un CRUD: dos operaciones de
//  negocio que llaman a procedimientos de la BD. Las reglas (vigencia,
//  intentos, un solo uso) las decide la BD; aquí no se repiten.
//
//  A diferencia de ClaveAcceso (D25), un código malo NO sube como excepción: la
//  BD devuelve un estado, para que los intentos fallidos se guarden (RF153).
//
//  Igual que IRepositorioUsuario, no sabe qué es BCrypt: 'claveHash' entra
//  como cadena opaca, ya calculada por ServicioUsuario.
// ==========================================================================

namespace ApiCatedras.Repositorios;

/// <summary>Operaciones de datos sobre 'catedras.codigo_recuperacion'.</summary>
public interface IRepositorioCodigoRecuperacion
{
    /// <summary>CALL sp_solicitar_recuperacion. Devuelve null si no hay una cuenta activa
    /// con ese usuario o correo; si la hay, (id_codigo, código EN TEXTO PLANO, correo de
    /// destino, nombre de usuario). Es la única vez que el código existe en claro (RNF12).</summary>
    Task<(long IdCodigo, string Codigo, string EnviadoA, string Usuario)?> SolicitarAsync(
        string identificador, string? ip);

    /// <summary>CALL sp_restablecer_clave. Devuelve el ESTADO que decidió la BD:
    /// "OK" (la clave cambió) o el motivo del rechazo: NO_EXISTE, USADO, EXPIRADO,
    /// INCORRECTO, AGOTADO. No lanza por un código malo: así el contador de
    /// intentos se guarda (ver 047_codigo_recuperacion.sql).</summary>
    Task<string> RestablecerAsync(long idCodigo, string codigo, string claveHash);
}
