// ==========================================================================
//  Servicios/ServicioSede.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Implementación de las reglas de negocio del recurso 'Sede'.
//
//  Recibe IRepositorioSede POR CONSTRUCTOR (lo inyecta el ensamblador de
//  Program.cs). No conoce HTTP ni el motor concreto: solo la interfaz.
//  Comunica los problemas con excepciones y el controlador las traduce:
//     ArgumentException     -> 400
//     NoEncontradoExcepcion -> 404
//
// --------------------------------------------------------------------------
//  PRINCIPIOS EN ESTE ARCHIVO:
//   · SOLID · S (Responsabilidad única): SOLO reglas de negocio (límite > 0,
//     body de PATCH no vacío, "no existe" -> 404). Ni HTTP ni SQL.
//   · SOLID · D (Inversión de dependencias): depende de IRepositorioSede
//     (campo _repositorio), nunca de RepositorioSedePostgres. Por eso la
//     prueba de 'pruebas/' puede darle un repositorio FALSO en memoria.
//   · SOLID · L (Sustitución de Liskov): el servicio se comporta igual con
//     CUALQUIER IRepositorioSede (Postgres real o falso): ambos respetan el
//     mismo contrato (devolver null / devolver 0 filas cuando no existe).
//   · SOLID · O (Abierto/Cerrado): añadir un verbo nuevo = añadir un método
//     nuevo; los existentes no se tocan.
//   · ACID (nota de alcance): la v1 hace UNA sentencia SQL por operación, así
//     que la atomicidad de cada operación la garantiza PostgreSQL en
//     autocommit. Este servicio NO abre transacciones porque no las
//     necesita. Si una regla futura tocara 2+ tablas, el 'BEGIN/COMMIT'
//     viviría AQUÍ (la capa que conoce la unidad de trabajo del negocio).
//
//  FLUJO DE UNA PETICIÓN — PASOS 4 y 5:
//   [FLUJO 4] Aplica la regla de negocio y transforma la 'Peticion' en lo
//             que el repositorio entiende (un 'Sede' o un diccionario
//             columna->valor).
//   [FLUJO 5] Llama a _repositorio.XxxAsync(...) por la interfaz.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;

/// <summary>Reglas de negocio de 'Sede'.</summary>
public class ServicioSede : IServicioSede
{
    // Límite por defecto de la lista cuando el cliente no manda '?limite='.
    private const int LimitePorDefecto = 1000;

    // [SOLID · D + L] El servicio depende de la INTERFAZ, nunca de
    // RepositorioSedePostgres. Cualquier implementación válida es
    // intercambiable sin cambiar una línea de esta clase.
    private readonly IRepositorioSede _repositorio;

    public ServicioSede(IRepositorioSede repositorio)
    {
        _repositorio = repositorio;
    }

    // ----------------------------------------------------------------------
    //  LISTAR
    // ----------------------------------------------------------------------
    public async Task<(List<Sede> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false)
    {
        // [FLUJO 4][SOLID · S] Regla de negocio: si no viene, 1000; si viene,
        // debe ser > 0. Esta decisión NO está en el controlador ni en la BD.
        int limiteEfectivo = limite ?? LimitePorDefecto;

        if (limiteEfectivo <= 0)
            throw new ArgumentException(
                "El parámetro 'limite' debe ser un entero mayor que 0.");

        // [FLUJO 5] Baja a la capa de datos por la interfaz.
        var datos = await _repositorio.ObtenerTodosAsync(limiteEfectivo, incluirInactivos);

        // 'total' = cuántas filas se devuelven en esta respuesta.
        return (datos, datos.Count);
    }

    // ----------------------------------------------------------------------
    //  OBTENER POR CÓDIGO
    // ----------------------------------------------------------------------
    public async Task<Sede> ObtenerAsync(string idSede)
    {
        var sede = await _repositorio.ObtenerPorIdAsync(idSede);

        // 'null' del repositorio = no existe -> el controlador hará 404.
        if (sede is null)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return sede;
    }

    // ----------------------------------------------------------------------
    //  CREAR (POST)
    // ----------------------------------------------------------------------
    public async Task CrearAsync(SedeCrear peticion)
    {
        // La petición ya pasó la validación de forma/tipo del framework:
        // aquí id_sede, nombre, es_virtual y activo NO son null. El '!' le
        // dice al compilador "confío en que tiene valor"; '.Value' saca el
        // bool de dentro del bool?.
        var sede = new Sede
        {
            IdSede = peticion.IdSede!,
            Nombre = peticion.Nombre!,
            Direccion = peticion.Direccion,          // opcional: puede ir null
            EsVirtual = peticion.EsVirtual!.Value,
            Activo = peticion.Activo!.Value,
            // v7.2: ubicación y radio (opcionales). Una sede virtual no lleva ubicación:
            // aunque llegara, se descarta para no chocar con 'chk_sede_virtual_sin_geo'.
            Latitud = peticion.EsVirtual == true ? null : peticion.Latitud,
            Longitud = peticion.EsVirtual == true ? null : peticion.Longitud,
            RadioMetros = peticion.EsVirtual == true ? null : peticion.RadioMetros,
        };

        await _repositorio.CrearAsync(sede);
    }

    // ----------------------------------------------------------------------
    //  REEMPLAZAR (PUT) — reemplazo COMPLETO: se escriben las 4 columnas
    // ----------------------------------------------------------------------
    public async Task<int> ReemplazarAsync(string idSede, SedeReemplazo peticion)
    {
        var datos = new Dictionary<string, object?>
        {
            ["nombre"]     = peticion.Nombre!,
            ["direccion"]  = peticion.Direccion,     // si no vino -> NULL en la columna
            ["es_virtual"] = peticion.EsVirtual!.Value,
            ["activo"]     = peticion.Activo!.Value,
        };

        // v7.2: la ubicación solo cambia si el body la trae (así un PUT de la v7.1, que no la
        // conoce, no borra las coordenadas). Una sede que pasa a ser virtual las pierde.
        AplicarUbicacion(datos, peticion.EsVirtual!.Value, peticion.Latitud, peticion.Longitud, peticion.RadioMetros);

        // [FLUJO 5] Una sola sentencia UPDATE. [ACID · A] PostgreSQL la
        // aplica entera o nada: las 4 columnas cambian juntas.
        int filas = await _repositorio.ActualizarAsync(idSede, datos);

        // [ACID · C] 0 filas = la fila no existe -> no se inventó nada; se
        // avisa con 404.
        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return filas;
    }

    // ----------------------------------------------------------------------
    //  ACTUALIZAR (PATCH) — parcial: solo las columnas enviadas
    // ----------------------------------------------------------------------
    public async Task<int> ActualizarAsync(string idSede, SedeActualizar peticion)
    {
        // La petición arma el diccionario solo con los campos que llegaron.
        var datos = peticion.ACamposEnviados();

        // [FLUJO 4][SOLID · S] Regla de negocio (no de forma): body sin
        // campos -> 400. El framework no puede saber esto; el servicio sí.
        if (datos.Count == 0)
            throw new ArgumentException(
                "El cuerpo del PATCH no trae ningún campo para actualizar.");

        // v7.2: si el PATCH vuelve virtual a la sede, se borra su ubicación (el CHECK de la BD no la admite).
        if (peticion.EsVirtual == true)
            AplicarUbicacion(datos, true, null, null, null);

        int filas = await _repositorio.ActualizarAsync(idSede, datos);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return filas;
    }

    // ----------------------------------------------------------------------
    //  Ubicación de la sede (v7.2): qué claves viajan al repositorio.
    //  Que una clave esté en el diccionario significa "cámbiala"; si falta, se conserva.
    // ----------------------------------------------------------------------
    private static void AplicarUbicacion(
        Dictionary<string, object?> datos, bool esVirtual, decimal? latitud, decimal? longitud, int? radioMetros)
    {
        if (esVirtual)
        {
            // Una sede virtual no está en ningún lugar: se limpian los tres (null explícito).
            datos["latitud"] = null;
            datos["longitud"] = null;
            datos["radio_metros"] = null;
            return;
        }

        if (latitud is not null) datos["latitud"] = latitud;
        if (longitud is not null) datos["longitud"] = longitud;
        if (radioMetros is not null) datos["radio_metros"] = radioMetros;
    }

    // ----------------------------------------------------------------------
    //  ELIMINAR (DELETE)
    // ----------------------------------------------------------------------
    public async Task<int> EliminarAsync(string idSede)
    {
        int filas = await _repositorio.EliminarAsync(idSede);

        if (filas == 0)
            throw new NoEncontradoExcepcion($"No existe la sede '{idSede}'.");

        return filas;
    }
}
