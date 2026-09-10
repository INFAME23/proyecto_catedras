// ==========================================================================
//  Repositorios/ManejadorTipoDateOnly.cs  ·  Proyecto Cátedras
// --------------------------------------------------------------------------
//  Dapper (anterior a 'DateOnly', tipo de C# 10) no sabe mapear ese tipo
//  por sí solo: un parámetro '@VigenteDesde' de tipo DateOnly falla al
//  enviarse ("no puede usarse como valor de parámetro") y una columna
//  DATE leída de la BD falla al mapearse a una propiedad DateOnly ("error
//  al analizar la columna"). PostgreSQL/Npgsql sí entienden DATE de forma
//  nativa; el único que falta es este puente en Dapper.
//
//  Se registra UNA vez en Program.cs (SqlMapper.AddTypeHandler) para
//  'DateOnly' y 'DateOnly?', y a partir de ahí todos los repositorios que
//  usan fechas (RepositorioEncuestaPostgres) simplemente funcionan.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using System.Data;
using Dapper;

/// <summary>Traduce DateOnly <-> DateTime (el tipo que sí entienden Dapper
/// y Npgsql) en ambas direcciones: parámetro de entrada y columna leída.</summary>
public class ManejadorTipoDateOnly : SqlMapper.TypeHandler<DateOnly>
{
    /// <summary>C# -> BD: el parámetro se manda como DateTime a medianoche.</summary>
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    /// <summary>BD -> C#: la columna DATE llega como DateTime; se recorta a DateOnly.</summary>
    public override DateOnly Parse(object value)
    {
        return DateOnly.FromDateTime((DateTime)value);
    }
}
