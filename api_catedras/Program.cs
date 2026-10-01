// ==========================================================================
//  Program.cs  ·  Proyecto Cátedras · Versión 2
// --------------------------------------------------------------------------
//  Punto de entrada de la API. Aquí ocurren CUATRO cosas y nada más:
//    1. Se registran los controladores y se personaliza el 422.
//    2. Se registra Swagger (documentación en /swagger).
//    3. EL ENSAMBLADOR: la ÚNICA sección que conoce clases concretas
//       (RepositorioSedePostgres, ServicioSede). Todo lo demás recibe
//       interfaces por constructor (Artículo 3).
//    4. Se define la ruta de diagnóstico GET / (RF7) y se arranca.
//
//  Sin fábrica multi-motor ni selección de motor (Artículo 1 / RNF6): la
//  v1 tiene UN motor y el código lo dice. En v3, cuando exista un segundo
//  motor, SOLO esta sección se convierte en la fábrica real.
//
// --------------------------------------------------------------------------
//  PRINCIPIOS QUE SE VEN EN ESTE ARCHIVO (mapa; el detalle está en cada
//  bloque marcado más abajo y en docs/RESUMEN_SOLID_ACID.md):
//
//   · SOLID · S (Responsabilidad única): este archivo SOLO ensambla y
//     arranca. No tiene reglas de negocio ni SQL.
//   · SOLID · O (Abierto/Cerrado): agregar un segundo motor en v3 = crear
//     otra clase ': IRepositorioSede' y cambiar SOLO las líneas de registro
//     de abajo. Ningún controlador ni servicio se modifica.
//   · SOLID · D (Inversión de dependencias): este es el ÚNICO lugar que
//     nombra clases concretas (RepositorioSedePostgres, ServicioSede). El
//     resto del sistema recibe interfaces por constructor. Aquí se "cablea"
//     la abstracción a su implementación -> patrón Composition Root.
//
//  FLUJO DE UNA PETICIÓN — PASOS 0 a 2 (la cadena completa está numerada
//  en Controller, Servicio y Repositorio):
//   [FLUJO 0] Al arrancar, el contenedor de DI queda cargado con el mapa
//             IServicioSede->ServicioSede y IRepositorioSede->Repositorio...
//   [FLUJO 1] Llega el HTTP. El middleware de enrutado (app.MapControllers)
//             elige el método por los atributos [HttpGet]/[HttpPost]/...
//   [FLUJO 2] [ApiController] enlaza el body al objeto de 'Peticiones/' y
//             corre sus anotaciones. Si algo falla -> se dispara la fábrica
//             'InvalidModelStateResponseFactory' de abajo -> 422 y el
//             controlador NUNCA se ejecuta.
// ==========================================================================

using ApiCatedras.Repositorios;
using ApiCatedras.Servicios;
using Dapper;
using Microsoft.AspNetCore.Mvc;

// 'builder' junta configuración (appsettings.json + variables de entorno),
// registro de servicios (DI) y ajustes del servidor web.
var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------------------------------
//  Dapper: que los nombres con guion bajo de las columnas (es_virtual)
//  encajen con las propiedades PascalCase del modelo (EsVirtual).
// --------------------------------------------------------------------------
DefaultTypeMap.MatchNamesWithUnderscores = true;

// Dapper no trae de fábrica un manejador para 'DateOnly' (columnas DATE de
// Encuesta): se registra una vez aquí para 'DateOnly' y 'DateOnly?', y
// todos los repositorios que usan fechas quedan cubiertos.
var manejadorDateOnly = new ManejadorTipoDateOnly();
SqlMapper.AddTypeHandler(typeof(DateOnly), manejadorDateOnly);
SqlMapper.AddTypeHandler(typeof(DateOnly?), manejadorDateOnly);

// --------------------------------------------------------------------------
//  1. Controladores + respuesta 422 personalizada
// --------------------------------------------------------------------------
builder.Services.AddControllers();

// Cuando el body no cumple las anotaciones de la PETICIÓN, ASP.NET corta
// ANTES del controlador. Aquí definimos ese cuerpo: { estado, mensaje,
// errores:[{campo, mensaje}] } con código 422 (Artículo 8).
builder.Services.Configure<ApiBehaviorOptions>(opciones =>
{
    opciones.InvalidModelStateResponseFactory = contexto =>
    {
        // Aplana el ModelState a una lista { campo, mensaje }.
        var todos = contexto.ModelState
            .Where(par => par.Value is not null && par.Value.Errors.Count > 0)
            .SelectMany(par => par.Value!.Errors.Select(e => new
            {
                campo = LimpiarNombreCampo(par.Key),
                mensaje = LimpiarMensaje(e.ErrorMessage),
            }))
            .ToList();

        // Cuando falla el tipo de un campo, el binder además marca el
        // parámetro raíz como "obligatorio". Si ya hay errores de campos
        // concretos, ese genérico sobra: nos quedamos solo con los concretos.
        var concretos = todos
            .Where(x => x.campo != "cuerpo" && x.campo != "peticion")
            .ToList();
        var errores = concretos.Count > 0 ? concretos : todos;

        var cuerpo = new
        {
            estado = "peticion_invalida",
            mensaje = "El cuerpo de la petición no cumple las reglas de validación.",
            errores,
        };

        return new ObjectResult(cuerpo)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
        };
    };
});

// Deja el nombre del campo legible y SIEMPRE en el mismo estilo que el
// JSON (snake_case):
//   · "$.es_virtual"  (error de tipo, del deserializador) -> "es_virtual"
//   · "IdSede"        (error de anotación, nombre de propiedad C#) -> "id_sede"
//   · "$" o vacío     (error a nivel del cuerpo entero) -> "cuerpo"
static string LimpiarNombreCampo(string clave)
{
    if (string.IsNullOrEmpty(clave) || clave == "$") return "cuerpo";

    // Quita el prefijo de ruta JSON ("$.").
    if (clave.StartsWith("$.")) clave = clave[2..];

    // Convierte PascalCase -> snake_case: antes de cada mayúscula (que no
    // sea la primera ni venga ya de un '_') se mete un guion bajo, y todo
    // pasa a minúsculas. "IdSede" -> "id_sede"; "es_virtual" no cambia.
    var resultado = new System.Text.StringBuilder();
    for (int i = 0; i < clave.Length; i++)
    {
        char caracter = clave[i];
        if (char.IsUpper(caracter) && i > 0 && clave[i - 1] != '_')
            resultado.Append('_');
        resultado.Append(char.ToLowerInvariant(caracter));
    }
    return resultado.ToString();
}

// Traduce los mensajes técnicos del deserializador a algo entendible.
static string LimpiarMensaje(string mensaje)
{
    if (string.IsNullOrWhiteSpace(mensaje))
        return "Valor con formato o tipo inválido.";
    if (mensaje.Contains("could not be converted") || mensaje.Contains("JSON value"))
        return "El tipo del valor no es el esperado para este campo.";
    if (mensaje.Contains("field is required"))
        return "Campo obligatorio ausente.";
    return mensaje;
}

// --------------------------------------------------------------------------
//  2. Swagger (Swashbuckle)
// --------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------------------------------------------------------------
//  3. EL ENSAMBLADOR (registro de dependencias)
// --------------------------------------------------------------------------
// La cadena de conexión sale de appsettings.json o, en Docker, de la
// variable de entorno ConnectionStrings__Postgres.
var cadenaConexion = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Falta la cadena de conexión 'Postgres' " +
        "(appsettings.json o la variable ConnectionStrings__Postgres).");

// [SOLID · D] Aquí se resuelve la inversión de dependencias: se declara qué
// clase concreta responde a cada interfaz. Nadie más en el proyecto conoce
// estos nombres concretos.
// [SOLID · O] Para cambiar de motor NO se toca este archivo salvo esta
// línea: se apunta la interfaz a otra clase.
// [ACID · I] 'AddScoped' = una instancia (y una conexión lógica) por
// petición HTTP: dos peticiones simultáneas no comparten estado en memoria;
// el aislamiento real entre sus transacciones lo pone PostgreSQL.
//
// IRepositorioSede -> RepositorioSedePostgres (única implementación en v1).
builder.Services.AddScoped<IRepositorioSede>(
    _ => new RepositorioSedePostgres(cadenaConexion));

// IServicioSede -> ServicioSede. El framework le inyecta el IRepositorioSede
// de arriba al construirlo (inyección por constructor).
builder.Services.AddScoped<IServicioSede, ServicioSede>();

// IRepositorioModalidad -> RepositorioModalidadPostgres, IServicioModalidad
// -> ServicioModalidad. Mismo patrón que Sede.
builder.Services.AddScoped<IRepositorioModalidad>(
    _ => new RepositorioModalidadPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioModalidad, ServicioModalidad>();

// IRepositorioEstadoProceso -> RepositorioEstadoProcesoPostgres,
// IServicioEstadoProceso -> ServicioEstadoProceso.
builder.Services.AddScoped<IRepositorioEstadoProceso>(
    _ => new RepositorioEstadoProcesoPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioEstadoProceso, ServicioEstadoProceso>();

// IRepositorioEncuesta -> RepositorioEncuestaPostgres, IServicioEncuesta
// -> ServicioEncuesta.
builder.Services.AddScoped<IRepositorioEncuesta>(
    _ => new RepositorioEncuestaPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioEncuesta, ServicioEncuesta>();

// IRepositorioPeriodoAcademico -> RepositorioPeriodoAcademicoPostgres,
// IServicioPeriodoAcademico -> ServicioPeriodoAcademico.
builder.Services.AddScoped<IRepositorioPeriodoAcademico>(
    _ => new RepositorioPeriodoAcademicoPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioPeriodoAcademico, ServicioPeriodoAcademico>();

// IRepositorioRol -> RepositorioRolPostgres, IServicioRol -> ServicioRol.
builder.Services.AddScoped<IRepositorioRol>(
    _ => new RepositorioRolPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioRol, ServicioRol>();

// IRepositorioTipoDocumento -> RepositorioTipoDocumentoPostgres,
// IServicioTipoDocumento -> ServicioTipoDocumento.
builder.Services.AddScoped<IRepositorioTipoDocumento>(
    _ => new RepositorioTipoDocumentoPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioTipoDocumento, ServicioTipoDocumento>();

// IRepositorioTipoEvento -> RepositorioTipoEventoPostgres,
// IServicioTipoEvento -> ServicioTipoEvento.
builder.Services.AddScoped<IRepositorioTipoEvento>(
    _ => new RepositorioTipoEventoPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioTipoEvento, ServicioTipoEvento>();

// IRepositorioTipoPregunta -> RepositorioTipoPreguntaPostgres,
// IServicioTipoPregunta -> ServicioTipoPregunta.
// (Cierre de la v1: el controlador existía pero faltaba este registro, y
//  sin él cada petición a /api/TipoPregunta respondía 500.)
builder.Services.AddScoped<IRepositorioTipoPregunta>(
    _ => new RepositorioTipoPreguntaPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioTipoPregunta, ServicioTipoPregunta>();

// IRepositorioTipoVinculacion -> RepositorioTipoVinculacionPostgres,
// IServicioTipoVinculacion -> ServicioTipoVinculacion. (Cierre de la v1.)
builder.Services.AddScoped<IRepositorioTipoVinculacion>(
    _ => new RepositorioTipoVinculacionPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioTipoVinculacion, ServicioTipoVinculacion>();

// ---------------------------- Versión 2 ---------------------------------
// Las tablas de la v2 tienen foreign keys, pero eso NO cambia el
// ensamblador: cada entidad sigue siendo una pareja interfaz -> clase.

// IRepositorioFacultad -> RepositorioFacultadPostgres,
// IServicioFacultad -> ServicioFacultad (valida la autorreferencia).
builder.Services.AddScoped<IRepositorioFacultad>(
    _ => new RepositorioFacultadPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioFacultad, ServicioFacultad>();

// IRepositorioDependencia -> RepositorioDependenciaPostgres,
// IServicioDependencia -> ServicioDependencia (misma regla).
builder.Services.AddScoped<IRepositorioDependencia>(
    _ => new RepositorioDependenciaPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioDependencia, ServicioDependencia>();

// IRepositorioProgramaAcademico -> RepositorioProgramaAcademicoPostgres,
// IServicioProgramaAcademico -> ServicioProgramaAcademico.
builder.Services.AddScoped<IRepositorioProgramaAcademico>(
    _ => new RepositorioProgramaAcademicoPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioProgramaAcademico, ServicioProgramaAcademico>();

// IRepositorioAliasPrograma -> RepositorioAliasProgramaPostgres,
// IServicioAliasPrograma -> ServicioAliasPrograma (solo-anexo: sin actualizar).
builder.Services.AddScoped<IRepositorioAliasPrograma>(
    _ => new RepositorioAliasProgramaPostgres(cadenaConexion));
builder.Services.AddScoped<IServicioAliasPrograma, ServicioAliasPrograma>();

// --------------------------------------------------------------------------
//  4. Construir la app, exponer Swagger, ruta de diagnóstico y arrancar
// --------------------------------------------------------------------------
var app = builder.Build();

// Swagger siempre disponible (Artículo 8: /swagger).
app.UseSwagger();
app.UseSwaggerUI();

// RF7 · GET / -> JSON de diagnóstico.
app.MapGet("/", () => Results.Json(new
{
    mensaje = "API Cátedras en línea.",
    version = "v2",
    contratos = "docs/spec_kit/Versiones/version2/6_contracts.md",
}));

// Conecta las rutas de los controladores ([Route("api/Sede")]...).
app.MapControllers();

// Levanta el servidor y se queda escuchando.
app.Run();
