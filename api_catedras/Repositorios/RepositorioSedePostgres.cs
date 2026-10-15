// ==========================================================================
//  Repositorios/RepositorioSedePostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  PRUEBA DE CONCEPTO del retrofit (ver PLAN_RETROFIT_SP.md): los 5
//  verbos ahora pasan por los procedimientos de
//  'db/postgres/migraciones/005_sp_sede_ejemplo.sql'. Esta clase YA NO
//  escribe SELECT/INSERT/UPDATE/DELETE: solo arma el 'CALL', manda los
//  parámetros, y convierte el JSON que devuelve el procedimiento en
//  objetos C#.
//
//  LO QUE NO CAMBIÓ (a propósito — SOLID · D, Inversión de dependencias):
//  'IRepositorioSede', 'ServicioSede', 'SedeController' y las 'Peticiones/'
//  siguen EXACTAMENTE igual. El contrato público de esta clase (qué
//  recibe, qué devuelve cada método) es el mismo de antes: el PATCH
//  parcial sigue entrando como 'Dictionary<string, object?>'. Lo único
//  que cambió es CÓMO llega a la base de datos — tal como dice el propio
//  §4 de VERSION_2.md: "Qué va en su repositorio: la llamada al
//  procedimiento, con sus parámetros".
//
//  CÓMO SE MEZCLA EL PATCH (nuevo, antes no hacía falta): como
//  'sp_actualizar_sede' reemplaza TODAS las columnas (una sola versión del
//  procedimiento sirve para PUT y para PATCH, ver el .sql), un PATCH
//  parcial ya no arma un 'SET' dinámico: primero se CONSULTA la fila
//  actual, se mezclan encima los campos que sí llegaron, y se llama al
//  procedimiento con el conjunto completo. Es exactamente lo mismo que ya
//  hacía 'ServicioSesion' para cruzar 'fin'/'inicio' en un PATCH parcial
//  (v4) — el mismo patrón, solo que ahora vive en el repositorio en vez
//  del servicio, porque el servicio no tiene por qué saber que ahora hay
//  un procedimiento de por medio.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.sede' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioSedePostgres : IRepositorioSede
{
    private readonly string _cadenaConexion;

    public RepositorioSedePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Sede>> ObtenerTodosAsync(int limite, bool incluirInactivos)
    {
        // 'CALL' con un INOUT devuelve UNA fila con ese parámetro:
        // QuerySingleAsync<string?> lee el texto JSON completo.
        const string sql = "CALL catedras.sp_listar_sede(@limite, @incluirInactivos, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite, incluirInactivos });

        // El procedimiento siempre devuelve un arreglo ('[]' si no hay
        // filas, nunca NULL) — json_agg(...) con COALESCE lo garantiza.
        return JsonSerializer.Deserialize<List<Sede>>(json!) ?? [];
    }

    public async Task<Sede?> ObtenerPorIdAsync(string idSede)
    {
        const string sql = "CALL catedras.sp_consultar_sede(@idSede, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idSede });

        // 'json' es NULL cuando el procedimiento no encontró la fila
        // (row_to_json de una subconsulta vacía es NULL) -> Sede? = null,
        // mismo contrato que antes con QuerySingleOrDefaultAsync.
        return json is null ? null : JsonSerializer.Deserialize<Sede>(json);
    }

    public async Task CrearAsync(Sede sede)
    {
        const string sql = @"
            CALL catedras.sp_crear_sede(
                @IdSede, @Nombre, @Direccion, @EsVirtual, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Código o nombre repetido (pk_sede, uq_sede_nombre) ->
        // NpgsqlException, igual que antes; el controlador la traduce.
        // No hace falta leer el JSON de vuelta: 'CrearAsync' no devuelve
        // nada (la clave la puso el cliente, no la BD).
        await conexion.ExecuteAsync(sql, sede);
    }

    public async Task<int> ActualizarAsync(string idSede, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual: 'sp_actualizar_sede' reemplaza TODAS
        //    las columnas, así que un PATCH parcial necesita completar lo
        //    que no llegó con el valor que ya estaba guardado.
        const string sqlConsultar = "CALL catedras.sp_consultar_sede(@idSede, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idSede });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<Sede>(actualJson)!;

        // 2) Mezclar: lo que llegó en 'datos' PISA el valor actual; lo que
        //    no llegó se queda como estaba. Un PUT manda las 4 claves, así
        //    que para un PUT esto reemplaza todo igual que antes.
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string? direccion = datos.TryGetValue("direccion", out var vDireccion) ? (string?)vDireccion : actual.Direccion;
        bool esVirtual = datos.TryGetValue("es_virtual", out var vVirtual) ? (bool)vVirtual! : actual.EsVirtual;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_sede(
                @idSede, @nombre, @direccion, @esVirtual, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idSede, nombre, direccion, esVirtual, activo });

        // El procedimiento deja p_resultado en NULL si ya no existía
        // (carrera entre el paso 1 y el paso 3) — mismo "0 filas" de antes.
        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idSede)
    {
        const string sql = "CALL catedras.sp_eliminar_sede(@idSede, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idSede });

        // {"eliminado": true|false}. 'sede' SÍ tiene 'activo': este
        // "eliminar" es un UPDATE activo=false dentro del procedimiento,
        // no un DELETE — la FK fk_sesion_sede ya no puede bloquearlo.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
