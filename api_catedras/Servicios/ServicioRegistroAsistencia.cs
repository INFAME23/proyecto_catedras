// ==========================================================================
//  Servicios/ServicioRegistroAsistencia.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Reglas de negocio de 'RegistroAsistencia'.
//
//  LA regla de este servicio (ADR D26): en el alta MANUAL, 'origen' solo
//  puede ser MANUAL o IMPORTADO. El CHECK 'chk_registro_clave' de la BD
//  técnicamente dejaría pasar un origen "QR" con una clave, pero por esta
//  puerta (sin fk_clave) no tiene sentido: los registros por QR o enlace
//  nacen SOLO de ClaveAcceso/validar. Se valida AQUÍ y no con una anotación
//  en la petición, para que la respuesta sea 400 y no 422.
//
//  Todo lo demás (vinculación vigente, ventana, cupo, duplicados) lo vigilan
//  el trigger y las restricciones de la BD; fallan como NpgsqlException (500).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'RegistroAsistencia'.</summary>
public class ServicioRegistroAsistencia : IServicioRegistroAsistencia
{
    private const int LimitePorDefecto = 1000;

    // Orígenes permitidos en el alta manual (D26). Comparación exacta: la BD
    // guarda los códigos en mayúsculas.
    private static readonly HashSet<string> OrigenesManuales = new() { "MANUAL", "IMPORTADO" };

    private readonly IRepositorioRegistroAsistencia _repositorio;

    public ServicioRegistroAsistencia(IRepositorioRegistroAsistencia repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<RegistroAsistencia> Datos, int Total)> ListarAsync(
        long? fkSesion, long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkSesion, fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<RegistroAsistencia> ObtenerAsync(long idRegistro)
    {
        var registro = await _repositorio.ObtenerPorIdAsync(idRegistro);

        if (registro is null)
            throw new NoEncontradoExcepcion($"No existe el registro '{idRegistro}'.");

        return registro;
    }

    public async Task<RegistroAsistencia> CrearAsync(RegistroAsistenciaCrear peticion)
    {
        // D26: se corta ANTES de llegar a la BD.
        if (!OrigenesManuales.Contains(peticion.Origen!))
            throw new ArgumentException(
                "El campo 'origen' debe ser MANUAL o IMPORTADO. " +
                "Los registros por QR o enlace se crean solo con ClaveAcceso/validar.");

        return await _repositorio.CrearAsync(
            peticion.FkSesion!.Value, peticion.FkAsistente!.Value, peticion.Origen!);
    }
}
