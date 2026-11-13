// ==========================================================================
//  Servicios/ServicioExportacion.cs  ·  Subversión v7.2 · RF-S13 / RF-S14
// --------------------------------------------------------------------------
//  Capa de negocio de las consultas de exportación (solo lectura). Valida los
//  parámetros de consulta (-> ArgumentException -> 400) y delega en el
//  repositorio. No arma CSV: eso es del front (módulos 'csv' y 'zipfile').
// ==========================================================================

namespace ApiCatedras.Servicios;

using System.Text.RegularExpressions;
using ApiCatedras.Modelos;
using ApiCatedras.Repositorios;

public interface IServicioExportacion
{
    Task<List<ExportacionCatedra>> CatedrasAsync(string? periodo);

    /// <summary>Una página de cátedras con sus asistentes. 'siguiente' = desde dónde pedir la próxima (null = no hay más).</summary>
    Task<(List<ExportacionCatedraAsistentes> Datos, string? Siguiente)> AsistentesAsync(string? periodo, string? desde, int? limite);
}

public class ServicioExportacion : IServicioExportacion
{
    // Cuántas cátedras trae una página si el cliente no dice (la función SQL acepta de 1 a 500).
    public const int LimitePorDefecto = 100;
    public const int LimiteMaximo = 500;

    // 'desde' es un id_evento_asis: hasta 9 dígitos.
    private static readonly Regex SoloDigitos = new("^[0-9]{1,9}$", RegexOptions.Compiled);

    private readonly IRepositorioExportacion _repositorio;

    public ServicioExportacion(IRepositorioExportacion repositorio)
    {
        _repositorio = repositorio;
    }

    public Task<List<ExportacionCatedra>> CatedrasAsync(string? periodo) =>
        _repositorio.CatedrasDelPeriodoAsync(ExigirPeriodo(periodo));

    public async Task<(List<ExportacionCatedraAsistentes> Datos, string? Siguiente)> AsistentesAsync(
        string? periodo, string? desde, int? limite)
    {
        string periodoValido = ExigirPeriodo(periodo);
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo is < 1 or > LimiteMaximo)
            throw new ArgumentException($"El parámetro 'limite' debe estar entre 1 y {LimiteMaximo}.");

        string? desdeValido = string.IsNullOrWhiteSpace(desde) ? null : desde.Trim();
        if (desdeValido is not null && !SoloDigitos.IsMatch(desdeValido))
            throw new ArgumentException("El parámetro 'desde' debe ser un id de cátedra (hasta 9 dígitos).");

        var datos = await _repositorio.AsistentesDelPeriodoAsync(periodoValido, desdeValido, limiteEfectivo);

        // Página llena = puede haber más: se entrega el último id para pedir la siguiente. Página corta = fin.
        string? siguiente = datos.Count == limiteEfectivo ? datos[^1].IdCatedra : null;
        return (datos, siguiente);
    }

    // El periodo es obligatorio (p. ej. 2026-2; la columna es char(6)).
    private static string ExigirPeriodo(string? periodo)
    {
        string valor = (periodo ?? "").Trim();

        if (valor.Length is 0 or > 6)
            throw new ArgumentException("El parámetro 'periodo' es obligatorio (por ejemplo 2026-2).");

        return valor;
    }
}
