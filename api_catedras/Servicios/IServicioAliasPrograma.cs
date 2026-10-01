// ==========================================================================
//  Servicios/IServicioAliasPrograma.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Contrato de negocio de 'AliasPrograma': 4 operaciones, sin actualizar
//  (mismo criterio que el repositorio, ADR D12).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'AliasPrograma' (solo-anexo).</summary>
public interface IServicioAliasPrograma
{
    /// <summary>Lista alias. 'limite' nulo -> 1000; &lt;= 0 -> ArgumentException.</summary>
    Task<(List<AliasPrograma> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el alias o lanza NoEncontradoExcepcion.</summary>
    Task<AliasPrograma> ObtenerAsync(long idAlias);

    /// <summary>Crea el alias y devuelve la fila completa (con id y fecha de la BD).</summary>
    Task<AliasPrograma> CrearAsync(AliasProgramaCrear peticion);

    /// <summary>Borra el alias. 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> EliminarAsync(long idAlias);
}
