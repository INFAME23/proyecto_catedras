// ==========================================================================
//  Repositorios/RepositorioProgramaAsistentePostgres.cs  ·  Cátedras · v3 + RETROFIT criterio 3
// --------------------------------------------------------------------------
//  IRepositorioProgramaAsistente para PostgreSQL, vía los procedimientos de
//  '027_sp_programa_asistente.sql'.
//
//  Tabla de LLAVE COMPUESTA (ADR D16): el contrato sigue con 3 operaciones.
//     ObtenerTodosAsync  -> sp_listar_programa_asistente
//     CrearAsync         -> sp_crear_programa_asistente
//     CambiarEstadoAsync -> sp_consultar_ + sp_actualizar_programa_asistente
//  'estado' es la única columna que no es llave: "cambiar estado" ES el
//  reemplazo completo, así que va por sp_actualizar_ con el mismo molde
//  de RepositorioSedePostgres.ActualizarAsync (consultar, mezclar, llamar).
//  sp_eliminar_programa_asistente (retiro lógico: estado = RETIRADO) existe
//  en la BD pero este contrato no lo usa: no hay EliminarAsync.
//
//  'fk_periodo' es CHAR(6) en la BD; 'text -> character' es un cast
//  implícito, así que el string viaja sin CAST.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;
using Dapper;
using Npgsql;
using System.Text.Json;

/// <summary>Acceso a 'catedras.programa_asistente' en PostgreSQL, vía procedimientos almacenados.</summary>
public class RepositorioProgramaAsistentePostgres : IRepositorioProgramaAsistente
{
    private readonly string _cadenaConexion;

    public RepositorioProgramaAsistentePostgres(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public async Task<List<ProgramaAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite)
    {
        const string sql = "CALL catedras.sp_listar_programa_asistente(@fkAsistente, @limite, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkAsistente, limite });

        return JsonSerializer.Deserialize<List<ProgramaAsistente>>(json!) ?? [];
    }

    public async Task CrearAsync(ProgramaAsistente programa)
    {
        // Llave repetida (RF111) o FK inexistente -> NpgsqlException (500).
        const string sql = @"
            CALL catedras.sp_crear_programa_asistente(
                @FkAsistente, @FkPrograma, @FkPeriodo, @Estado, NULL);";

        await using var conexion = new NpgsqlConnection(_cadenaConexion);
        await conexion.ExecuteAsync(sql, new
        {
            programa.FkAsistente,
            programa.FkPrograma,
            programa.FkPeriodo,
            programa.Estado,
        });
    }

    public async Task<int> CambiarEstadoAsync(long fkAsistente, string fkPrograma, string fkPeriodo, string estado)
    {
        await using var conexion = new NpgsqlConnection(_cadenaConexion);

        // 1) Fila actual por su llave completa.
        const string sqlConsultar = @"
            CALL catedras.sp_consultar_programa_asistente(
                @fkAsistente, @fkPrograma, @fkPeriodo, NULL);";
        string? actualJson = await conexion.QuerySingleAsync<string?>(
            sqlConsultar, new { fkAsistente, fkPrograma, fkPeriodo });

        if (actualJson is null)
            return 0;

        // 2) Mezclar: la única columna que no es llave es 'estado', y llega
        //    siempre (la petición la exige). La mezcla queda trivial, pero
        //    se mantiene el mismo paso del molde para que, si la tabla gana
        //    columnas, el lugar donde completarlas ya exista.
        // 3) Conjunto completo al procedimiento.
        const string sqlActualizar = @"
            CALL catedras.sp_actualizar_programa_asistente(
                @fkAsistente, @fkPrograma, @fkPeriodo, @estado, NULL);";
        string? resultadoJson = await conexion.QuerySingleAsync<string?>(
            sqlActualizar, new { fkAsistente, fkPrograma, fkPeriodo, estado });

        return resultadoJson is null ? 0 : 1;
    }
}
