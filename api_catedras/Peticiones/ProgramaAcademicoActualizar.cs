// ==========================================================================
//  Peticiones/ProgramaAcademicoActualizar.cs  ·  Proyecto Cátedras · v2
// --------------------------------------------------------------------------
//  Body del PATCH /api/ProgramaAcademico/{codigo}: PARCIAL, todo opcional.
//  Cada campo que llega se valida con las mismas reglas de forma.
//  Convención v1: null = "no enviado" (para quitar la facultad, usar PUT).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/ProgramaAcademico/{codigo}. Todo opcional.</summary>
public class ProgramaAcademicoActualizar
{
    [StringLength(150, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre' debe tener entre 1 y 150 caracteres.")]
    [JsonPropertyName("nombre")]
    public string? Nombre { get; set; }

    [StringLength(10, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_facultad' debe tener entre 1 y 10 caracteres.")]
    [JsonPropertyName("fk_facultad")]
    public string? FkFacultad { get; set; }

    [RegularExpression("^(PREGRADO|ESPECIALIZACION|MAESTRIA|DOCTORADO|EDUCACION_CONTINUA|OTRO)$",
        ErrorMessage = "El campo 'nivel' debe ser PREGRADO, ESPECIALIZACION, MAESTRIA, DOCTORADO, EDUCACION_CONTINUA u OTRO.")]
    [JsonPropertyName("nivel")]
    public string? Nivel { get; set; }

    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (Nombre is not null) datos["nombre"] = Nombre;
        if (FkFacultad is not null) datos["fk_facultad"] = FkFacultad;
        if (Nivel is not null) datos["nivel"] = Nivel;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
