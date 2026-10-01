// ==========================================================================
//  Servicios/ServicioProgramaAcademico.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Reglas de negocio de 'ProgramaAcademico'. No tiene reglas que crucen
//  campos: el formato del código y la lista de niveles son de FORMA (los
//  revisa la petición -> 422) y la existencia de la facultad es una FK
//  (la revisa el motor -> 500). Aquí quedan solo las reglas comunes:
//  límite > 0, valores por defecto, PATCH no vacío y "no existe" -> 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'ProgramaAcademico'.</summary>
public class ServicioProgramaAcademico : IServicioProgramaAcademico
{
    private const int LimitePorDefecto = 1000;

    // DEFAULT de la columna 'nivel' en la BD.
    private const string NivelPorDefecto = "PREGRADO";

    private readonly IRepositorioProgramaAcademico _repositorio;

    public ServicioProgramaAcademico(IRepositorioProgramaAcademico repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<ProgramaAcademico> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<ProgramaAcademico> ObtenerAsync(string codigo)
    {
        var programa = await _repositorio.ObtenerPorIdAsync(codigo);

        if (programa is null)
            throw new NoEncontradoExcepcion($"No existe el programa académico '{codigo}'.");

        return programa;
    }

    public async Task CrearAsync(ProgramaAcademicoCrear peticion)
    {
        var programa = new ProgramaAcademico
        {
            Codigo = peticion.Codigo!,
            Nombre = peticion.Nombre!,
            FkFacultad = peticion.FkFacultad,             // opcional
            Nivel = peticion.Nivel ?? NivelPorDefecto,    // DEFAULT 'PREGRADO'
            Activo = peticion.Activo ?? true,             // DEFAULT true
        };

        await _repositorio.CrearAsync(programa);
    }

    public async Task<int> ReemplazarAsync(string codigo, ProgramaAcademicoReemplazo peticion)
    {
        // Reemplazo completo: las 4 columnas, facultad incluida (null si no llegó).
        var datos = new Dictionary<string, object?>
        {
            ["nombre"] = peticion.Nombre!,
            ["fk_facultad"] = peticion.FkFacultad,
            ["nivel"] = peticion.Nivel!,
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(codigo, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el programa académico '{codigo}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(string codigo, ProgramaAcademicoActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(codigo, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el programa académico '{codigo}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(string codigo)
    {
        int filas = await _repositorio.EliminarAsync(codigo);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el programa académico '{codigo}'.");

        return filas;
    }
}
