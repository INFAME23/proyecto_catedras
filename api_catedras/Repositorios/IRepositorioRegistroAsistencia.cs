// ==========================================================================
//  Repositorios/IRepositorioRegistroAsistencia.cs  ·  Cátedras · v5
// --------------------------------------------------------------------------
//  Contrato de acceso a datos de 'catedras.registro_asistencia'.
//
//  Solo lectura + alta manual (RF154-RF157). Un registro es un HECHO
//  auditable: no hay Reemplazar, Actualizar ni Eliminar.
// ==========================================================================

namespace ApiCatedras.Repositorios;

using ApiCatedras.Modelos;

/// <summary>Operaciones de datos sobre 'catedras.registro_asistencia'.</summary>
public interface IRepositorioRegistroAsistencia
{
    /// <summary>Registros, hasta 'limite'. Ambos filtros son opcionales y combinables.</summary>
    Task<List<RegistroAsistencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite);

    Task<RegistroAsistencia?> ObtenerPorIdAsync(long idRegistro);

    /// <summary>Inserta un registro MANUAL/IMPORTADO (sin fk_clave ni fk_enlace) y devuelve
    /// la fila con lo que decidió la BD (id, hora y las 2 instantáneas del trigger).
    /// Asistente sin vinculación vigente, sesión/asistente inexistente, registro repetido
    /// o sin cupo -> NpgsqlException (500).</summary>
    Task<RegistroAsistencia> CrearAsync(long fkSesion, long fkAsistente, string origen);
}
