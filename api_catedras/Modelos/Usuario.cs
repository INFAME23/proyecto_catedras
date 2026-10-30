// ==========================================================================
//  Modelos/Usuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Una fila de 'catedras.usuario': la CUENTA DE ACCESO de una persona del
//  personal (administrador, coordinador...). Máximo una cuenta por asistente.
//
//  Definición real de la tabla (ver version5/5_data_model.md §2.1):
//    id_usuario     BIGINT        PK  GENERATED ALWAYS AS IDENTITY
//    fk_asistente   BIGINT        NOT NULL  FK -> asistente (v3, RESTRICT)
//                                 uq_usuario_persona: 1 usuario por persona
//    usuario        VARCHAR(40)   NOT NULL  CHECK ~ '^[a-z0-9._-]{4,40}$'
//                                 uq_usuario_login
//    clave_hash     VARCHAR(255)  NOT NULL  hash BCrypt (Guía IA, Regla 3)
//    activo         BOOLEAN       NOT NULL  DEFAULT true
//    ultimo_acceso  TIMESTAMPTZ   (lo pone el servicio en cada login exitoso)
//    creado_en      TIMESTAMPTZ   NOT NULL  DEFAULT now()
//
//  DOS COSAS NUEVAS EN ESTE MODELO:
//
//  1) 'ClaveHash' lleva [JsonIgnore] (ADR D23, RF138). Este modelo es lo que
//     la API devuelve en sus respuestas; con [JsonIgnore] el hash NUNCA sale,
//     ni en un GET ni en la respuesta de un POST. Se prefirió esta anotación
//     a inventar una clase "UsuarioRespuesta" aparte: es un cambio mínimo.
//     OJO: [JsonIgnore] va aquí, en el MODELO, y NO en las peticiones: el
//     cliente SÍ manda la 'clave' en texto plano al crear o cambiarla.
//
//  2) La columna se llama 'usuario' y una propiedad no puede llamarse igual
//     que su clase, así que la propiedad es 'NombreUsuario'. Dapper no
//     adivina esa equivalencia: el SQL del repositorio debe escribir
//     'usuario AS nombre_usuario'. El JSON sí conserva el nombre 'usuario'.
//
//  Fechas: DateTime en UTC (ADR D21 de la v4), nunca DateTimeOffset.
//
//  ⚠ 'ClaveHash' NO lleva 'required'. System.Text.Json (.NET 8+) lanza
//  InvalidOperationException al serializar un tipo que tiene una propiedad
//  'required' marcada [JsonIgnore] ("is marked required but does not specify
//  a setter"): una propiedad obligatoria que el serializador ignora es una
//  contradicción para él. Lo encontró la prueba de capas (Fase 43); en la API
//  real habría dado 500 en CADA respuesta que incluyera un Usuario. El servicio
//  siempre la asigna, así que 'required' no aportaba nada.
//
//  Tres propiedades las pone la BD o el servicio y por eso no llevan
//  'required': IdUsuario (IDENTITY), CreadoEn (DEFAULT now()) y
//  UltimoAcceso (se llena al hacer login).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una cuenta de acceso (usuario y clave) de una persona del personal.</summary>
public class Usuario
{
    /// <summary>Identificador autogenerado por la BD.</summary>
    [JsonPropertyName("id_usuario")]
    public long IdUsuario { get; set; }

    /// <summary>Persona dueña de la cuenta (tabla de la v3). Única: una cuenta por persona.</summary>
    [JsonPropertyName("fk_asistente")]
    public required long FkAsistente { get; set; }

    /// <summary>Nombre de usuario para iniciar sesión (minúsculas, dígitos, . _ -). Único.</summary>
    [JsonPropertyName("usuario")]
    public required string NombreUsuario { get; set; }

    /// <summary>Hash BCrypt de la clave. NUNCA se serializa (RF138, ADR D23).</summary>
    [JsonIgnore]
    public string ClaveHash { get; set; } = "";

    /// <summary>¿La cuenta puede iniciar sesión? Por defecto true.</summary>
    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    /// <summary>v7.1 · D54: true = en el próximo ingreso debe cambiar su clave (clave inicial = código de estudiante).</summary>
    [JsonPropertyName("debe_cambiar_clave")]
    public bool DebeCambiarClave { get; set; }

    /// <summary>Último login exitoso, en UTC. Null si nunca ha entrado.</summary>
    [JsonPropertyName("ultimo_acceso")]
    public DateTime? UltimoAcceso { get; set; }

    /// <summary>Cuándo se creó la cuenta (DEFAULT now() de la BD). Solo lectura, en UTC.</summary>
    [JsonPropertyName("creado_en")]
    public DateTime CreadoEn { get; set; }
}
