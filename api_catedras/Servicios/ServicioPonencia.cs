// ==========================================================================
//  Servicios/ServicioPonencia.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Ponencia' (ADR D19).
//
//  Es un servicio casi "de paso": no hay fechas que cruzar ni valores por
//  calcular. Su única decisión de negocio es el rol por defecto (PONENTE).
//  La llave duplicada y las FK inexistentes las rechaza la BD (D9 -> 500).
//
//  Comparación con ServicioVinculacionAsistente (v3, ADR D16): allá hay que
//  revisar "¿existe? ¿sigue abierta?" antes del UPDATE. Aquí no hay estado
//  intermedio: el DELETE borra o no borra (0 filas -> 404).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Ponencia'.</summary>
public class ServicioPonencia : IServicioPonencia
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioPonencia _repositorio;

    public ServicioPonencia(IRepositorioPonencia repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Ponencia> Datos, int Total)> ListarAsync(long? fkSesion, long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkSesion, fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Ponencia> ObtenerAsync(long fkSesion, long fkAsistente, string rol)
    {
        var ponencia = await _repositorio.ObtenerUnoAsync(fkSesion, fkAsistente, rol);

        if (ponencia is null)
            throw new NoEncontradoExcepcion(
                $"El asistente '{fkAsistente}' no tiene el rol '{rol}' en la sesión '{fkSesion}'.");

        return ponencia;
    }

    public async Task<Ponencia> CrearAsync(PonenciaCrear peticion)
    {
        var ponencia = new Ponencia
        {
            FkSesion = peticion.FkSesion!.Value,
            FkAsistente = peticion.FkAsistente!.Value,
            // Sin rol -> PONENTE (el mismo DEFAULT que tiene la columna).
            Rol = peticion.Rol ?? "PONENTE",
        };

        await _repositorio.CrearAsync(ponencia);
        return ponencia;
    }

    public async Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol)
    {
        int filas = await _repositorio.EliminarAsync(fkSesion, fkAsistente, rol);

        if (filas == 0)
            throw new NoEncontradoExcepcion(
                $"El asistente '{fkAsistente}' no tiene el rol '{rol}' en la sesión '{fkSesion}'.");

        return filas;
    }
}
