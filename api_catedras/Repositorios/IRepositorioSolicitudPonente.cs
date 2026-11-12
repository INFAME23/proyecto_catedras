// ==========================================================================
//  Repositorios/IRepositorioSolicitudPonente.cs  ·  Proyecto Cátedras · v7.1 · D56
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.solicitud_ponente' (la bandeja del
//  ADMIN). Cuatro operaciones: listar, consultar, crear y resolver. No hay
//  editar ni borrar: una solicitud se decide una vez y queda como historia.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Operaciones de datos sobre 'catedras.solicitud_ponente'.</summary>
public interface IRepositorioSolicitudPonente
{
    /// <summary>Solicitudes (las pendientes primero), hasta 'limite'. 'estado' null = todas.</summary>
    Task<List<SolicitudPonente>> ObtenerTodosAsync(string? estado, int limite);

    /// <summary>La solicitud con ese id, o null si no existe.</summary>
    Task<SolicitudPonente?> ObtenerUnoAsync(long idSolicitud);

    /// <summary>Registra la postulación como PENDIENTE. Fecha pasada, fuera de los periodos o ya hay una pendiente igual -> PostgresException P0001;
    /// tipo de documento inexistente -> NpgsqlException.</summary>
    Task<SolicitudPonente> CrearAsync(SolicitudPonenteCrear datos);

    /// <summary>Aprueba o rechaza (sp_resolver_solicitud_ponente). Al aprobar una solicitud con fecha propuesta, la base de datos
    /// crea la cátedra con su sesión (con los datos de 'datos'). null = la solicitud no existe.
    /// Ya resuelta, rechazo sin motivo o faltan datos -> PostgresException P0001; FK inexistente -> NpgsqlException.</summary>
    Task<SolicitudPonente?> ResolverAsync(long idSolicitud, SolicitudPonenteResolver datos);
}
