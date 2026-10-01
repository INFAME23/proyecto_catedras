// ==========================================================================
//  Servicios/ServicioAliasPrograma.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Reglas de negocio de 'AliasPrograma'. Sin método de actualizar.
//  En CrearAsync el servicio arma el modelo con los valores por defecto,
//  pide al repositorio el INSERT y completa el modelo con lo que la BD
//  generó (id_alias y creado_en) antes de devolverlo.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'AliasPrograma'.</summary>
public class ServicioAliasPrograma : IServicioAliasPrograma
{
    private const int LimitePorDefecto = 1000;

    // DEFAULT de la columna 'origen'.
    private const string OrigenPorDefecto = "FORMULARIO";

    private readonly IRepositorioAliasPrograma _repositorio;

    public ServicioAliasPrograma(IRepositorioAliasPrograma repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<AliasPrograma> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<AliasPrograma> ObtenerAsync(long idAlias)
    {
        var alias = await _repositorio.ObtenerPorIdAsync(idAlias);

        if (alias is null)
            throw new NoEncontradoExcepcion($"No existe el alias '{idAlias}'.");

        return alias;
    }

    public async Task<AliasPrograma> CrearAsync(AliasProgramaCrear peticion)
    {
        var alias = new AliasPrograma
        {
            TextoNormalizado = peticion.TextoNormalizado!,
            FkPrograma = peticion.FkPrograma!,
            Origen = peticion.Origen ?? OrigenPorDefecto,
        };

        // Deconstrucción de tupla: cada valor que devolvió la BD a su variable.
        var (idAlias, creadoEn) = await _repositorio.CrearAsync(alias);
        alias.IdAlias = idAlias;
        alias.CreadoEn = creadoEn;

        return alias;
    }

    public async Task<int> EliminarAsync(long idAlias)
    {
        int filas = await _repositorio.EliminarAsync(idAlias);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el alias '{idAlias}'.");

        return filas;
    }
}
