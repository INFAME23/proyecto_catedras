// ==========================================================================
//  Servicios/ServicioConsentimientoDatos.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'ConsentimientoDatos'. La entidad más simple de la
//  versión: sin reglas que crucen campos. "Una versión de la política por
//  asistente" la vigila la BD (uq_consentimiento_ver -> 500, D9).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'ConsentimientoDatos'.</summary>
public class ServicioConsentimientoDatos : IServicioConsentimientoDatos
{
    private const int LimitePorDefecto = 1000;

    // DEFAULT de la columna 'canal'.
    private const string CanalPorDefecto = "WEB";

    private readonly IRepositorioConsentimientoDatos _repositorio;

    public ServicioConsentimientoDatos(IRepositorioConsentimientoDatos repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<ConsentimientoDatos> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<ConsentimientoDatos> ObtenerAsync(long idConsentimiento)
    {
        var consentimiento = await _repositorio.ObtenerPorIdAsync(idConsentimiento);

        if (consentimiento is null)
            throw new NoEncontradoExcepcion($"No existe el consentimiento '{idConsentimiento}'.");

        return consentimiento;
    }

    public async Task<ConsentimientoDatos> CrearAsync(ConsentimientoDatosCrear peticion)
    {
        var consentimiento = new ConsentimientoDatos
        {
            FkAsistente = peticion.FkAsistente!.Value,
            VersionPolitica = peticion.VersionPolitica!,
            Canal = peticion.Canal ?? CanalPorDefecto,
            Ip = peticion.Ip,
        };

        var (idConsentimiento, aceptadoEn) = await _repositorio.CrearAsync(consentimiento);
        consentimiento.IdConsentimiento = idConsentimiento;
        consentimiento.AceptadoEn = aceptadoEn;

        return consentimiento;
    }
}
