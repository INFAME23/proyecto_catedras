// ==========================================================================
//  Servicios/ServicioClaveAcceso.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Reglas de negocio de 'ClaveAcceso'.
//
//  Este servicio hace MUY poco a propósito (ADR D24): quién es el asistente,
//  si el enlace está vigente, si la clave coincide, si expiró, si ya se usó,
//  si hay cupo... todo eso lo decide la BD (sp_solicitar_clave y
//  sp_validar_clave_y_registrar). Repetirlo aquí sería duplicar reglas y
//  desincronizarlas. Lo único que valida el servicio es la FORMA de dos
//  datos que viajan como texto:
//   · 'token' debe ser un uuid  -> si no, 400
//   · 'ip' (si viene) debe ser una IP válida -> si no, 400
//   · 'latitud' y 'longitud' (RF-S1, v7.1) deben venir en 'validar' -> si no, 400
//
//  Las excepciones de negocio de la BD (D25) NO se capturan aquí: suben como
//  NpgsqlException y el controlador las responde con 500.
// ==========================================================================

namespace ApiCatedras.Servicios;

using System.Net;
using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'ClaveAcceso'.</summary>
public class ServicioClaveAcceso : IServicioClaveAcceso
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioClaveAcceso _repositorio;

    public ServicioClaveAcceso(IRepositorioClaveAcceso repositorio)
    {
        _repositorio = repositorio;
    }

    // Devuelve la IP normalizada (o null si no vino). IP mal escrita -> 400.
    private static string? ValidarIp(string? ip)
    {
        if (ip is null)
            return null;

        if (!IPAddress.TryParse(ip, out var direccion))
            throw new ArgumentException("El campo 'ip' no es una dirección IP válida.");

        return direccion.ToString();
    }

    public async Task<(List<ClaveAcceso> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<ClaveAcceso> ObtenerAsync(long idClave)
    {
        var clave = await _repositorio.ObtenerPorIdAsync(idClave);

        if (clave is null)
            throw new NoEncontradoExcepcion($"No existe la clave '{idClave}'.");

        return clave;
    }

    public async Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(ClaveAccesoSolicitar peticion)
    {
        if (!Guid.TryParse(peticion.Token, out var token))
            throw new ArgumentException("El campo 'token' no es un uuid válido.");

        string? ip = ValidarIp(peticion.Ip);

        return await _repositorio.SolicitarAsync(peticion.Identificador!, token, ip);
    }

    public async Task<long> ValidarAsync(ClaveAccesoValidar peticion)
    {
        string? ip = ValidarIp(peticion.Ip);

        // Latitud y longitud son obligatorias (RF-S1). El 422 de la petición ya
        // las exige; el servicio no asume que viene de ese camino (lo llama
        // también la prueba de capas) y lo vuelve a comprobar.
        if (peticion.Latitud is null || peticion.Longitud is null)
            throw new ArgumentException(
                "Los campos 'latitud' y 'longitud' son obligatorios para registrar la asistencia.");

        return await _repositorio.ValidarAsync(
            peticion.IdClave!.Value, peticion.Clave!,
            peticion.Latitud.Value, peticion.Longitud.Value, ip, peticion.UserAgent);
    }
}
