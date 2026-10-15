// ==========================================================================
//  Repositorios/RepositorioFacultadPostgres.cs  ·  Cátedras · RETROFIT criterio 3
// --------------------------------------------------------------------------
//  Los 5 verbos pasan por los procedimientos de
//  'db/postgres/migraciones/006_sp_facultad.sql'. Esta clase YA NO escribe
//  SELECT/INSERT/UPDATE/DELETE: arma el 'CALL', manda los parámetros y
//  convierte el JSON que devuelve el procedimiento en objetos C#. Es el
//  mismo molde de RepositorioSedePostgres.cs.
//
//  'IRepositorioFacultad', 'ServicioFacultad', 'FacultadController' y las
//  'Peticiones/' NO cambiaron: el PATCH parcial sigue entrando como
//  'Dictionary<string, object?>' y se mezcla AQUÍ con la fila actual.
//
//  'facultad' tiene 'activo': 'EliminarAsync' es un borrado LÓGICO (lo hace
//  el procedimiento), y el listado solo trae las facultades activas.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.facultad' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioFacultadPostgres : IRepositorioFacultad
{
    private readonly string _cadenaConexion;

    public RepositorioFacultadPostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<Facultad>> ObtenerTodosAsync(int limite, bool incluirInactivos)
    {
        // 'CALL' con un INOUT devuelve UNA fila con ese parámetro: se lee
        // como texto JSON.
        const string sql = "CALL catedras.sp_listar_facultad(@limite, @incluirInactivos, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { limite, incluirInactivos });

        // Siempre un arreglo ('[]' si no hay filas, nunca NULL).
        return JsonSerializer.Deserialize<List<Facultad>>(json!) ?? [];
    }

    public async Task<Facultad?> ObtenerPorIdAsync(string idFacultad)
    {
        const string sql = "CALL catedras.sp_consultar_facultad(@idFacultad, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idFacultad });

        // NULL = el procedimiento no encontró la fila.
        return json is null ? null : JsonSerializer.Deserialize<Facultad>(json);
    }

    public async Task CrearAsync(Facultad facultad)
    {
        const string sql = @"
            CALL catedras.sp_crear_facultad(
                @IdFacultad, @Nombre, @FkFacultadPadre, @Activo, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // Clave o nombre repetido, o padre inexistente -> NpgsqlException,
        // igual que antes. 'CrearAsync' no devuelve nada (la clave la puso
        // el cliente), así que no hace falta leer el JSON de vuelta.
        await conexion.ExecuteAsync(sql, facultad);
    }

    public async Task<int> ActualizarAsync(string idFacultad, Dictionary<string, object?> datos)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Traer la fila actual: 'sp_actualizar_facultad' reemplaza TODAS
        //    las columnas, así que un PATCH necesita completar lo que no
        //    llegó con el valor guardado.
        const string sqlConsultar = "CALL catedras.sp_consultar_facultad(@idFacultad, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(sqlConsultar, new { idFacultad });

        if (actualJson is null)
            return 0;   // no existe -> ni se intenta el UPDATE

        var actual = JsonSerializer.Deserialize<Facultad>(actualJson)!;

        // 2) Mezclar: lo que llegó en 'datos' PISA el valor actual (un PUT
        //    manda todas las claves, así que reemplaza todo; un 'null'
        //    explícito en el PUT sí borra el padre).
        string nombre = datos.TryGetValue("nombre", out var vNombre) ? (string)vNombre! : actual.Nombre;
        string? fkFacultadPadre = datos.TryGetValue("fk_facultad_padre", out var vPadre) ? (string?)vPadre : actual.FkFacultadPadre;
        bool activo = datos.TryGetValue("activo", out var vActivo) ? (bool)vActivo! : actual.Activo;

        // 3) Llamar al procedimiento con el conjunto COMPLETO.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_facultad(
                @idFacultad, @nombre, @fkFacultadPadre, @activo, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { idFacultad, nombre, fkFacultadPadre, activo });

        // NULL = ya no existía (carrera entre el paso 1 y el 3) -> "0 filas".
        return resultadoJson is null ? 0 : 1;
    }

    public async Task<int> EliminarAsync(string idFacultad)
    {
        const string sql = "CALL catedras.sp_eliminar_facultad(@idFacultad, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { idFacultad });

        // {"eliminado": true|false}. Es un UPDATE activo=false dentro del
        // procedimiento, no un DELETE.
        using var documento = JsonDocument.Parse(json!);
        bool eliminado = documento.RootElement.GetProperty("eliminado").GetBoolean();
        return eliminado ? 1 : 0;
    }
}
