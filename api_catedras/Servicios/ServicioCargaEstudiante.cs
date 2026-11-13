// ==========================================================================
//  Servicios/ServicioCargaEstudiante.cs  ·  Subversión v7.2 · RF-S8
// --------------------------------------------------------------------------
//  Capa de negocio de la carga de estudiantes. Las reglas pesadas (carnet
//  existente, CC de otra persona, un solo programa activo por periodo) viven
//  en la base de datos; aquí solo se normalizan los textos.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

public interface IServicioCargaEstudiante
{
    Task<ResultadoCargaEstudiante> CargarAsync(EstudianteCargar peticion);
}

public class ServicioCargaEstudiante : IServicioCargaEstudiante
{
    private readonly IRepositorioCargaEstudiante _repositorio;

    public ServicioCargaEstudiante(IRepositorioCargaEstudiante repositorio)
    {
        _repositorio = repositorio;
    }

    public Task<ResultadoCargaEstudiante> CargarAsync(EstudianteCargar peticion)
    {
        // La petición ya pasó la validación de forma: aquí los campos obligatorios no son null.
        // El código de programa va en mayúsculas (los códigos del catálogo lo están).
        return _repositorio.CargarAsync(
            peticion.IdAsis!.Trim(),
            peticion.Documento!.Trim(),
            peticion.Programa!.Trim().ToUpperInvariant(),
            peticion.Periodo!.Trim(),
            peticion.FkLoteCarga);
    }
}
