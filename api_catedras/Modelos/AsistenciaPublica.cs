// ==========================================================================
//  Modelos/AsistenciaPublica.cs  ·  Subversión v7.2 · RF-S9 / RF-S10
// --------------------------------------------------------------------------
//  Lo que la API devuelve en el registro público de asistencia (sin cuenta,
//  sin clave por correo: D60). No son filas de una tabla: son el resultado de
//  funciones y procedimientos de la base de datos (migración 063).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>
/// Lo que necesita la página pública tras escanear el QR. NO incluye las
/// coordenadas de la sede (la comparación la hace la base de datos).
/// </summary>
public class EnlacePublico
{
    [JsonPropertyName("id_enlace")] public long IdEnlace { get; set; }
    [JsonPropertyName("id_sesion")] public long IdSesion { get; set; }
    [JsonPropertyName("titulo_sesion")] public string? TituloSesion { get; set; }
    [JsonPropertyName("numero_reunion")] public int NumeroReunion { get; set; }

    /// <summary>Código de 9 dígitos de la cátedra (catedra.id_evento_asis).</summary>
    [JsonPropertyName("id_catedra")] public string? IdCatedra { get; set; }
    [JsonPropertyName("titulo_catedra")] public string? TituloCatedra { get; set; }
    [JsonPropertyName("inicio")] public DateTime Inicio { get; set; }
    [JsonPropertyName("fin")] public DateTime Fin { get; set; }
    [JsonPropertyName("modalidad")] public string? Modalidad { get; set; }

    /// <summary>QR (presencial) o ENLACE (virtual). Decide si se pide ubicación.</summary>
    [JsonPropertyName("canal")] public string? Canal { get; set; }

    /// <summary>true = el formulario debe pedir la ubicación del navegador (canal QR).</summary>
    [JsonPropertyName("requiere_ubicacion")] public bool RequiereUbicacion { get; set; }
    [JsonPropertyName("ventana_desde")] public DateTime VentanaDesde { get; set; }
    [JsonPropertyName("ventana_hasta")] public DateTime VentanaHasta { get; set; }

    /// <summary>pendiente (aún no abre), abierto, cerrado o revocado.</summary>
    [JsonPropertyName("estado_ventana")] public string? EstadoVentana { get; set; }
}

/// <summary>
/// Aviso temprano de ubicación: ¿está la persona dentro del radio de la sede?
/// No guarda nada; el procedimiento de registro la vuelve a verificar.
/// </summary>
public class UbicacionVerificada
{
    /// <summary>OK, FUERA_DE_RADIO, SIN_UBICACION, DATOS_INVALIDOS, SEDE_SIN_COORDENADAS, ENLACE_REVOCADO, FUERA_DE_VENTANA o ENLACE_NO_EXISTE.</summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = "";
    [JsonPropertyName("requiere_ubicacion")] public bool RequiereUbicacion { get; set; }

    /// <summary>true/false cuando se pudo comparar; null si el canal no pide ubicación o faltó un dato.</summary>
    [JsonPropertyName("dentro")] public bool? Dentro { get; set; }
    [JsonPropertyName("distancia_metros")] public decimal? DistanciaMetros { get; set; }
    [JsonPropertyName("radio_metros")] public int? RadioMetros { get; set; }

    /// <summary>Texto para mostrar a la persona (lo pone el servicio, no la base de datos).</summary>
    [JsonPropertyName("mensaje")] public string? Mensaje { get; set; }
}

/// <summary>Resultado de registrar la asistencia (estudiante, docente o externo).</summary>
public class ResultadoRegistroPublico
{
    /// <summary>OK u otro estado de negocio (ver migración 063): nada se guardó si no es OK.</summary>
    [JsonPropertyName("estado")] public string Estado { get; set; } = "";
    [JsonPropertyName("mensaje")] public string? Mensaje { get; set; }
    [JsonPropertyName("id_registro")] public long? IdRegistro { get; set; }
    [JsonPropertyName("distancia_metros")] public decimal? DistanciaMetros { get; set; }
    [JsonPropertyName("radio_metros")] public int? RadioMetros { get; set; }
}
