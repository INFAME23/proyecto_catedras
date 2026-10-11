// ==========================================================================
//  Servicios/ServicioRolPorUsuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Reglas de negocio de 'RolPorUsuario' (ADR D16). Mismo molde que
//  ServicioVinculacionAsistente (v3).
//
//  'CerrarAsync' revisa, ANTES de pedir el UPDATE al repositorio:
//   1. que exista un rol con esa llave                -> si no, 404
//   2. que siga abierto ('fecha_fin' todavía null)    -> si no, 400
//   3. que 'fecha_fin' no sea anterior a 'fecha_ini'  -> si no, 400
//  La 3 también la vigila el CHECK chk_rol_usuario_fechas de la BD; aquí se
//  adelanta para responder con un mensaje claro.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'RolPorUsuario'.</summary>
public class ServicioRolPorUsuario : IServicioRolPorUsuario
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioRolPorUsuario _repositorio;

    public ServicioRolPorUsuario(IRepositorioRolPorUsuario repositorio)
    {
        _repositorio = repositorio;
    }

    public async Task<(List<RolPorUsuario> Datos, int Total)> ListarAsync(long? fkUsuario, int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(fkUsuario, limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<RolPorUsuario> CrearAsync(RolPorUsuarioCrear peticion)
    {
        var rol = new RolPorUsuario
        {
            FkUsuario = peticion.FkUsuario!.Value,
            FkRol = peticion.FkRol!,
            // Sin fecha -> hoy.
            FechaIni = peticion.FechaIni ?? DateOnly.FromDateTime(DateTime.Today),
        };

        await _repositorio.CrearAsync(rol);
        return rol;
    }

    public async Task<RolPorUsuario> CerrarAsync(RolPorUsuarioCerrar peticion)
    {
        long fkUsuario = peticion.FkUsuario!.Value;
        string fkRol = peticion.FkRol!;
        DateOnly fechaIni = peticion.FechaIni!.Value;
        DateOnly fechaFin = peticion.FechaFin ?? DateOnly.FromDateTime(DateTime.Today);

        // 1) ¿Existe? Se piden los roles de ese usuario y se busca el de la
        //    llave exacta. 'FirstOrDefault' devuelve null si ninguno cumple.
        var delUsuario = await _repositorio.ObtenerTodosAsync(fkUsuario, LimitePorDefecto);
        var actual = delUsuario.FirstOrDefault(r =>
            r.FkRol == fkRol && r.FechaIni == fechaIni);

        if (actual is null)
            throw new NoEncontradoExcepcion(
                $"No existe el rol '{fkRol}' del usuario '{fkUsuario}' iniciado el {fechaIni:yyyy-MM-dd}.");

        // 2) ¿Sigue abierto? Un rol cerrado no se "re-cierra".
        if (actual.FechaFin is not null)
            throw new ArgumentException(
                $"El rol ya estaba cerrado (fecha_fin: {actual.FechaFin:yyyy-MM-dd}).");

        // 3) Coherencia de fechas.
        if (fechaFin < fechaIni)
            throw new ArgumentException(
                "El campo 'fecha_fin' no puede ser anterior a 'fecha_ini'.");

        int filas = await _repositorio.CerrarAsync(fkUsuario, fkRol, fechaIni, fechaFin);

        // Si entre la revisión y el UPDATE otra petición lo cerró, el
        // repositorio devuelve 0 (su WHERE exige 'fecha_fin IS NULL').
        if (filas == 0)
            throw new ArgumentException("El rol ya estaba cerrado.");

        actual.FechaFin = fechaFin;
        return actual;
    }
}
