// ==========================================================================
//  Repositorios/IRepositorioExportacion.cs  ·  Subversión v7.2 · RF-S13 / RF-S14
// --------------------------------------------------------------------------
//  Contrato de las consultas de la exportación de fin de periodo. Solo lectura:
//  dos funciones de la migración 066 (las reglas —periodo por sesiones, sin
//  repetir personas, clasificación por vinculación— viven en la base de datos).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

public interface IRepositorioExportacion
{
    /// <summary>Cátedras con al menos una sesión en el periodo, con sus conteos por tipo.</summary>
    Task<List<ExportacionCatedra>> CatedrasDelPeriodoAsync(string periodo);

    /// <summary>
    /// Asistentes por cátedra, PAGINADO por cátedra: 'desde' es el último id_catedra ya recibido
    /// (null = desde el principio) y 'limite' cuántas cátedras trae la página.
    /// </summary>
    Task<List<ExportacionCatedraAsistentes>> AsistentesDelPeriodoAsync(string periodo, string? desde, int limite);
}
