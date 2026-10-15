// ==========================================================================
//  Servicios/ServicioBitacora.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Bitacora'. Mismo patrón que ServicioSede.
//  ⚠️ Es un log de auditoría (ver IRepositorioBitacora): Crear / Reemplazar /
//  Actualizar / Eliminar existen para que los 5 procedimientos tengan
//  llamador, no porque la aplicación deba reescribir la auditoría.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Consulta del log de auditoría.</summary>
public class ServicioBitacora : IServicioBitacora
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioBitacora _repositorio;

    public ServicioBitacora(IRepositorioBitacora repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<Bitacora> Datos, int Total)> ListarAsync(string? nombreTabla, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        // Un filtro vacío ("?nombreTabla=") se trata como "sin filtro".
        string? filtro = string.IsNullOrWhiteSpace(nombreTabla) ? null : nombreTabla.Trim();

        var datos = await _repositorio.ObtenerTodosAsync(filtro, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Bitacora> ObtenerAsync(long idBitacora)
    {
        var entrada = await _repositorio.ObtenerPorIdAsync(idBitacora);

        if (entrada is null)
            throw new NoEncontradoExcepcion($"No existe la entrada de bitácora '{idBitacora}'.");

        return entrada;
    }

    public async Task<Bitacora> CrearAsync(BitacoraCrear peticion)
    {
        // 'ocurrido_en' y 'usuario_bd' los pone la BD; el id y la fecha salen
        // de la fila que devuelve el procedimiento.
        return await _repositorio.CrearAsync(
            peticion.NombreTabla!, peticion.Operacion!, peticion.FkUsuario,
            peticion.Llave, peticion.DatosAntes, peticion.DatosDespues);
    }

    public async Task<int> ReemplazarAsync(long idBitacora, BitacoraReemplazo peticion)
    {
        // Reemplazo COMPLETO: lo que no llega (usuario, llave, datos) queda NULL.
        var datos = new Dictionary<string, object?>
        {
            ["fk_usuario"] = peticion.FkUsuario,
            ["nombre_tabla"] = peticion.NombreTabla!,
            ["operacion"] = peticion.Operacion!,
            ["llave"] = peticion.Llave,
            ["datos_antes"] = peticion.DatosAntes,
            ["datos_despues"] = peticion.DatosDespues,
        };

        int filas = await _repositorio.ActualizarAsync(idBitacora, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la entrada de bitácora '{idBitacora}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idBitacora, BitacoraActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idBitacora, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la entrada de bitácora '{idBitacora}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idBitacora)
    {
        int filas = await _repositorio.EliminarAsync(idBitacora);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la entrada de bitácora '{idBitacora}'.");

        return filas;
    }
}
