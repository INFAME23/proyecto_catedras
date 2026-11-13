// ==========================================================================
//  Repositorios/IRepositorioCargaEstudiante.cs  ·  Subversión v7.2 · RF-S8
// --------------------------------------------------------------------------
//  Contrato de la carga de un estudiante del archivo (CC, carnet, programa).
//  Una sola operación: 'sp_cargar_estudiante' (migración 064) hace todo en una
//  transacción (persona + documento + programa + vinculación, Guía IA Regla 1).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

public interface IRepositorioCargaEstudiante
{
    /// <summary>
    /// Crea o actualiza al estudiante. Lanza la excepción del motor si la fila no
    /// se puede cargar (programa o periodo inexistente, CC de otra persona...).
    /// </summary>
    Task<ResultadoCargaEstudiante> CargarAsync(
        string idAsis, string documento, string programa, string periodo, long? fkLoteCarga);
}
