// ==========================================================================
//  Peticiones/AsistenteActualizar.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Body del PATCH /api/Asistente/{idAsistente}: PARCIAL, todo opcional
//  (RF99). Solo columnas propias, sin detalles. Convención v1: null = "no
//  enviado" (para dejar un campo en NULL, usar PUT).
//
//  Aquí el servicio NO puede revisar el cruce 'id_asis'/'es_externo' ni
//  "al menos un correo": solo ve los campos que llegaron, no la fila
//  completa. Esas reglas las vigilan los CHECK de la BD (-> 500).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Asistente/{idAsistente}. Todo opcional.</summary>
public class AsistenteActualizar
{
    [RegularExpression("^[0-9A-Za-z]{5,15}$",
        ErrorMessage = "El campo 'id_asis' debe tener entre 5 y 15 letras o dígitos.")]
    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'nombres' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "El campo 'apellidos' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'nombre_completo_asis' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("nombre_completo_asis")]
    public string? NombreCompletoAsis { get; set; }

    [StringLength(254, ErrorMessage = "El campo 'correo_institucional' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        ErrorMessage = "El campo 'correo_institucional' no tiene formato de correo.")]
    [JsonPropertyName("correo_institucional")]
    public string? CorreoInstitucional { get; set; }

    [StringLength(254, ErrorMessage = "El campo 'correo_personal' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
        ErrorMessage = "El campo 'correo_personal' no tiene formato de correo.")]
    [JsonPropertyName("correo_personal")]
    public string? CorreoPersonal { get; set; }

    [StringLength(20, MinimumLength = 1,
        ErrorMessage = "El campo 'celular' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("celular")]
    public string? Celular { get; set; }

    [JsonPropertyName("es_externo")]
    public bool? EsExterno { get; set; }

    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (IdAsis is not null) datos["id_asis"] = IdAsis;
        if (Nombres is not null) datos["nombres"] = Nombres;
        if (Apellidos is not null) datos["apellidos"] = Apellidos;
        if (NombreCompletoAsis is not null) datos["nombre_completo_asis"] = NombreCompletoAsis;
        if (CorreoInstitucional is not null) datos["correo_institucional"] = CorreoInstitucional;
        if (CorreoPersonal is not null) datos["correo_personal"] = CorreoPersonal;
        if (Celular is not null) datos["celular"] = Celular;
        if (EsExterno is not null) datos["es_externo"] = EsExterno;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
