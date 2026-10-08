// ==========================================================================
//  Servicios/ServicioDocumentoAsistente.cs  ·  Proyecto Cátedras · v3
// --------------------------------------------------------------------------
//  Reglas de negocio de 'DocumentoAsistente'. No hay reglas que crucen
//  campos: "un solo vigente por persona" depende de OTRAS filas y la
//  vigila el índice único parcial de la BD (ux_documento_vigente, D9).
//  Aquí quedan las reglas comunes: límite > 0, valores por defecto,
//  PATCH no vacío y "no existe" -> 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'DocumentoAsistente'.</summary>
public class ServicioDocumentoAsistente : IServicioDocumentoAsistente
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioDocumentoAsistente _repositorio;

    public ServicioDocumentoAsistente(IRepositorioDocumentoAsistente repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<DocumentoAsistente> Datos, int Total)> ListarAsync(long? fkAsistente, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkAsistente, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<DocumentoAsistente> ObtenerAsync(long idDocumento)
    {
        var documento = await _repositorio.ObtenerPorIdAsync(idDocumento);

        if (documento is null)
            throw new NoEncontradoExcepcion($"No existe el documento '{idDocumento}'.");

        return documento;
    }

    public async Task<DocumentoAsistente> CrearAsync(DocumentoAsistenteCrear peticion)
    {
        var documento = new DocumentoAsistente
        {
            FkAsistente = peticion.FkAsistente!.Value,
            FkTipoDocumento = peticion.FkTipoDocumento!,
            Numero = peticion.Numero!,
            Vigente = peticion.Vigente ?? true,   // DEFAULT true
        };

        // Deconstrucción de tupla: cada valor que devolvió la BD a su variable.
        var (idDocumento, registradoEn) = await _repositorio.CrearAsync(documento);
        documento.IdDocumento = idDocumento;
        documento.RegistradoEn = registradoEn;

        return documento;
    }

    public async Task<int> ReemplazarAsync(long idDocumento, DocumentoAsistenteReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["fk_tipo_documento"] = peticion.FkTipoDocumento!,
            ["numero"] = peticion.Numero!,
            ["vigente"] = peticion.Vigente!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idDocumento, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el documento '{idDocumento}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idDocumento, DocumentoAsistenteActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idDocumento, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el documento '{idDocumento}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idDocumento)
    {
        int filas = await _repositorio.EliminarAsync(idDocumento);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el documento '{idDocumento}'.");

        return filas;
    }
}
