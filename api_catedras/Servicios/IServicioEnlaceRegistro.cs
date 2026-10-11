// ==========================================================================
//  Servicios/IServicioEnlaceRegistro.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Contrato de negocio de 'EnlaceRegistro' (ADR D24): listar, obtener,
//  EMITIR y REVOCAR. Sin reemplazar, actualizar ni eliminar.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'EnlaceRegistro'.</summary>
public interface IServicioEnlaceRegistro
{
    /// <summary>Lista enlaces. 'limite' &lt;= 0 -> ArgumentException.</summary>
    Task<(List<EnlaceRegistro> Datos, int Total)> ListarAsync(long? fkSesion, int? limite);

    Task<EnlaceRegistro> ObtenerAsync(long idEnlace);

    /// <summary>Emite un enlace y devuelve su id_enlace. Si la sesión ya tenía uno
    /// vigente, la BD lo revoca (RN-14).</summary>
    Task<long> EmitirAsync(EnlaceRegistroEmitir peticion);

    /// <summary>Revoca un enlace. 404 si no existe; 400 si ya estaba revocado.</summary>
    Task<int> RevocarAsync(long idEnlace);
}
