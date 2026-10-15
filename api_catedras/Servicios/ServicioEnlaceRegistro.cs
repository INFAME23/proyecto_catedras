// ==========================================================================
//  Servicios/ServicioEnlaceRegistro.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Reglas de negocio de 'EnlaceRegistro'.
//
//  'EmitirAsync' no decide nada: la BD calcula token, url, canal y ventana
//  (ADR D24). El servicio solo pasa los datos.
//
//  'RevocarAsync' revisa, ANTES de pedir el UPDATE al repositorio:
//   1. que el enlace exista            -> si no, 404
//   2. que siga vigente (sin revocar)  -> si no, 400
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'EnlaceRegistro'.</summary>
public class ServicioEnlaceRegistro : IServicioEnlaceRegistro
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioEnlaceRegistro _repositorio;

    public ServicioEnlaceRegistro(IRepositorioEnlaceRegistro repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<EnlaceRegistro> Datos, int Total)> ListarAsync(long? fkSesion, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkSesion, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<EnlaceRegistro> ObtenerAsync(long idEnlace)
    {
        var enlace = await _repositorio.ObtenerPorIdAsync(idEnlace);

        if (enlace is null)
            throw new NoEncontradoExcepcion($"No existe el enlace '{idEnlace}'.");

        return enlace;
    }

    public async Task<long> EmitirAsync(EnlaceRegistroEmitir peticion)
    {
        return await _repositorio.EmitirAsync(
            peticion.FkSesion!.Value,
            peticion.MinutosAntes,
            peticion.MinutosDespues,
            peticion.FkUsuario);
    }

    public async Task<int> RevocarAsync(long idEnlace)
    {
        // 1) ¿Existe?
        var enlace = await _repositorio.ObtenerPorIdAsync(idEnlace);

        if (enlace is null)
            throw new NoEncontradoExcepcion($"No existe el enlace '{idEnlace}'.");

        // 2) ¿Sigue vigente? Un enlace revocado no se "re-revoca".
        if (enlace.RevocadoEn is not null)
            throw new ArgumentException(
                $"El enlace ya estaba revocado (revocado_en: {enlace.RevocadoEn:yyyy-MM-dd HH:mm:ss} UTC).");

        int filas = await _repositorio.RevocarAsync(idEnlace);

        // Si entre la revisión y el UPDATE otra petición lo revocó, el
        // repositorio devuelve 0 (su WHERE exige 'revocado_en IS NULL').
        if (filas == 0)
            throw new ArgumentException("El enlace ya estaba revocado.");

        return filas;
    }
}
