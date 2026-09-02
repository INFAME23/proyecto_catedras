// ==========================================================================
//  Program.cs  ·  Proyecto Cátedras · Versión 1
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

// IRepositorioSede -> RepositorioSedePostgres (única implementación en v1).
// 'AddScoped' = una instancia por petición HTTP.
builder.Services.AddScoped<IRepositorioSede>(
    _ => new RepositorioSedePostgres(cadenaConexion));

// IServicioSede -> ServicioSede. El framework le inyecta el IRepositorioSede
// de arriba al construirlo.
builder.Services.AddScoped<IServicioSede, ServicioSede>();

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
    version = "v1",
    contratos = "docs/spec_kit/6_contracts.md",
}));

// Conecta las rutas de los controladores ([Route("api/Sede")]...).
app.MapControllers();

// Levanta el servidor y se queda escuchando.
app.Run();
