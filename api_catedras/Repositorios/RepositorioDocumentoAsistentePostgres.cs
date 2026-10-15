// ==========================================================================
//  Repositorios/RepositorioDocumentoAsistentePostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioDocumentoAsistente para PostgreSQL. Los 5 verbos pasan por
//  los procedimientos de '025_sp_documento_asistente.sql' (molde de
//  RepositorioSedePostgres). Las dos reglas fuertes de la tabla —un solo
//  documento vigente por persona (ux_documento_vigente) y número único por
//  tipo (uq_documento_numero)— las sigue aplicando el motor (D9).
//
//  Diferencias con 'sede':
//   · La lista acepta el filtro opcional por asistente.
//   · El id lo genera la BD: 'sp_crear_' devuelve la fila completa y de
//     ella se toman 'id_documento' y 'registrado_en' (mismo contrato de la
//     v3: CrearAsync devuelve esa tupla).
//   · Eliminar es DELETE FÍSICO (la tabla no tiene 'activo').
//  IRepositorioDocumentoAsistente, el servicio, el controlador y las
//  Peticiones NO cambiaron.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.documento_asistente' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioDocumentoAsistentePostgres : IRepositorioDocumentoAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioDocumentoAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<DocumentoAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        // 'fkAsistente' null -> el procedimiento no filtra.
        const string sql = "CALL catedras.sp_listar_documento_asistente(@fkAsistente, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkAsistente, limite });

        return JsonSerializer.Deserialize<List<DocumentoAsistente>>(json!) ?? [];
    }

    public async Task<DocumentoAsistente?> ObtenerPorIdAsync(long idDocumento)
    {
        const string sql = "CALL catedras.sp_consultar_documento_asistente(@idDocumento, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idDocumento });

        return json is null ? null : JsonSerializer.Deserialize<DocumentoAsistente>(json);
    }

    public async Task<(long IdDocumento, DateTime RegistradoEn)> CrearAsync(DocumentoAsistente documento)
    {
        const string sql = @"
            CALL catedras.sp_crear_documento_asistente(
                @FkAsistente, @FkTipoDocumento, @Numero, @Vigente, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Segundo vigente o número repetido -> NpgsqlException (500), igual
        // que antes. Si todo va bien, la fila vuelve tal como quedó.
        string? json = await conexion.QuerySingleAsync<string?>(sql, new
        {
            documento.FkAsistente,
            documento.FkTipoDocumento,
            documento.Numero,
            documento.Vigente,
        });

        var guardado = JsonSerializer.Deserialize<DocumentoAsistente>(json!)!;
        return (guardado.IdDocumento, guardado.RegistradoEn);
    }

    public async Task<int> ActualizarAsync(long idDocumento, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual (el procedimiento reemplaza las 3 columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_documento_asistente(@idDocumento, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idDocumento });

        if (actualJson is null)
            return 0;

        var actual = JsonSerializer.Deserialize<DocumentoAsistente>(actualJson)!;

        // 2) Mezclar. 'fk_asistente' no está: un documento no cambia de dueño.
        string fkTipoDocumento = datos.TryGetValue("fk_tipo_documento", out var vTipo) ? (string)vTipo! : actual.FkTipoDocumento;
        string numero = datos.TryGetValue("numero", out var vNumero) ? (string)vNumero! : actual.Numero;
        bool vigente = datos.TryGetValue("vigente", out var vVigente) ? (bool)vVigente! : actual.Vigente;

        // 3) Conjunto completo al procedimiento.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_documento_asistente(
                @idDocumento, @fkTipoDocumento, @numero, @vigente, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idDocumento, fkTipoDocumento, numero, vigente });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(long idDocumento)
    {
        const string sql = "CALL catedras.sp_eliminar_documento_asistente(@idDocumento, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idDocumento });

        // {"eliminado": true|false}. FÍSICO: ninguna tabla referencia a
        // documento_asistente, así que ninguna FK lo bloquea (RF105).
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
