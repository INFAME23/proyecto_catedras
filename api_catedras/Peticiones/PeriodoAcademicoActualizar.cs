// ==========================================================================
//  Peticiones/PeriodoAcademicoActualizar.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Body del PATCH /api/PeriodoAcademico/{idPeriodo}: actualización PARCIAL.
//  Todo opcional; el body vacío es regla de negocio (400) y la decide el
//  SERVICIO. La coherencia de fechas también la valida el servicio, SOLO
//  cuando las DOS llegan juntas aquí (3_plan.md §4.8).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/PeriodoAcademico/{idPeriodo}. Todo opcional.</summary>
public class PeriodoAcademicoActualizar
{
    /// <summary>Nueva fecha de inicio. Si llega.</summary>
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }

    /// <summary>Nueva fecha de fin. Si llega.</summary>
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }

    /// <summary>Nuevo valor de "activo". Si llega: true o false.</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>
    /// Arma el diccionario columna -> valor SOLO con los campos que
    /// llegaron (no null). Lista blanca de columnas.
    /// </summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (FechaIni is not null) datos["fecha_ini"] = FechaIni;
        if (FechaFin is not null) datos["fecha_fin"] = FechaFin;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
