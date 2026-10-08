// ==========================================================================
//  Repositorios/IRepositorioConsentimientoDatos.cs  ·  Cátedras · v3
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.consentimiento_datos'. SOLO 3
//  operaciones: listar, obtener y crear. SIN ActualizarAsync ni
//  EliminarAsync (RF113): un hecho legal no se reescribe ni se borra.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre la tabla 'catedras.consentimiento_datos'.</summary>
public interface IRepositorioConsentimientoDatos
{
    /// <summary>Consentimientos por id, hasta 'limite'. Con 'fkAsistente', solo los de ese asistente.</summary>
    Task<List<ConsentimientoDatos>> ObtenerTodosAsync(long? fkAsistente, int limite);

    /// <summary>El consentimiento con ese id, o null si no existe.</summary>
    Task<ConsentimientoDatos?> ObtenerPorIdAsync(long idConsentimiento);

    /// <summary>Inserta y devuelve lo que generó la BD (id y fecha de aceptación).</summary>
    Task<(long IdConsentimiento, DateTime AceptadoEn)> CrearAsync(ConsentimientoDatos consentimiento);
}
