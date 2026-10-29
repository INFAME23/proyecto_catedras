// ==========================================================================
//  Servicios/ServicioInscripcionCatedra.cs  ·  Cátedras · Subversión v7.1 · D50
// --------------------------------------------------------------------------
//  Traduce el ESTADO que devuelve la BD a lo que entiende la API:
//    · INSCRITA / YA_INSCRITA            -> el controlador responde 200 (idempotente).
//    · CATEDRA_NO_DISPONIBLE / PERSONA_NO_DISPONIBLE / NO_INSCRITA -> NoEncontradoExcepcion (404).
//  La regla de fondo (cátedra activa, no duplicar, reactivar) vive en la BD.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

public interface IServicioInscripcionCatedra
{
    /// <summary>Lista inscripciones. 'limite' &lt;= 0 -> ArgumentException. Por defecto solo las ACTIVAS.</summary>
    Task<(List<InscripcionCatedra> Datos, int Total)> ListarAsync(
        long? fkAsistente, long? fkCatedra, bool? soloActivas, int? limite);

    /// <summary>Devuelve true si la inscripción es NUEVA (o se reactivó) y false si ya estaba activa.</summary>
    Task<bool> InscribirAsync(InscripcionCatedraAccion peticion);

    /// <summary>Cancela una inscripción activa. Sin inscripción activa -> NoEncontradoExcepcion.</summary>
    Task CancelarAsync(InscripcionCatedraAccion peticion);
}

public class ServicioInscripcionCatedra : IServicioInscripcionCatedra
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioInscripcionCatedra _repositorio;

    public ServicioInscripcionCatedra(IRepositorioInscripcionCatedra repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<InscripcionCatedra> Datos, int Total)> ListarAsync(
        long? fkAsistente, long? fkCatedra, bool? soloActivas, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException("El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkAsistente, fkCatedra, soloActivas ?? true, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<bool> InscribirAsync(InscripcionCatedraAccion peticion)
    {
        string estado = await _repositorio.InscribirAsync(peticion.FkCatedra!.Value, peticion.FkAsistente!.Value);

        return estado switch
        {
            "INSCRITA" => true,
            "YA_INSCRITA" => false,
            "CATEDRA_NO_DISPONIBLE" => throw new NoEncontradoExcepcion(
                $"No existe una cátedra activa con id '{peticion.FkCatedra}'."),
            "PERSONA_NO_DISPONIBLE" => throw new NoEncontradoExcepcion(
                $"No existe una persona activa con id '{peticion.FkAsistente}'."),
            _ => throw new InvalidOperationException($"Estado inesperado de la inscripción: '{estado}'."),
        };
    }

    public async Task CancelarAsync(InscripcionCatedraAccion peticion)
    {
        string estado = await _repositorio.CancelarAsync(peticion.FkCatedra!.Value, peticion.FkAsistente!.Value);

        if (estado == "NO_INSCRITA")
            throw new NoEncontradoExcepcion("La persona no tiene una inscripción activa en esa cátedra.");
        if (estado != "CANCELADA")
            throw new InvalidOperationException($"Estado inesperado al cancelar: '{estado}'.");
    }
}
