// ==========================================================================
//  Modelos/Exportacion.cs  ·  Subversión v7.2 · RF-S13 / RF-S14
// --------------------------------------------------------------------------
//  Lo que devuelven las consultas de la exportación de fin de periodo
//  (migración 066). Son datos de SOLO LECTURA: el front arma los CSV y el ZIP.
//
//  'IdCatedra' es el código de 9 dígitos de la cátedra (catedra.id_evento_asis,
//  con ceros a la izquierda): es el mismo en los nombres de archivo, en la
//  pantalla y en el catálogo, para poder cruzarlos.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Una cátedra del periodo con cuántas personas de cada tipo asistieron.</summary>
public class ExportacionCatedra
{
    [JsonPropertyName("id_catedra")] public string IdCatedra { get; set; } = "";
    [JsonPropertyName("titulo")] public string Titulo { get; set; } = "";

    /// <summary>false = cátedra inactiva (solo aparece si tiene asistencias en el periodo).</summary>
    [JsonPropertyName("activo")] public bool Activo { get; set; }

    /// <summary>Estudiantes distintos que asistieron (una persona cuenta una vez aunque haya ido a varias sesiones).</summary>
    [JsonPropertyName("estudiantes")] public int Estudiantes { get; set; }
    [JsonPropertyName("docentes")] public int Docentes { get; set; }
    [JsonPropertyName("externos")] public int Externos { get; set; }

    /// <summary>Asistentes de otras vinculaciones (administrativo, egresado...): no se exportan, solo se cuentan.</summary>
    [JsonPropertyName("otros")] public int Otros { get; set; }
}

/// <summary>Una cátedra con la lista de quienes asistieron (ya sin repetir personas).</summary>
public class ExportacionCatedraAsistentes
{
    [JsonPropertyName("id_catedra")] public string IdCatedra { get; set; } = "";
    [JsonPropertyName("titulo")] public string Titulo { get; set; } = "";
    [JsonPropertyName("asistentes")] public List<ExportacionAsistente> Asistentes { get; set; } = [];
}

/// <summary>
/// Una persona que asistió. Trae TODOS los campos de los tres CSV; el front elige
/// las columnas según el 'tipo':
///   ESTUDIANTE -> cc, carnet, programa · DOCENTE -> cc, nombre, contacto, facultad ·
///   EXTERNO -> cc, nombre, contacto, procedencia.
/// </summary>
public class ExportacionAsistente
{
    /// <summary>ESTUDIANTE, DOCENTE o EXTERNO (la vinculación con que se registró).</summary>
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = "";
    [JsonPropertyName("cc")] public string? Cc { get; set; }
    [JsonPropertyName("carnet")] public string? Carnet { get; set; }
    [JsonPropertyName("nombre")] public string? Nombre { get; set; }
    [JsonPropertyName("contacto")] public string? Contacto { get; set; }

    /// <summary>Programa tal como estaba el día de la cátedra (código, igual que en el archivo de carga).</summary>
    [JsonPropertyName("programa")] public string? Programa { get; set; }

    /// <summary>Nombre de la facultad que declaró el docente.</summary>
    [JsonPropertyName("facultad")] public string? Facultad { get; set; }

    /// <summary>De dónde dijo venir el externo.</summary>
    [JsonPropertyName("procedencia")] public string? Procedencia { get; set; }
}
