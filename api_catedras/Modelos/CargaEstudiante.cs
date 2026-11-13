// ==========================================================================
//  Modelos/CargaEstudiante.cs  ·  Subversión v7.2 · RF-S8
// --------------------------------------------------------------------------
//  Respuesta de cargar UNA fila del archivo de estudiantes. No es una tabla:
//  es lo que devuelve sp_cargar_estudiante (migración 064).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Resultado de cargar un estudiante: quién es y si se creó o ya existía.</summary>
public class ResultadoCargaEstudiante
{
    /// <summary>Id interno de la persona (creada o ya existente).</summary>
    [JsonPropertyName("id_asistente")]
    public long IdAsistente { get; set; }

    /// <summary>CREADO (persona nueva) o ACTUALIZADO (el carnet ya existía: es idempotente).</summary>
    [JsonPropertyName("accion")]
    public string Accion { get; set; } = "";
}
