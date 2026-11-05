// ==========================================================================
//  Servicios/ServicioBitacora.cs  ·  Proyecto Cátedras · Versión 7
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Bitacora' (solo lectura, ADR D36 y D37-A). Mismo
//  patrón que ServicioSede: valida el límite, normaliza el filtro y traduce
//  "no existe" a NoEncontradoExcepcion (que el controlador vuelve 404).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Repositorios;

/// <summary>Consulta del log de auditoría.</summary>
public class ServicioBitacora : IServicioBitacora
{
    private const int LimitePorDefecto = 1000;

    // Solo la interfaz: el servicio no sabe qué motor de BD hay detrás.
    private readonly IRepositorioBitacora _repositorio;

    public ServicioBitacora(IRepositorioBitacora repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Bitacora> Datos, int Total)> ListarAsync(string? nombreTabla, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        // Un filtro vacío ("?nombreTabla=") se trata como "sin filtro".
        string? filtro = string.IsNullOrWhiteSpace(nombreTabla) ? null : nombreTabla.Trim();

        var datos = await _repositorio.ObtenerTodosAsync(filtro, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Bitacora> ObtenerAsync(long idBitacora)
    {
        var entrada = await _repositorio.ObtenerPorIdAsync(idBitacora);

        if (entrada is null)
            throw new NoEncontradoExcepcion($"No existe la entrada de bitácora '{idBitacora}'.");

        return entrada;
    }
}
