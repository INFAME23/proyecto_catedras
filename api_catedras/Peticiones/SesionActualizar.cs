// ==========================================================================
//  Peticiones/SesionActualizar.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Body del PATCH /api/Sesion/{idSesion}: PARCIAL, todo opcional (RF123).
//  Convención v1: null = "no enviado". Body vacío -> 400 (lo decide el
//  servicio).
//
//  Si llega 'inicio' o 'fin' (o los dos), el SERVICIO combina lo enviado
//  con lo que ya tiene la fila y valida "fin > inicio" (RF125). Así un
//  PATCH que solo mueve 'fin' a una hora anterior al inicio guardado
//  también responde 400 y no 500.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Sesion/{idSesion}. Todo opcional.</summary>
public class SesionActualizar
{
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_catedra' debe ser mayor que 0.")]
    [JsonPropertyName("fk_catedra")]
    public long? FkCatedra { get; set; }

    /// <summary>Si llega, >= 1 (en un UPDATE la BD no lo calcula).</summary>
    [Range(1, short.MaxValue,
        ErrorMessage = "El campo 'numero_reunion' debe estar entre 1 y 32767.")]
    [JsonPropertyName("numero_reunion")]
    public short? NumeroReunion { get; set; }

    [JsonPropertyName("numero_reunion_asis")]
    public short? NumeroReunionAsis { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'titulo' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("titulo")]
    public string? Titulo { get; set; }

    [JsonPropertyName("inicio")]
    public DateTimeOffset? Inicio { get; set; }

    [JsonPropertyName("fin")]
    public DateTimeOffset? Fin { get; set; }

    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_modalidad' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_modalidad")]
    public string? FkModalidad { get; set; }

    [StringLength(15, MinimumLength = 1,
        ErrorMessage = "El campo 'fk_sede' debe tener entre 1 y 15 caracteres.")]
    [JsonPropertyName("fk_sede")]
    public string? FkSede { get; set; }

    [StringLength(6, MinimumLength = 6,
        ErrorMessage = "El campo 'fk_periodo' debe tener exactamente 6 caracteres (p. ej. '2026-2').")]
    [JsonPropertyName("fk_periodo")]
    public string? FkPeriodo { get; set; }

    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_encuesta' debe ser mayor que 0.")]
    [JsonPropertyName("fk_encuesta")]
    public long? FkEncuesta { get; set; }

    [StringLength(200, MinimumLength = 1,
        ErrorMessage = "El campo 'lugar' debe tener entre 1 y 200 caracteres.")]
    [JsonPropertyName("lugar")]
    public string? Lugar { get; set; }

    [StringLength(300, MinimumLength = 1,
        ErrorMessage = "El campo 'enlace_virtual' debe tener entre 1 y 300 caracteres.")]
    [JsonPropertyName("enlace_virtual")]
    public string? EnlaceVirtual { get; set; }


    [RegularExpression("^(PROGRAMADA|ABIERTA|CERRADA|MIGRADA)$",
        ErrorMessage = "El campo 'estado' debe ser PROGRAMADA, ABIERTA, CERRADA o MIGRADA.")]
    [JsonPropertyName("estado")]
    public string? Estado { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null).
    /// Las fechas se guardan en UTC ('.UtcDateTime'), igual que en el
    /// POST y el PUT (ADR D21).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (FkCatedra is not null) datos["fk_catedra"] = FkCatedra;
        if (NumeroReunion is not null) datos["numero_reunion"] = NumeroReunion;
        if (NumeroReunionAsis is not null) datos["numero_reunion_asis"] = NumeroReunionAsis;
        if (Titulo is not null) datos["titulo"] = Titulo;
        // '.Value' saca el DateTimeOffset del 'DateTimeOffset?'; '.UtcDateTime'
        // lo convierte en un DateTime en UTC (Kind = Utc), que es lo que
        // Npgsql acepta para una columna TIMESTAMPTZ.
        if (Inicio is not null) datos["inicio"] = Inicio.Value.UtcDateTime;
        if (Fin is not null) datos["fin"] = Fin.Value.UtcDateTime;
        if (FkModalidad is not null) datos["fk_modalidad"] = FkModalidad;
        if (FkSede is not null) datos["fk_sede"] = FkSede;
        if (FkPeriodo is not null) datos["fk_periodo"] = FkPeriodo;
        if (FkEncuesta is not null) datos["fk_encuesta"] = FkEncuesta;
        if (Lugar is not null) datos["lugar"] = Lugar;
        if (EnlaceVirtual is not null) datos["enlace_virtual"] = EnlaceVirtual;
        if (Estado is not null) datos["estado"] = Estado;

        return datos;
    }
}
