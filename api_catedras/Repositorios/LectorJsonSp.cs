// ==========================================================================
//  Repositorios/LectorJsonSp.cs  ·  Cátedras · RETROFIT criterio 3 (Fase 5-6)
// --------------------------------------------------------------------------
//  Lee el 'p_resultado' (json) que devuelven los procedimientos del
//  retrofit y lo convierte en objetos C#. Es exactamente lo que hace
//  RepositorioSedePostgres con 'JsonSerializer.Deserialize', con UN ajuste
//  que 'sede' no necesitó porque no tiene columnas de fecha y hora:
//
//  FECHAS EN UTC (ADR D21 de la v4). Antes, Dapper/Npgsql leían las
//  columnas 'timestamptz' como DateTime con Kind=Utc, y la API las
//  respondía como "2026-10-20T19:00:00Z" — el front (servicios/fechas.py)
//  cuenta con eso. 'row_to_json' escribe la fecha con el desfase de la
//  sesión de PostgreSQL ("...-05:00" o "...+00:00"), y JsonSerializer, por
//  defecto, la convertiría a Kind=Local. Los dos convertidores de abajo la
//  devuelven a UTC, así que la respuesta de la API queda IGUAL que antes.
//
//  Solo lo usan los repositorios; no cambia ningún contrato público.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Convierte el JSON de 'p_resultado' en objetos C#, con fechas en UTC.</summary>
internal static class LectorJsonSp
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        Converters = { new ConvertidorDateTimeUtc(), new ConvertidorDateTimeOffsetUtc() },
    };

    /// <summary>Arreglo JSON -> lista. Los 'sp_listar_' nunca devuelven NULL ('[]' si no hay filas).</summary>
    public static List<T> Lista<T>(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<List<T>>(json, Opciones) ?? [];

    /// <summary>Objeto JSON -> fila. NULL (no encontrada) -> null, mismo contrato que QuerySingleOrDefaultAsync.</summary>
    public static T? Fila<T>(string? json) where T : class =>
        json is null ? null : JsonSerializer.Deserialize<T>(json, Opciones);

    /// <summary>Lee una bandera booleana, p. ej. {"eliminado": true}.</summary>
    public static bool Bandera(string? json, string propiedad)
    {
        if (json is null)
            return false;

        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.GetProperty(propiedad).GetBoolean();
    }

    /// <summary>Lee una propiedad de texto que el modelo no deserializa (p. ej. un campo [JsonIgnore]).</summary>
    public static string? Texto(string json, string propiedad)
    {
        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.GetProperty(propiedad).GetString();
    }

    // "2026-10-20T14:00:00-05:00" -> DateTime 19:00 con Kind=Utc.
    private sealed class ConvertidorDateTimeUtc : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset().UtcDateTime;

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime());
    }

    // Igual, para DateTimeOffset (RespuestaEncuesta.RespondidaEn): desfase +00:00.
    private sealed class ConvertidorDateTimeOffsetUtc : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.GetDateTimeOffset().ToUniversalTime();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime());
    }
}
