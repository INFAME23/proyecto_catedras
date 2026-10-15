// ==========================================================================
//  Modelos/RolPorUsuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Una fila de 'catedras.rol_por_usuario': un ROL que tiene (o tuvo) un
//  usuario, con su vigencia en el tiempo.
//
//  Definición real de la tabla (ver version5/5_data_model.md §2.2):
//    fk_usuario   BIGINT       PK (1/3)  FK -> usuario (v5, CASCADE)
//    fk_rol       VARCHAR(20)  PK (2/3)  FK -> rol (v1, RESTRICT)
//    fecha_ini    DATE         PK (3/3)  DEFAULT CURRENT_DATE
//    fecha_fin    DATE         NULL = el rol sigue vigente
//    CHECK chk_rol_usuario_fechas: fecha_fin IS NULL OR fecha_fin >= fecha_ini
//
//  LLAVE COMPUESTA (ADR D16, el mismo patrón de VinculacionAsistente en la
//  v3): esta tabla NO tiene columna 'id'. La fila se identifica con sus 3
//  columnas de llave, por eso este modelo no tiene propiedad 'Id' y la API
//  no ofrece '/{id}'. Un rol no se borra: se CIERRA poniéndole 'fecha_fin'.
//
//  'fecha_ini' no lleva 'required' en la petición (si no llega, se usa hoy),
//  pero aquí en el modelo SÍ es obligatoria: es parte de la llave y el
//  repositorio siempre la manda ya resuelta.
// ==========================================================================

namespace ApiCatedras.Modelos;

using System.Text.Json.Serialization;

/// <summary>Un rol (con vigencia) asignado a un usuario.</summary>
public class RolPorUsuario
{
    /// <summary>Usuario que tiene el rol. Parte 1 de la llave.</summary>
    [JsonPropertyName("fk_usuario")]
    public required long FkUsuario { get; set; }

    /// <summary>Rol asignado (catálogo v1: ADMIN, CONSULTA...). Parte 2 de la llave.</summary>
    [JsonPropertyName("fk_rol")]
    public required string FkRol { get; set; }

    /// <summary>Fecha en que empezó. Parte 3 de la llave.</summary>
    [JsonPropertyName("fecha_ini")]
    public required DateOnly FechaIni { get; set; }

    /// <summary>Fecha en que terminó. Null = sigue vigente.</summary>
    [JsonPropertyName("fecha_fin")]
    public DateOnly? FechaFin { get; set; }
}
