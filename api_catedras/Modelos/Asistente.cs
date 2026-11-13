// ==========================================================================
//  Modelos/Asistente.cs  ·  Proyecto Cátedras · Versión 3
// --------------------------------------------------------------------------
//  Una fila de 'catedras.asistente': la PERSONA que asiste a las cátedras
//  (estudiante, docente, administrativo, egresado o público externo).
//
//  Definición real de la tabla (ver version3/5_data_model.md §2.2):
//    id_asistente          BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    id_asis               VARCHAR(15)   UNIQUE  CHECK ('^[0-9A-Za-z]{5,15}$')
//    nombres / apellidos   VARCHAR(100)  NOT NULL
//    nombre_completo_asis  VARCHAR(200)  (el nombre tal como llegó del ASIS)
//    correo_institucional  CITEXT        UNIQUE  (al menos un correo)
//    correo_personal       CITEXT
//    celular               VARCHAR(20)
//    es_externo            BOOLEAN       NOT NULL DEFAULT false
//    activo                BOOLEAN       NOT NULL DEFAULT true
//    creado_en             TIMESTAMPTZ   NOT NULL DEFAULT now()
//    fk_lote_carga         BIGINT        FK -> lote_carga_asistente (SET NULL)
//
//  CHECK chk_asistente_externo: si NO es externo, 'id_asis' es obligatorio.
//  (Un externo PUEDE tener id_asis; el CHECK no los hace excluyentes.)
//
//  'CITEXT' = texto que se compara SIN distinguir mayúsculas: para la BD
//  "Ana@X.co" y "ana@x.co" son el mismo correo. En C# es un 'string' normal.
//
//  'IdAsistente' y 'CreadoEn' NO llevan 'required': los pone la BD.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una persona que asiste a las cátedras.</summary>
public class Asistente
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_asistente")]
    public long IdAsistente { get; set; }

    /// <summary>Código ASIS (5 a 15 alfanuméricos). Obligatorio si no es externo.</summary>
    [JsonPropertyName("id_asis")]
    public string? IdAsis { get; set; }

    /// <summary>Nombres. Obligatorio.</summary>
    [JsonPropertyName("nombres")]
    public required string Nombres { get; set; }

    /// <summary>Apellidos. Obligatorio.</summary>
    [JsonPropertyName("apellidos")]
    public required string Apellidos { get; set; }

    /// <summary>Nombre tal como llegó del ASIS, sin partir. Opcional.</summary>
    [JsonPropertyName("nombre_completo_asis")]
    public string? NombreCompletoAsis { get; set; }

    /// <summary>Correo institucional (único). Opcional, pero debe haber al menos un correo.</summary>
    [JsonPropertyName("correo_institucional")]
    public string? CorreoInstitucional { get; set; }

    /// <summary>Correo personal. Opcional.</summary>
    [JsonPropertyName("correo_personal")]
    public string? CorreoPersonal { get; set; }

    /// <summary>Celular. Opcional.</summary>
    [JsonPropertyName("celular")]
    public string? Celular { get; set; }

    /// <summary>¿Es público externo a la universidad? Por defecto false.</summary>
    [JsonPropertyName("es_externo")]
    public bool EsExterno { get; set; } = false;

    /// <summary>¿Está activo? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>Cuándo se registró (lo pone la BD con now()). De solo lectura.</summary>
    [JsonPropertyName("creado_en")]
    public DateTime CreadoEn { get; set; }

    /// <summary>Lote de carga masiva que lo creó. Null si se creó a mano.</summary>
    [JsonPropertyName("fk_lote_carga")]
    public long? FkLoteCarga { get; set; }

    /// <summary>Solo lectura (071). Nombres de los roles VIGENTES de su cuenta de usuario (Administrador,
    /// Asistente...). Solo viene en el listado; [] si no tiene cuenta.</summary>
    [JsonPropertyName("roles_cuenta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RolesCuenta { get; set; }

    /// <summary>Solo lectura (071). Nombres de sus vinculaciones VIGENTES (Estudiante USB, Docente USB,
    /// Publico externo...). Solo viene en el listado.</summary>
    [JsonPropertyName("vinculaciones")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Vinculaciones { get; set; }
}
