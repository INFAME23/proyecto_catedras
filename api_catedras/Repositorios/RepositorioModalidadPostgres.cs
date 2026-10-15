// ==========================================================================
//  Repositorios/RepositorioModalidadPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/008_sp_modalidad.sql', con el MISMO molde de
//  RepositorioSedePostgres.cs: esta clase solo arma el 'CALL', manda los
//  parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'IRepositorioModalidad', 'ServicioModalidad', 'ModalidadController' y
//  las 'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.modalidad' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioModalidadPostgres : IRepositorioModalidad
{
    private readonly string _cadenaConexion;

    public RepositorioModalidadPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Modalidad>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_modalidad(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<Modalidad>>(json!) ?? [];
    }

    public async Task<Modalidad?> ObtenerPorIdAsync(string idModalidad)
    {
        const string sql = "CALL catedras.sp_consultar_modalidad(@idModalidad, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idModalidad });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<Modalidad>(json);
    }

    public async Task CrearAsync(Modalidad modalidad)
    {
        const string sql = @"
            CALL catedras.sp_crear_modalidad(
                @IdModalidad, @Nombre, @CanalDifusion, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida o canal inválido -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, modalidad);
    }

    public async Task<int> ActualizarAsync(string idModalidad, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_modalidad(@idModalidad, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idModalidad });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<Modalidad>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string canalDifusion = datos.TryGetValue("canal_difusion", out var vCanal) ? (string)vCanal! : actual.CanalDifusion;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_modalidad(
                @idModalidad, @nombre, @canalDifusion, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idModalidad, nombre, canalDifusion, activo });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idModalidad)
    {
        const string sql = "CALL catedras.sp_eliminar_modalidad(@idModalidad, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idModalidad });

        // {"eliminado": true|false}. 'modalidad' SÍ tiene 'activo': borrado LÓGICO.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
