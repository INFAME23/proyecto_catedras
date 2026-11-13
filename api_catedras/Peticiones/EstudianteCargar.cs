// ==========================================================================
//  Peticiones/EstudianteCargar.cs  ·  Subversión v7.2 · RF-S8
// --------------------------------------------------------------------------
//  Body del POST /api/Asistente/cargar-estudiante: UNA fila del archivo de
//  estudiantes (CC, Carnet_estudiante, Programa) más el periodo que el
//  administrador eligió en la pantalla de carga.
//
//  El archivo no trae nombres, correo ni periodo: por eso esta petición es
//  distinta de AsistenteCrear (que los exige). La persona se crea (o se
//  actualiza) con 'sp_cargar_estudiante' (migración 064), idempotente por carnet.
//
//  Solo validaciones de FORMA (-> 422). Que el programa o el periodo existan,
//  o que la CC ya sea de otra persona, lo decide la base de datos.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Asistente/cargar-estudiante.</summary>
public class EstudianteCargar
{
    /// <summary>Carnet del estudiante (columna 'Carnet_estudiante' del archivo) = asistente.id_asis.</summary>
    [Required(ErrorMessage = "El campo 'id_asis' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z]{5,15}$",
        ErrorMessage = "El campo 'id_asis' debe tener entre 5 y 15 letras o dígitos.")]
    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    /// <summary>Cédula (columna 'CC' del archivo). Se guarda como documento de tipo CC.</summary>
    [Required(ErrorMessage = "El campo 'documento' es obligatorio.")]
    [RegularExpression("^[0-9A-Za-z-]{4,20}$",
        ErrorMessage = "El campo 'documento' debe tener entre 4 y 20 letras, dígitos o guiones.")]
    [JsonPropertyName("documento")]
    public string? Documento { get; set; }

    /// <summary>Código del programa (columna 'Programa' del archivo), ya traducido por el front si era un nombre o alias.</summary>
    [Required(ErrorMessage = "El campo 'programa' es obligatorio.")]
    [StringLength(5, MinimumLength = 1, ErrorMessage = "El campo 'programa' debe tener entre 1 y 5 caracteres.")]
    [JsonPropertyName("programa")]
    public string? Programa { get; set; }

    /// <summary>Periodo académico elegido en la pantalla (Regla 2: sale de un select), p. ej. 2026-2.</summary>
    [Required(ErrorMessage = "El campo 'periodo' es obligatorio.")]
    [StringLength(6, MinimumLength = 1, ErrorMessage = "El campo 'periodo' debe tener entre 1 y 6 caracteres.")]
    [JsonPropertyName("periodo")]
    public string? Periodo { get; set; }

    /// <summary>Lote de carga al que pertenece la fila (opcional).</summary>
    [JsonPropertyName("fk_lote_carga")]
    public long? FkLoteCarga { get; set; }
}
