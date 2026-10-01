// ==========================================================================
//  Servicios/IServicioProgramaAcademico.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Contrato de negocio de 'ProgramaAcademico'. Mismo molde que la v1:
//  ArgumentException -> 400 · NoEncontradoExcepcion -> 404.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'ProgramaAcademico'.</summary>
public interface IServicioProgramaAcademico
{
    Task<(List<ProgramaAcademico> Datos, int Total)> ListarAsync(int? limite);
    Task<ProgramaAcademico> ObtenerAsync(string codigo);
    Task CrearAsync(ProgramaAcademicoCrear peticion);
    Task<int> ReemplazarAsync(string codigo, ProgramaAcademicoReemplazo peticion);
    Task<int> ActualizarAsync(string codigo, ProgramaAcademicoActualizar peticion);
    Task<int> EliminarAsync(string codigo);
}
