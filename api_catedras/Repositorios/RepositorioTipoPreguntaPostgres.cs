// ==========================================================================
//  Repositorios/RepositorioTipoPreguntaPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/022_sp_tipo_pregunta.sql', con el MISMO molde
//  de RepositorioSedePostgres.cs: esta clase solo arma el 'CALL', manda
//  los parámetros y convierte el JSON que devuelve el procedimiento.
//
//  'tipo_pregunta' NO tiene 'activo' -> el eliminar es un DELETE físico.
//  'IRepositorioTipoPregunta', 'ServicioTipoPregunta',
//  'TipoPreguntaController' y las 'Peticiones/' NO cambiaron (SOLID · D).
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.tipo_pregunta' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioTipoPreguntaPostgres : IRepositorioTipoPregunta
{
    private readonly string _cadenaConexion;

    public RepositorioTipoPreguntaPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<TipoPregunta>> ObtenerTodosAsync(int limite)
    {
        const string sql = "CALL catedras.sp_listar_tipo_pregunta(@limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite });

        // Siempre un arreglo ('[]' si no hay filas): json_agg + COALESCE.
        return JsonSerializer.Deserialize<List<TipoPregunta>>(json!) ?? [];
    }

    public async Task<TipoPregunta?> ObtenerPorIdAsync(string idTipoPregunta)
    {
        const string sql = "CALL catedras.sp_consultar_tipo_pregunta(@idTipoPregunta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idTipoPregunta });

        // NULL = no existe -> mismo contrato que QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<TipoPregunta>(json);
    }

    public async Task CrearAsync(TipoPregunta tipoPregunta)
    {
        const string sql = @"
            CALL catedras.sp_crear_tipo_pregunta(
                @IdTipoPregunta, @Nombre, @GuardaNumero, @GuardaTexto, @GuardaOpcion, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave repetida o chk_tipo_pregunta_destino -> NpgsqlException, igual que antes.
        await conexion.ExecuteAsync(sql, tipoPregunta);
    }

    public async Task<int> ActualizarAsync(string idTipoPregunta, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual (el procedimiento reemplaza TODAS las columnas).
        const string sqlConsultar = "CALL catedras.sp_consultar_tipo_pregunta(@idTipoPregunta, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idTipoPregunta });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<TipoPregunta>(actualJson)!;

        // 2) Mezclar: lo que llegó PISA el valor actual; lo demás se queda.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        bool guardaNumero = datos.TryGetValue("guarda_numero", out var vNumero) ? (bool)vNumero! : actual.GuardaNumero;
        bool guardaTexto = datos.TryGetValue("guarda_texto", out var vTexto) ? (bool)vTexto! : actual.GuardaTexto;
        bool guardaOpcion = datos.TryGetValue("guarda_opcion", out var vOpcion) ? (bool)vOpcion! : actual.GuardaOpcion;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_tipo_pregunta(
                @idTipoPregunta, @nombre, @guardaNumero, @guardaTexto, @guardaOpcion, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idTipoPregunta, nombre, guardaNumero, guardaTexto, guardaOpcion });

        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idTipoPregunta)
    {
        const string sql = "CALL catedras.sp_eliminar_tipo_pregunta(@idTipoPregunta, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idTipoPregunta });

        // {"eliminado": true|false}. Sin 'activo': DELETE físico.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
