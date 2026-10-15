// ==========================================================================
//  Peticiones/UsuarioActualizar.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Body del PATCH /api/Usuario/{idUsuario}: PARCIAL, todo opcional (RF136).
//  Convención v1: null = "no enviado". Body vacío -> 400 (lo decide el
//  servicio).
//
//  LA CLAVE ES ESPECIAL:
//   · Si 'clave' NO viene, el hash guardado no se toca (por eso el cliente
//     puede activar o desactivar una cuenta sin saber su clave).
//   · Si viene, el SERVICIO la hashea con BCrypt y guarda el hash nuevo.
//  Por eso 'clave' NO aparece en ACamposEnviados(): esa lista son columnas
//  de la tabla, y la columna es 'clave_hash', no 'clave'. El servicio añade
//  'clave_hash' al diccionario solo cuando hay clave (ADR D23).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PATCH /api/Usuario/{idUsuario}. Todo opcional.</summary>
public class UsuarioActualizar
{
    [Range(1, long.MaxValue, ErrorMessage = "El campo 'fk_asistente' debe ser mayor que 0.")]
    [JsonPropertyName("fk_asistente")]
    public long? FkAsistente { get; set; }

    [RegularExpression("^[a-z0-9._-]{4,40}$",
        ErrorMessage = "El campo 'usuario' debe tener de 4 a 40 caracteres: minúsculas, dígitos, punto, guion y guion bajo.")]
    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    /// <summary>Clave nueva en texto plano (mínimo 8). Si no viene, el hash no cambia.</summary>
    [MinLength(8, ErrorMessage = "El campo 'clave' debe tener al menos 8 caracteres.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }

    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>Diccionario columna -> valor SOLO con lo que llegó (no null),
    /// SIN la clave: el hash lo agrega el servicio (ver cabecera).</summary>
    public Dictionary<string, object?> ACamposEnviados()
    {
        var datos = new Dictionary<string, object?>();

        if (FkAsistente is not null) datos["fk_asistente"] = FkAsistente;
        if (Usuario is not null) datos["usuario"] = Usuario;
        if (Activo is not null) datos["activo"] = Activo;

        return datos;
    }
}
