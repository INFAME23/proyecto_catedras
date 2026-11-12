// ==========================================================================
//  Modelos/RestriccionProgramacion.cs  ·  Proyecto Cátedras · Subversión v7.1 · D57
// --------------------------------------------------------------------------
//  Una VENTANA de programación: el periodo (fechas), las horas y los días de la
//  semana en que se pueden programar sesiones. Fila de 'catedras.restriccion_programacion'
//  más 'texto' (la descripción legible que arma la base de datos).
//
//  Sin ventanas activas no hay restricción. Con alguna, una sesión solo se puede
//  crear o mover a un horario que quepa en una de ellas (disparador de la BD).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una ventana de programación de sesiones.</summary>
public class RestriccionProgramacion
{
    [JsonPropertyName("id_restriccion")]
    public long IdRestriccion { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    /// <summary>Primer día en que se puede programar (inclusive), AAAA-MM-DD.</summary>
    [JsonPropertyName("fecha_desde")]
    public DateOnly FechaDesde { get; set; }

    /// <summary>Último día en que se puede programar (inclusive).</summary>
    [JsonPropertyName("fecha_hasta")]
    public DateOnly FechaHasta { get; set; }

    /// <summary>Hora más temprana de inicio ('HH:mm:ss'); null = sin límite de hora.</summary>
    [JsonPropertyName("hora_desde")]
    public string? HoraDesde { get; set; }

    /// <summary>Hora más tardía de fin; null = sin límite de hora.</summary>
    [JsonPropertyName("hora_hasta")]
    public string? HoraHasta { get; set; }

    /// <summary>Días permitidos, ISO: 1 = lunes … 7 = domingo; null = todos.</summary>
    [JsonPropertyName("dias_semana")]
    public List<int>? DiasSemana { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("creado_en")]
    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>Descripción legible: «Periodo 2026-2»: del 2026-08-03 al 2026-12-05, de 06:00 a 21:00, lun, mar…</summary>
    [JsonPropertyName("texto")]
    public string? Texto { get; set; }
}

/// <summary>Resultado de preguntar si un horario cabe en las ventanas activas.</summary>
public class ValidacionHorario
{
    [JsonPropertyName("permitido")]
    public bool Permitido { get; set; }

    /// <summary>¿Hay ventanas activas? Si no, todo horario es permitido.</summary>
    [JsonPropertyName("hay_restricciones")]
    public bool HayRestricciones { get; set; }

    /// <summary>Por qué no, con las ventanas donde sí se puede; null si es permitido.</summary>
    [JsonPropertyName("mensaje")]
    public string? Mensaje { get; set; }

    [JsonPropertyName("ventanas")]
    public List<VentanaResumen> Ventanas { get; set; } = [];
}

/// <summary>Una ventana activa, en una línea.</summary>
public class VentanaResumen
{
    [JsonPropertyName("id_restriccion")]
    public long IdRestriccion { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("texto")]
    public string Texto { get; set; } = "";
}
