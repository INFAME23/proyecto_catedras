// ==========================================================================
//  Servicios/ServicioLoteCargaAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'LoteCargaAsistente' (ADR D15).
//
//  'CerrarAsync' revisa en este orden:
//   1. Reglas que solo miran el BODY (no necesitan la BD), ANTES de tocar
//      el repositorio — así se prueban sin PostgreSQL (criterio 8):
//        · aceptadas + rechazadas <= leidas                       -> 400
//        · una novedad por cada fila rechazada (RNF8)              -> 400
//   2. Reglas que necesitan la fila guardada:
//        · el lote existe                                          -> 404
//        · el lote no está ya cerrado (un lote no se cierra 2 veces) -> 400
//   3. Solo entonces, el CALL al SP (contadores + novedades, todo o nada).
//  El CHECK chk_lote_carga_filas de la BD sigue siendo la última muralla.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'LoteCargaAsistente'.</summary>
public class ServicioLoteCargaAsistente : IServicioLoteCargaAsistente
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioLoteCargaAsistente _repositorio;

    public ServicioLoteCargaAsistente(IRepositorioLoteCargaAsistente repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<LoteCargaAsistente> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<LoteCargaAsistente> ObtenerAsync(long idLoteCarga)
    {
        var lote = await _repositorio.ObtenerPorIdAsync(idLoteCarga);

        if (lote is null)
            throw new NoEncontradoExcepcion($"No existe el lote de carga '{idLoteCarga}'.");

        return lote;
    }

    public async Task<LoteCargaAsistente> CrearAsync(LoteCargaAsistenteCrear peticion)
    {
        var lote = new LoteCargaAsistente
        {
            NombreArchivo = peticion.NombreArchivo!,
            FkUsuario = null,   // siempre null hasta la v5 (no existe 'usuario')
        };

        var (idLoteCarga, cargadoEn) = await _repositorio.CrearAsync(lote);
        lote.IdLoteCarga = idLoteCarga;
        lote.CargadoEn = cargadoEn;

        return lote;
    }

    public async Task<LoteCargaAsistente> CerrarAsync(long idLoteCarga, LoteCargaAsistenteCerrar peticion)
    {
        int leidas = peticion.FilasLeidas!.Value;
        int aceptadas = peticion.FilasAceptadas!.Value;
        int rechazadas = peticion.FilasRechazadas!.Value;
        var novedadesPeticion = peticion.Novedades ?? [];

        // ---- 1) Reglas del body: NO se toca el repositorio todavía -------
        if (aceptadas + rechazadas > leidas)
            throw new ArgumentException(
                $"Los contadores no cuadran: filas_aceptadas ({aceptadas}) + filas_rechazadas ({rechazadas}) " +
                $"es mayor que filas_leidas ({leidas}).");

        if (novedadesPeticion.Count != rechazadas)
            throw new ArgumentException(
                $"Debe llegar una novedad por cada fila rechazada: filas_rechazadas = {rechazadas}, " +
                $"novedades = {novedadesPeticion.Count}.");

        // ---- 2) Reglas que necesitan la fila guardada --------------------
        var lote = await _repositorio.ObtenerPorIdAsync(idLoteCarga);

        if (lote is null)
            throw new NoEncontradoExcepcion($"No existe el lote de carga '{idLoteCarga}'.");

        if (lote.EstaCerrado)
            throw new ArgumentException(
                $"El lote de carga '{idLoteCarga}' ya estaba cerrado: un lote no se cierra dos veces.");

        // ---- 3) Cierre: contadores + novedades en una sola transacción ---
        lote.FilasLeidas = leidas;
        lote.FilasAceptadas = aceptadas;
        lote.FilasRechazadas = rechazadas;
        lote.Observaciones = peticion.Observaciones;

        // Cada novedad pertenece SIEMPRE al lote que se cierra.
        var novedades = novedadesPeticion
            .Select(n => new NovedadCarga
            {
                FkLoteCarga = idLoteCarga,
                NumeroFila = n.NumeroFila!.Value,
                ContenidoCrudo = n.ContenidoCrudo!,
                Motivo = n.Motivo!,
            })
            .ToList();

        await _repositorio.CerrarAsync(lote, novedades);
        return lote;
    }
}
