// ==========================================================================
//  Servicios/ServicioUsuario.cs  ·  Proyecto Cátedras · Versión 5
// --------------------------------------------------------------------------
//  Reglas de negocio de 'Usuario'.
//
//  ESTE ES EL ÚNICO ARCHIVO DEL PROYECTO QUE LLAMA A BCRYPT (ADR D23, RNF13,
//  Guía IA Regla 3):
//   · BCrypt.Net.BCrypt.HashPassword(clave)  -> convierte la clave en hash.
//   · BCrypt.Net.BCrypt.Verify(clave, hash)  -> compara. NUNCA se comparan
//     dos hashes con '==': BCrypt mete una sal distinta en cada hash, así que
//     el mismo texto da hashes distintos cada vez y solo Verify sabe leerlos.
//  El controlador y el repositorio reciben y devuelven el hash como una
//  cadena cualquiera; jamás ven la clave en texto plano más que "de paso".
//
//  LO QUE NO HACE, a propósito (D9):
//   · NO revisa que 'usuario' esté libre ni que el asistente no tenga ya una
//     cuenta: lo garantizan uq_usuario_login y uq_usuario_persona (500).
//   · NO revisa que el asistente exista: la FK del motor (500).
//
//  LOGIN (RF139, ADR D27): usuario inexistente, cuenta inactiva y clave
//  incorrecta producen EXACTAMENTE el mismo resultado (null). Así quien
//  intenta entrar no puede averiguar qué usuarios existen.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Usuario'.</summary>
public class ServicioUsuario : IServicioUsuario
{
    private const int LimitePorDefecto = 1000;

    private readonly IRepositorioUsuario _repositorio;
    private readonly IRepositorioRolPorUsuario _repositorioRoles;
    private readonly IRepositorioCodigoRecuperacion _repositorioCodigos;

    // El login necesita los roles vigentes del usuario: por eso este servicio
    // recibe TAMBIÉN el repositorio de 'rol_por_usuario' (Fase 40).
    // v7.1: y el de 'codigo_recuperacion', porque recuperar la clave también
    // acaba en un hash BCrypt y ESTE es el único archivo que lo calcula.
    public ServicioUsuario(
        IRepositorioUsuario repositorio,
        IRepositorioRolPorUsuario repositorioRoles,
        IRepositorioCodigoRecuperacion repositorioCodigos)
    {
        _repositorio = repositorio;
        _repositorioRoles = repositorioRoles;
        _repositorioCodigos = repositorioCodigos;
    }

    // Único punto de hash del proyecto. El factor de costo es el que trae
    // BCrypt.Net por defecto (suficiente para este proyecto didáctico).
    private static string Hashear(string clave) => BCrypt.Net.BCrypt.HashPassword(clave);

    public async Task<(List<Usuario> Datos, int Total)> ListarAsync(int? limite)
    {
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo);
        return (datos, datos.Count);
    }

    public async Task<Usuario> ObtenerAsync(long idUsuario)
    {
        var usuario = await _repositorio.ObtenerPorIdAsync(idUsuario);

        if (usuario is null)
            throw new NoEncontradoExcepcion($"No existe el usuario '{idUsuario}'.");

        return usuario;
    }

    public async Task<(long IdUsuario, DateTime CreadoEn)> CrearAsync(UsuarioCrear peticion)
    {
        var usuario = new Usuario
        {
            FkAsistente = peticion.FkAsistente!.Value,
            NombreUsuario = peticion.Usuario!,
            ClaveHash = Hashear(peticion.Clave!),   // la clave en claro termina aquí
            Activo = peticion.Activo ?? true,
        };

        return await _repositorio.CrearAsync(usuario);
    }

    public async Task<int> ReemplazarAsync(long idUsuario, UsuarioReemplazo peticion)
    {
        // Un PUT reemplaza TODO, así que también fija una clave nueva.
        var datos = new Dictionary<string, object?>
        {
            ["fk_asistente"] = peticion.FkAsistente!.Value,
            ["usuario"] = peticion.Usuario!,
            ["clave_hash"] = Hashear(peticion.Clave!),
            ["activo"] = peticion.Activo!.Value,
        };

        int filas = await _repositorio.ActualizarAsync(idUsuario, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el usuario '{idUsuario}'.");

        return filas;
    }

    public async Task<int> ActualizarAsync(long idUsuario, UsuarioActualizar peticion)
    {
        var datos = peticion.ACamposEnviados();

        // Si llegó una clave nueva, se hashea y se agrega como 'clave_hash'.
        // Si NO llegó, el hash guardado no se toca (RF136).
        if (peticion.Clave is not null)
            datos["clave_hash"] = Hashear(peticion.Clave);

        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        int filas = await _repositorio.ActualizarAsync(idUsuario, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el usuario '{idUsuario}'.");

        return filas;
    }

    public async Task<int> EliminarAsync(long idUsuario)
    {
        int filas = await _repositorio.EliminarAsync(idUsuario);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe el usuario '{idUsuario}'.");

        return filas;
    }

    public async Task<(Usuario Usuario, List<string> Roles)?> LoginAsync(UsuarioLogin peticion)
    {
        var usuario = await _repositorio.ObtenerPorLoginAsync(peticion.Usuario!);

        // Tres causas, un solo resultado (ADR D27): no existe, está inactivo o
        // la clave no coincide. El '||' corta en la primera que se cumple; el
        // Verify solo se ejecuta si el usuario existe y está activo.
        if (usuario is null
            || !usuario.Activo
            || !BCrypt.Net.BCrypt.Verify(peticion.Clave!, usuario.ClaveHash))
            return null;

        await _repositorio.ActualizarUltimoAccesoAsync(usuario.IdUsuario);

        // El objeto se leyó ANTES de la actualización; se corrige aquí para que
        // la respuesta muestre el acceso que acaba de ocurrir.
        usuario.UltimoAcceso = DateTime.UtcNow;

        var roles = await _repositorioRoles.ObtenerVigentesAsync(usuario.IdUsuario);
        return (usuario, roles);
    }

    // ----------------------------------------------------------------------
    //  v7.1 · RF-S5 · Cambiar la clave sabiendo la actual
    // ----------------------------------------------------------------------
    public async Task<bool> CambiarClaveAsync(long idUsuario, UsuarioCambiarClave peticion)
    {
        var usuario = await _repositorio.ObtenerPorIdAsync(idUsuario)
            ?? throw new NoEncontradoExcepcion($"No existe el usuario '{idUsuario}'.");

        // ObtenerPorId no trae el hash (ADR D23); el único camino que lo trae
        // es el del login, de uso interno justamente para esto.
        var conHash = await _repositorio.ObtenerPorLoginAsync(usuario.NombreUsuario);

        if (conHash is null
            || !conHash.Activo
            || !BCrypt.Net.BCrypt.Verify(peticion.ClaveActual!, conHash.ClaveHash))
            return false;

        if (peticion.ClaveNueva == peticion.ClaveActual)
            throw new ArgumentException("La clave nueva debe ser distinta de la actual.");

        await _repositorio.ActualizarAsync(idUsuario,
            new Dictionary<string, object?> { ["clave_hash"] = Hashear(peticion.ClaveNueva!) });
        return true;
    }

    // ----------------------------------------------------------------------
    //  v7.1 · RF-S5 / RF-S6 · Recuperar una clave olvidada
    // ----------------------------------------------------------------------
    public async Task<(long IdCodigo, string Codigo, string EnviadoA, string Usuario)?> SolicitarRecuperacionAsync(
        RecuperacionSolicitar peticion)
    {
        string? ip = null;
        if (peticion.Ip is not null)
        {
            if (!System.Net.IPAddress.TryParse(peticion.Ip, out var direccion))
                throw new ArgumentException("El campo 'ip' no es una dirección IP válida.");
            ip = direccion.ToString();
        }

        return await _repositorioCodigos.SolicitarAsync(peticion.Identificador!.Trim(), ip);
    }

    public async Task RestablecerClaveAsync(RecuperacionRestablecer peticion)
    {
        string estado = await _repositorioCodigos.RestablecerAsync(
            peticion.IdCodigo!.Value, peticion.Codigo!, Hashear(peticion.ClaveNueva!));

        if (estado != "OK")
            throw new CodigoRecuperacionInvalidoExcepcion(estado);
    }
}
