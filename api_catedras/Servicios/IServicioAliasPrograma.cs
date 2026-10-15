// ==========================================================================
//  Servicios/IServicioAliasPrograma.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Contrato de negocio de 'AliasPrograma': listar, obtener, crear, reemplazar
//  (PUT), actualizar (PATCH) y eliminar. Reemplazar/actualizar se agregaron
//  en el RETROFIT criterio 3 (antes: solo-anexo, ADR D12).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'AliasPrograma'.</summary>
public interface IServicioAliasPrograma
{
    /// <summary>Lista alias. 'limite' nulo -> 1000; &lt;= 0 -> ArgumentException.</summary>
    Task<(List<AliasPrograma> Datos, int Total)> ListarAsync(int? limite);

    /// <summary>Devuelve el alias o lanza NoEncontradoExcepcion.</summary>
    Task<AliasPrograma> ObtenerAsync(long idAlias);

    /// <summary>Crea el alias y devuelve la fila completa (con id y fecha de la BD).</summary>
    Task<AliasPrograma> CrearAsync(AliasProgramaCrear peticion);

    /// <summary>PUT: reemplaza texto, programa y origen. 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> ReemplazarAsync(long idAlias, AliasProgramaReemplazo peticion);

    /// <summary>PATCH: cambia solo lo enviado. Body vacío -> ArgumentException; 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> ActualizarAsync(long idAlias, AliasProgramaActualizar peticion);

    /// <summary>Borra el alias. 0 filas -> NoEncontradoExcepcion.</summary>
    Task<int> EliminarAsync(long idAlias);
}
