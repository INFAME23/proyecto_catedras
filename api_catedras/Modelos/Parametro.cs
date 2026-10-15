// ==========================================================================
//  Modelos/Parametro.cs  ·  Proyecto Cátedras · Retrofit criterio 3
// --------------------------------------------------------------------------
//  Una FILA de 'catedras.parametro': configuración que el administrador
//  cambia sin tocar el código (del .docx: "el administrador configura o
//  parametriza los valores que considere").
//
//  Definición real de la tabla (script provisto):
//    clave                VARCHAR(60)   NOT NULL  PRIMARY KEY
//    valor                VARCHAR(200)  NOT NULL
//    tipo_dato            VARCHAR(15)   NOT NULL  DEFAULT 'TEXTO'
//                         (TEXTO | ENTERO | BOOLEANO | FECHA | DECIMAL)
//    descripcion          VARCHAR(300)  NOT NULL
//    fk_usuario_modifica  BIGINT                  (admite NULL)
//    modificado_en        TIMESTAMPTZ   NOT NULL  DEFAULT now()
//
//  [SOLID · S] Solo representa la fila. Los DTO de entrada viven en
//  'Peticiones/'. 'modificado_en' lo pone la BD (alta y cada actualización).
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un parámetro de configuración del sistema (clave -> valor).</summary>
public class Parametro
{
    /// <summary>Nombre único del parámetro. Clave primaria. 1 a 60 caracteres.</summary>
    [JsonPropertyName("clave")]
    public required string Clave { get; set; }

    /// <summary>Valor, siempre como texto (el 'tipo_dato' dice cómo leerlo).</summary>
    [JsonPropertyName("valor")]
    public required string Valor { get; set; }

    /// <summary>TEXTO, ENTERO, BOOLEANO, FECHA o DECIMAL. Por defecto TEXTO.</summary>
    [JsonPropertyName("tipo_dato")]
    public string TipoDato { get; set; } = "TEXTO";

    /// <summary>Para qué sirve el parámetro.</summary>
    [JsonPropertyName("descripcion")]
    public required string Descripcion { get; set; }

    /// <summary>Usuario que lo modificó por última vez (opcional).</summary>
    [JsonPropertyName("fk_usuario_modifica")]
    public long? FkUsuarioModifica { get; set; }

    /// <summary>Cuándo se creó o modificó por última vez. Lo pone la BD.</summary>
    [JsonPropertyName("modificado_en")]
    public DateTime ModificadoEn { get; set; }
}
