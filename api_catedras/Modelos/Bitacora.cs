// ==========================================================================
//  Modelos/Bitacora.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Una FILA de 'catedras.bitacora' (RN-35): registro de toda modificación
//  sobre datos sensibles, con el antes y el después en jsonb.
//
//  Definición real de la tabla (script provisto):
//    id_bitacora    BIGINT       NOT NULL  PRIMARY KEY (identity)
//    ocurrido_en    TIMESTAMPTZ  NOT NULL  DEFAULT now()
//    usuario_bd     NAME         NOT NULL  DEFAULT CURRENT_USER
//    fk_usuario     BIGINT                 (admite NULL)
//    nombre_tabla   VARCHAR(63)  NOT NULL
//    operacion      VARCHAR(10)  NOT NULL  (INSERT | UPDATE | DELETE)
//    llave          VARCHAR(100)           (admite NULL)
//    datos_antes    JSONB                  (admite NULL)
//    datos_despues  JSONB                  (admite NULL)
//
//  ⚠️ LOG DE AUDITORÍA: sus columnas se llenan SOLAS (DEFAULT now(),
//  DEFAULT CURRENT_USER) por los triggers trg_bitacora_asistente y
//  trg_bitacora_registro. La API ahora también permite crear / corregir /
//  borrar entradas (a pedido, para cubrir los 5 verbos), pero 'ocurrido_en' y
//  'usuario_bd' NUNCA se aceptan del cliente (ver BitacoraController).
//
//  'datos_antes' / 'datos_despues' son JSON libre (la fila entera de la
//  tabla auditada): se exponen como JsonElement para devolverlos tal cual,
//  sin inventarles una forma.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Una entrada del log de auditoría.</summary>
public class Bitacora
{
    /// <summary>Identificador de la entrada. Clave primaria (lo genera la BD).</summary>
    [JsonPropertyName("id_bitacora")]
    public long IdBitacora { get; set; }

    /// <summary>Cuándo ocurrió el cambio. Lo pone la BD.</summary>
    [JsonPropertyName("ocurrido_en")]
    public DateTime OcurridoEn { get; set; }

    /// <summary>Usuario de la base de datos que hizo el cambio. Lo pone la BD.</summary>
    [JsonPropertyName("usuario_bd")]
    public required string UsuarioBd { get; set; }

    /// <summary>Usuario de la aplicación, si se conoce.</summary>
    [JsonPropertyName("fk_usuario")]
    public long? FkUsuario { get; set; }

    /// <summary>Tabla auditada (p. ej. 'asistente').</summary>
    [JsonPropertyName("nombre_tabla")]
    public required string NombreTabla { get; set; }

    /// <summary>INSERT, UPDATE o DELETE.</summary>
    [JsonPropertyName("operacion")]
    public required string Operacion { get; set; }

    /// <summary>Llave de la fila afectada, como texto.</summary>
    [JsonPropertyName("llave")]
    public string? Llave { get; set; }

    /// <summary>La fila ANTES del cambio (null en un INSERT).</summary>
    [JsonPropertyName("datos_antes")]
    public JsonElement? DatosAntes { get; set; }

    /// <summary>La fila DESPUÉS del cambio (null en un DELETE).</summary>
    [JsonPropertyName("datos_despues")]
    public JsonElement? DatosDespues { get; set; }
}
