// ==========================================================================
//  Modelos/Ponencia.cs  ·  Proyecto Cátedras · Versión 4
// --------------------------------------------------------------------------
//  Una fila de 'catedras.ponencia': quién participó en una sesión como
//  PONENTE, MODERADOR, INVITADO u ORGANIZADOR.
//
//  Definición real de la tabla (ver version4/5_data_model.md §2.2):
//    fk_sesion     BIGINT       PK (1/3)  FK -> sesion (CASCADE)
//    fk_asistente  BIGINT       PK (2/3)  FK -> asistente (v3, RESTRICT)
//    rol           VARCHAR(15)  PK (3/3)  DEFAULT 'PONENTE'  (lista cerrada)
//
//  LLAVE COMPUESTA SIN VIGENCIA (ADR D19): igual que VinculacionAsistente
//  (v3) no hay columna 'id' — la fila se identifica con sus 3 columnas —,
//  PERO aquí no hay "desde/hasta" que proteger: la participación existe o
//  no existe. Por eso este recurso SÍ se borra (DELETE) y no se "cierra".
//
//  Ninguna columna queda fuera de la llave: no hay nada que editar. Cambiar
//  el rol de alguien = quitar la fila y crear otra.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>La participación de un asistente en una sesión, con un rol.</summary>
public class Ponencia
{
    // SIN propiedad 'Id': la llave primaria son estas 3 columnas.

    /// <summary>Sesión en la que participa. Parte 1 de la llave.</summary>
    [JsonPropertyName("fk_sesion")]
    public required long FkSesion { get; set; }

    /// <summary>Asistente que participa. Parte 2 de la llave.</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>PONENTE, MODERADOR, INVITADO u ORGANIZADOR. Parte 3 de la llave.</summary>
    [JsonPropertyName("rol")]
    public string Rol { get; set; } = "PONENTE";
}
