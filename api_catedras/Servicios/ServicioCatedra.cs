// ==========================================================================
//  Servicios/ServicioCatedra.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Catedra'.
//
//  Diferencia con el resto de servicios (decisión de la v2): sin '?limite='
//  NO se aplica el tope de 1000, se devuelven TODAS las cátedras, porque
//  el catálogo público las pide así (6_contracts.md §9, ADR D13). Y el
//  total es el de la tabla completa (criterio de aceptación 6).
//
//  Las FK (tipo de evento, dependencia) y el formato ASIS NO se validan
//  aquí: el formato es de la petición (422) y las FK del motor (500).
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Catedra'.</summary>
public class ServicioCatedra : IServicioCatedra
{
    private readonly IRepositorioCatedra _repositorio;

    public ServicioCatedra(IRepositorioCatedra repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Catedra> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false)
    {
        // Si llega, debe ser > 0. Si no llega, null = sin límite.
        if (limite is not null && limite <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limite, incluirInactivos);
        int total = await _repositorio.ContarAsync(incluirInactivos);
        return (datos, total);
    }

    public async Task<Catedra> ObtenerAsync(long idCatedra)
    {
        var catedra = await _repositorio.ObtenerPorIdAsync(idCatedra);

        if (catedra is null)
            throw new NoEncontradoExcepcion($"No existe la cátedra '{idCatedra}'.");

        return catedra;
    }

    public async Task<(long IdCatedra, string NombreAsis)> CrearAsync(CatedraCrear peticion)
    {
        var catedra = new Catedra
        {
            IdEventoAsis = peticion.IdEventoAsis!,
            Nombre = peticion.Nombre!,
            FkTipoEvento = peticion.FkTipoEvento!,
            FkDependencia = peticion.FkDependencia,   // opcional
            Activo = peticion.Activo ?? true,
            // NombreAsis NO se asigna: lo calcula la BD (RNF7).
        };

        // Maestro-detalle (Guía IA, Regla 1): las sesiones que llegaron en el
        // mismo body se convierten en filas de 'sesion' y viajan JUNTO con la
        // cátedra al repositorio (un solo CALL a 'sp_guardar_catedra').
        //  · "fin > inicio" se valida AQUÍ, con la misma regla de
        //    ServicioSesion (RF125): con una sesión al revés, la BD ni se
        //    entera y no se crea nada (400).
        //  · 'FkCatedra' va en 0: el id real todavía no existe; el SP lo fija.
        //  · 'NumeroReunion' ausente -> 0: el trigger pone el consecutivo.
        var sesionesIniciales = new List<Sesion>();
        foreach (var (s, i) in (peticion.Sesiones ?? []).Select((elemento, posicion) => (elemento, posicion)))
        {
            DateTime inicio = s.Inicio!.Value.UtcDateTime;
            DateTime fin = s.Fin!.Value.UtcDateTime;
            try
            {
                ServicioSesion.ValidarRango(inicio, fin);
            }
            catch (ArgumentException ex)
            {
                // Se dice CUÁL sesión: el formulario puede traer varias.
                throw new ArgumentException($"Sesión {i + 1}: {ex.Message}");
            }

            sesionesIniciales.Add(new Sesion
            {
                FkCatedra = 0,
                NumeroReunion = s.NumeroReunion ?? 0,
                NumeroReunionAsis = s.NumeroReunionAsis,
                Titulo = s.Titulo,
                Inicio = inicio,
                Fin = fin,
                FkModalidad = s.FkModalidad!,
                FkSede = s.FkSede!,
                FkPeriodo = s.FkPeriodo!,
                FkEncuesta = s.FkEncuesta,
                Lugar = s.Lugar,
                EnlaceVirtual = s.EnlaceVirtual,
                Cupo = s.Cupo,
                Estado = s.Estado ?? "PROGRAMADA",
            });
        }

        return await _repositorio.CrearAsync(catedra, sesionesIniciales);
    }

    public async Task<int> ReemplazarAsync(long idCatedra, CatedraReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["id_evento_asis"] = peticion.IdEventoAsis!,
            ["nombre"] = peticion.Nombre!,
            ["fk_tipo_evento"] = peticion.FkTipoEvento!,
            ["fk_dependencia"] = peticion.FkDependencia,   // null si no llegó
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idCatedra, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la cátedra '{idCatedra}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idCatedra, CatedraActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idCatedra, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la cátedra '{idCatedra}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idCatedra)
    {
        int filas = await _repositorio.EliminarAsync(idCatedra);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la cátedra '{idCatedra}'.");

        return filas;
    }
}
