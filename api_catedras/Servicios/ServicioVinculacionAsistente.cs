// ==========================================================================
//  Servicios/ServicioVinculacionAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'VinculacionAsistente' (ADR D16).
//
//  'CerrarAsync' revisa, ANTES de pedir el UPDATE al repositorio:
//   1. que exista una vinculación con esa llave        -> si no, 404
//   2. que siga abierta ('fecha_fin' todavía null)     -> si no, 400
//   3. que 'fecha_fin' no sea anterior a 'fecha_ini'    -> si no, 400
//  La 3 también la vigila el CHECK chk_vinculacion_fechas de la BD; aquí
//  se adelanta para responder con un mensaje claro (cruce de campos, D10).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'VinculacionAsistente'.</summary>
public class ServicioVinculacionAsistente : IServicioVinculacionAsistente
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioVinculacionAsistente _repositorio;

    public ServicioVinculacionAsistente(IRepositorioVinculacionAsistente repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<VinculacionAsistente> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<VinculacionAsistente> CrearAsync(VinculacionAsistenteCrear peticion)
    {
        var vinculacion = new VinculacionAsistente
        {
            FkAsistente = peticion.FkAsistente!.Value,
            FkTipoVinculacion = peticion.FkTipoVinculacion!,
            // Sin fecha -> hoy.
            FechaIni = peticion.FechaIni ?? DateOnly.FromDateTime(DateTime.Today),
        };

        await _repositorio.CrearAsync(vinculacion);
        return vinculacion;
    }

    public async Task<VinculacionAsistente> CerrarAsync(VinculacionAsistenteCerrar peticion)
    {
        long fkAsistente = peticion.FkAsistente!.Value;
        string fkTipo = peticion.FkTipoVinculacion!;
        DateOnly fechaIni = peticion.FechaIni!.Value;
        DateOnly fechaFin = peticion.FechaFin ?? DateOnly.FromDateTime(DateTime.Today);

        // 1) ¿Existe? Se piden las vinculaciones de ese asistente y se busca
        //    la de la llave exacta. 'FirstOrDefault' devuelve null si ninguna
        //    cumple la condición.
        var delAsistente = await _repositorio.ObtenerTodosAsync(fkAsistente, LimitePorDefecto);
        var actual = delAsistente.FirstOrDefault(v =>
            v.FkTipoVinculacion == fkTipo && v.FechaIni == fechaIni);

        if (actual is null)
            throw new NoEncontradoExcepcion(
                $"No existe la vinculación '{fkTipo}' del asistente '{fkAsistente}' iniciada el {fechaIni:yyyy-MM-dd}.");

        // 2) ¿Sigue abierta? Una vinculación cerrada no se "re-cierra".
        if (actual.FechaFin is not null)
            throw new ArgumentException(
                $"La vinculación ya estaba cerrada (fecha_fin: {actual.FechaFin:yyyy-MM-dd}).");

        // 3) Coherencia de fechas.
        if (fechaFin < fechaIni)
            throw new ArgumentException(
                "El campo 'fecha_fin' no puede ser anterior a 'fecha_ini'.");

        int filas = await _repositorio.CerrarAsync(fkAsistente, fkTipo, fechaIni, fechaFin);

        // Si entre la revisión y el UPDATE otra petición la cerró, el
        // repositorio devuelve 0 (su WHERE exige 'fecha_fin IS NULL').
        if (filas == 0)
            throw new ArgumentException("La vinculación ya estaba cerrada.");

        actual.FechaFin = fechaFin;
        return actual;
    }
}
