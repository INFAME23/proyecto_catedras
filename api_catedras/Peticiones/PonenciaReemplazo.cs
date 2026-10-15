// ==========================================================================
//  Peticiones/PonenciaReemplazo.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Body del PUT /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}: cambia el ROL
//  de una participación existente. La llave actual viaja en la URL; aquí solo
//  llega el rol NUEVO (las otras dos columnas SON la llave: moverlas es
//  eliminar + crear). Mismo conjunto de valores que 'PonenciaCrear'.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del PUT /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}.</summary>
public class PonenciaReemplazo
{
    /// <summary>Rol nuevo. Obligatorio; lista cerrada (refleja chk_ponencia_rol).</summary>
    [Required(ErrorMessage = "El campo 'rol' es obligatorio.")]
    [RegularExpression("^(PONENTE|MODERADOR|INVITADO|ORGANIZADOR)$",
        ErrorMessage = "El campo 'rol' debe ser PONENTE, MODERADOR, INVITADO u ORGANIZADOR.")]
    [JsonPropertyName("rol")]
    public string? Rol { get; set; }
}
