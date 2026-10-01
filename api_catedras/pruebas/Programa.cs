// ==========================================================================
//  pruebas/Programa.cs  ·  Proyecto Cátedras · Versiones 1 y 2
//  (criterio 6 de la v1 · criterio 8 de la v2)
// --------------------------------------------------------------------------
//  "Prueba de capas": se construye ServicioSede pasándole un repositorio
//  FALSO que guarda las sedes en un diccionario en memoria (implementa
//  IRepositorioSede, sin PostgreSQL). Si el servicio funciona así, es
//  porque depende de la INTERFAZ y no del motor: las capas están bien
//  cortadas.
//
//  Ejecutar (desde la carpeta api_catedras):
//      dotnet run --project pruebas
//  Sale con código 0 si todo pasa; 1 si algo falla.
//
//  [SOLID · L] Este archivo ES la demostración de la Sustitución de Liskov:
//  se le pasa a ServicioSede un 'RepositorioSedeFalso' en lugar del de
//  Postgres y TODAS las pruebas pasan igual. El servicio no distingue una
//  implementación de la otra porque ambas respetan el contrato
//  IRepositorioSede.
//  [SOLID · D] Solo es posible porque ServicioSede depende de la interfaz,
//  no de RepositorioSedePostgres (si dependiera de la clase concreta, esta
//  prueba necesitaría PostgreSQL).
// ==========================================================================

using ApiCatedras.Excepciones;
using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;
using ApiCatedras.Repositorios;
using ApiCatedras.Servicios;

// --------------------------------------------------------------------------
//  Instrumentación mínima de pruebas (sin librerías: Artículo 2).
// --------------------------------------------------------------------------
int pasadas = 0;
int fallidas = 0;

void Afirmar(string caso, bool condicion)
{
    Console.WriteLine((condicion ? "  OK    " : "  FALLA ") + caso);
    if (condicion) pasadas++;
    else fallidas++;
}

async Task AfirmarLanza<TExcepcion>(string caso, Func<Task> accion)
    where TExcepcion : Exception
{
    try
    {
        await accion();
        Afirmar(caso, false); // no lanzó: falla
    }
    catch (TExcepcion)
    {
        Afirmar(caso, true);  // lanzó lo esperado: pasa
    }
}

// --------------------------------------------------------------------------
//  El sujeto de prueba: el servicio real con el repositorio FALSO.
// --------------------------------------------------------------------------
var servicio = new ServicioSede(new RepositorioSedeFalso());

Console.WriteLine("== Prueba de capas: ServicioSede + repositorio falso en memoria ==\n");

// 1 · Listar devuelve las sedes semilla del repo falso.
var (datos, total) = await servicio.ListarAsync(null);
Afirmar("Listar() devuelve las 2 sedes semilla", total == 2 && datos.Count == 2);

// 2 · limite <= 0 -> ArgumentException (el controlador haría 400).
await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicio.ListarAsync(0));

// 3 · Obtener una sede inexistente -> NoEncontradoExcepcion (404).
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicio.ObtenerAsync("NO_VA"));

// 4 · Crear + Obtener.
await servicio.CrearAsync(new SedeCrear
{
    IdSede = "BELLO",
    Nombre = "Campus Bello",
    Direccion = "Calle 45 61-40, Bello",
    EsVirtual = false,
    Activo = true,
});
var creada = await servicio.ObtenerAsync("BELLO");
Afirmar("Crear() inserta y Obtener() la encuentra", creada.Nombre == "Campus Bello");

// 5 · PUT reemplaza los 4 campos (incluida 'direccion' a null).
int filasPut = await servicio.ReemplazarAsync("BELLO", new SedeReemplazo
{
    Nombre = "Bello (renombrada)",
    Direccion = null,
    EsVirtual = true,
    Activo = false,
});
var trasPut = await servicio.ObtenerAsync("BELLO");
Afirmar("PUT reemplaza todo",
    filasPut == 1 && trasPut.Nombre == "Bello (renombrada)"
    && trasPut.EsVirtual && !trasPut.Activo && trasPut.Direccion is null);

// 6 · PATCH toca SOLO el campo enviado.
int filasPatch = await servicio.ActualizarAsync("BELLO", new SedeActualizar { Activo = true });
var trasPatch = await servicio.ObtenerAsync("BELLO");
Afirmar("PATCH cambia solo 'activo' y deja 'nombre' igual",
    filasPatch == 1 && trasPatch.Activo && trasPatch.Nombre == "Bello (renombrada)");

// 7 · PATCH con body vacío -> ArgumentException (el controlador haría 400).
await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicio.ActualizarAsync("BELLO", new SedeActualizar()));

// 8 · PATCH sobre una sede inexistente -> NoEncontradoExcepcion (404).
await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicio.ActualizarAsync("NO_VA", new SedeActualizar { Activo = false }));

// 9 · DELETE elimina; el segundo DELETE -> NoEncontradoExcepcion (404).
int eliminadas = await servicio.EliminarAsync("BELLO");
Afirmar("DELETE elimina 1 fila", eliminadas == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicio.EliminarAsync("BELLO"));

// ============================================================================
//  Prueba de capas: ServicioModalidad + repositorio falso en memoria
// ============================================================================
var servicioModalidad = new ServicioModalidad(new RepositorioModalidadFalso());

Console.WriteLine("\n== Prueba de capas: ServicioModalidad + repositorio falso en memoria ==\n");

var (datosMod, totalMod) = await servicioModalidad.ListarAsync(null);
Afirmar("Listar() devuelve las 4 modalidades semilla", totalMod == 4 && datosMod.Count == 4);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioModalidad.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioModalidad.ObtenerAsync("NO_VA"));

await servicioModalidad.CrearAsync(new ModalidadCrear
{
    IdModalidad = "ASINCRONA",
    Nombre = "Asincrónica",
    CanalDifusion = "ENLACE",
});
var modalidadCreada = await servicioModalidad.ObtenerAsync("ASINCRONA");
Afirmar("Crear() aplica 'activo=true' por defecto y Obtener() la encuentra",
    modalidadCreada.Nombre == "Asincrónica" && modalidadCreada.Activo);

int filasPutMod = await servicioModalidad.ReemplazarAsync("ASINCRONA", new ModalidadReemplazo
{
    Nombre = "Asincrónica (renombrada)",
    CanalDifusion = "AMBOS",
    Activo = false,
});
var trasPutMod = await servicioModalidad.ObtenerAsync("ASINCRONA");
Afirmar("PUT reemplaza todo",
    filasPutMod == 1 && trasPutMod.Nombre == "Asincrónica (renombrada)"
    && trasPutMod.CanalDifusion == "AMBOS" && !trasPutMod.Activo);

int filasPatchMod = await servicioModalidad.ActualizarAsync("ASINCRONA", new ModalidadActualizar { Activo = true });
var trasPatchMod = await servicioModalidad.ObtenerAsync("ASINCRONA");
Afirmar("PATCH cambia solo 'activo' y deja 'nombre' igual",
    filasPatchMod == 1 && trasPatchMod.Activo && trasPatchMod.Nombre == "Asincrónica (renombrada)");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioModalidad.ActualizarAsync("ASINCRONA", new ModalidadActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioModalidad.ActualizarAsync("NO_VA", new ModalidadActualizar { Activo = false }));

int eliminadasMod = await servicioModalidad.EliminarAsync("ASINCRONA");
Afirmar("DELETE elimina 1 fila", eliminadasMod == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioModalidad.EliminarAsync("ASINCRONA"));

// ============================================================================
//  Prueba de capas: ServicioEstadoProceso + repositorio falso en memoria
// ============================================================================
var servicioEstado = new ServicioEstadoProceso(new RepositorioEstadoProcesoFalso());

Console.WriteLine("\n== Prueba de capas: ServicioEstadoProceso + repositorio falso en memoria ==\n");

var (datosEdo, totalEdo) = await servicioEstado.ListarAsync(null);
Afirmar("Listar() devuelve los 6 estados semilla", totalEdo == 6 && datosEdo.Count == 6);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioEstado.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioEstado.ObtenerAsync("NO_VA"));

// Regla de coherencia (chk_estado_coherente): exitoso=true exige final=true.
await AfirmarLanza<ArgumentException>(
    "Crear con es_exitoso=true y es_final=false lanza ArgumentException",
    () => servicioEstado.CrearAsync(new EstadoProcesoCrear
    {
        IdEstado = "TEST",
        Nombre = "Prueba",
        EsFinal = false,
        EsExitoso = true,
    }));

await servicioEstado.CrearAsync(new EstadoProcesoCrear
{
    IdEstado = "TEST",
    Nombre = "Prueba",
    EsFinal = true,
    EsExitoso = true,
});
var estadoCreado = await servicioEstado.ObtenerAsync("TEST");
Afirmar("Crear() inserta y Obtener() la encuentra", estadoCreado.Nombre == "Prueba");

await AfirmarLanza<ArgumentException>(
    "PUT con coherencia violada lanza ArgumentException",
    () => servicioEstado.ReemplazarAsync("TEST", new EstadoProcesoReemplazo
    {
        Nombre = "Prueba",
        EsFinal = false,
        EsExitoso = true,
    }));

int filasPutEdo = await servicioEstado.ReemplazarAsync("TEST", new EstadoProcesoReemplazo
{
    Nombre = "Prueba (renombrada)",
    EsFinal = false,
    EsExitoso = false,
});
var trasPutEdo = await servicioEstado.ObtenerAsync("TEST");
Afirmar("PUT reemplaza todo",
    filasPutEdo == 1 && trasPutEdo.Nombre == "Prueba (renombrada)"
    && !trasPutEdo.EsFinal && !trasPutEdo.EsExitoso);

int filasPatchEdo = await servicioEstado.ActualizarAsync("TEST", new EstadoProcesoActualizar { EsFinal = true });
var trasPatchEdo = await servicioEstado.ObtenerAsync("TEST");
Afirmar("PATCH cambia solo 'es_final' y deja 'nombre' igual",
    filasPatchEdo == 1 && trasPatchEdo.EsFinal && trasPatchEdo.Nombre == "Prueba (renombrada)");

await AfirmarLanza<ArgumentException>(
    "PATCH con los dos campos violando coherencia lanza ArgumentException",
    () => servicioEstado.ActualizarAsync("TEST", new EstadoProcesoActualizar { EsFinal = false, EsExitoso = true }));

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioEstado.ActualizarAsync("TEST", new EstadoProcesoActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioEstado.ActualizarAsync("NO_VA", new EstadoProcesoActualizar { EsFinal = false }));

int eliminadasEdo = await servicioEstado.EliminarAsync("TEST");
Afirmar("DELETE elimina 1 fila", eliminadasEdo == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioEstado.EliminarAsync("TEST"));

// ============================================================================
//  Prueba de capas: ServicioEncuesta + repositorio falso en memoria
// ============================================================================
var servicioEncuesta = new ServicioEncuesta(new RepositorioEncuestaFalso());

Console.WriteLine("\n== Prueba de capas: ServicioEncuesta + repositorio falso en memoria ==\n");

var (datosEnc, totalEnc) = await servicioEncuesta.ListarAsync(null);
Afirmar("Listar() devuelve la encuesta semilla", totalEnc == 1 && datosEnc.Count == 1);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioEncuesta.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioEncuesta.ObtenerAsync(999));

// Regla de coherencia (chk_encuesta_vig): vigente_hasta no puede ser anterior a vigente_desde.
await AfirmarLanza<ArgumentException>(
    "Crear con vigente_hasta anterior a vigente_desde lanza ArgumentException",
    () => servicioEncuesta.CrearAsync(new EncuestaCrear
    {
        Nombre = "Prueba",
        VigenteDesde = new DateOnly(2026, 5, 1),
        VigenteHasta = new DateOnly(2026, 1, 1),
    }));

// Crear() SIN version/vigente_desde/activa -> debe aplicar los defaults
// (version=1, vigente_desde=hoy, activa=true) e id autogenerado por el
// repositorio falso (simulando la IDENTITY de la BD real).
long idEncuestaCreada = await servicioEncuesta.CrearAsync(new EncuestaCrear { Nombre = "Prueba" });
var encuestaCreada = await servicioEncuesta.ObtenerAsync(idEncuestaCreada);
Afirmar("Crear() aplica los valores por defecto y devuelve el id generado",
    idEncuestaCreada == 2 && encuestaCreada.Version == 1 && encuestaCreada.Activa
    && encuestaCreada.VigenteDesde == DateOnly.FromDateTime(DateTime.Now));

int filasPutEnc = await servicioEncuesta.ReemplazarAsync(idEncuestaCreada, new EncuestaReemplazo
{
    Nombre = "Prueba (renombrada)",
    Version = 2,
    VigenteDesde = new DateOnly(2026, 1, 1),
    VigenteHasta = null,
    Activa = false,
});
var trasPutEnc = await servicioEncuesta.ObtenerAsync(idEncuestaCreada);
Afirmar("PUT reemplaza todo",
    filasPutEnc == 1 && trasPutEnc.Nombre == "Prueba (renombrada)"
    && trasPutEnc.Version == 2 && !trasPutEnc.Activa);

int filasPatchEnc = await servicioEncuesta.ActualizarAsync(idEncuestaCreada, new EncuestaActualizar { Activa = true });
var trasPatchEnc = await servicioEncuesta.ObtenerAsync(idEncuestaCreada);
Afirmar("PATCH cambia solo 'activa' y deja 'nombre' igual",
    filasPatchEnc == 1 && trasPatchEnc.Activa && trasPatchEnc.Nombre == "Prueba (renombrada)");

await AfirmarLanza<ArgumentException>(
    "PATCH con las dos fechas invertidas lanza ArgumentException",
    () => servicioEncuesta.ActualizarAsync(idEncuestaCreada, new EncuestaActualizar
    {
        VigenteDesde = new DateOnly(2026, 5, 1),
        VigenteHasta = new DateOnly(2026, 1, 1),
    }));

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioEncuesta.ActualizarAsync(idEncuestaCreada, new EncuestaActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a un id inexistente lanza NoEncontradoExcepcion",
    () => servicioEncuesta.ActualizarAsync(999, new EncuestaActualizar { Activa = false }));

int eliminadasEnc = await servicioEncuesta.EliminarAsync(idEncuestaCreada);
Afirmar("DELETE elimina 1 fila", eliminadasEnc == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioEncuesta.EliminarAsync(idEncuestaCreada));

// ============================================================================
//  Prueba de capas: ServicioPeriodoAcademico + repositorio falso en memoria
// ============================================================================
var servicioPeriodo = new ServicioPeriodoAcademico(new RepositorioPeriodoAcademicoFalso());

Console.WriteLine("\n== Prueba de capas: ServicioPeriodoAcademico + repositorio falso en memoria ==\n");

var (datosPer, totalPer) = await servicioPeriodo.ListarAsync(null);
Afirmar("Listar() devuelve los 4 periodos semilla", totalPer == 4 && datosPer.Count == 4);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioPeriodo.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioPeriodo.ObtenerAsync("NO_VA"));

// Regla de coherencia (chk_periodo_fechas): fecha_fin debe ser posterior a fecha_ini.
await AfirmarLanza<ArgumentException>(
    "Crear con fecha_fin <= fecha_ini lanza ArgumentException",
    () => servicioPeriodo.CrearAsync(new PeriodoAcademicoCrear
    {
        IdPeriodo = "2027-1",
        FechaIni = new DateOnly(2027, 6, 1),
        FechaFin = new DateOnly(2027, 1, 1),
    }));

await servicioPeriodo.CrearAsync(new PeriodoAcademicoCrear
{
    IdPeriodo = "2027-1",
    FechaIni = new DateOnly(2027, 1, 19),
    FechaFin = new DateOnly(2027, 6, 6),
});
var periodoCreado = await servicioPeriodo.ObtenerAsync("2027-1");
Afirmar("Crear() inserta y Obtener() lo encuentra", periodoCreado.Activo);

await AfirmarLanza<ArgumentException>(
    "PUT con fecha_fin <= fecha_ini lanza ArgumentException",
    () => servicioPeriodo.ReemplazarAsync("2027-1", new PeriodoAcademicoReemplazo
    {
        FechaIni = new DateOnly(2027, 6, 1),
        FechaFin = new DateOnly(2027, 1, 1),
        Activo = true,
    }));

int filasPutPer = await servicioPeriodo.ReemplazarAsync("2027-1", new PeriodoAcademicoReemplazo
{
    FechaIni = new DateOnly(2027, 1, 20),
    FechaFin = new DateOnly(2027, 6, 7),
    Activo = false,
});
var trasPutPer = await servicioPeriodo.ObtenerAsync("2027-1");
Afirmar("PUT reemplaza todo", filasPutPer == 1 && !trasPutPer.Activo);

int filasPatchPer = await servicioPeriodo.ActualizarAsync("2027-1", new PeriodoAcademicoActualizar { Activo = true });
var trasPatchPer = await servicioPeriodo.ObtenerAsync("2027-1");
Afirmar("PATCH cambia solo 'activo'", filasPatchPer == 1 && trasPatchPer.Activo);

await AfirmarLanza<ArgumentException>(
    "PATCH con las dos fechas invertidas lanza ArgumentException",
    () => servicioPeriodo.ActualizarAsync("2027-1", new PeriodoAcademicoActualizar
    {
        FechaIni = new DateOnly(2027, 6, 1),
        FechaFin = new DateOnly(2027, 1, 1),
    }));

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioPeriodo.ActualizarAsync("2027-1", new PeriodoAcademicoActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioPeriodo.ActualizarAsync("NO_VA", new PeriodoAcademicoActualizar { Activo = false }));

int eliminadasPer = await servicioPeriodo.EliminarAsync("2027-1");
Afirmar("DELETE elimina 1 fila", eliminadasPer == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioPeriodo.EliminarAsync("2027-1"));

// ============================================================================
//  Prueba de capas: ServicioRol + repositorio falso en memoria
// ============================================================================
var servicioRol = new ServicioRol(new RepositorioRolFalso());

Console.WriteLine("\n== Prueba de capas: ServicioRol + repositorio falso en memoria ==\n");

var (datosRol, totalRol) = await servicioRol.ListarAsync(null);
Afirmar("Listar() devuelve los 3 roles semilla", totalRol == 3 && datosRol.Count == 3);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioRol.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioRol.ObtenerAsync("NO_VA"));

await servicioRol.CrearAsync(new RolCrear
{
    IdRol = "PONENTE",
    Nombre = "Ponente",
    Descripcion = "Dicta cátedras",
});
var rolCreado = await servicioRol.ObtenerAsync("PONENTE");
Afirmar("Crear() inserta y Obtener() lo encuentra", rolCreado.Nombre == "Ponente");

int filasPutRol = await servicioRol.ReemplazarAsync("PONENTE", new RolReemplazo
{
    Nombre = "Ponente externo",
    Descripcion = null,
});
var trasPutRol = await servicioRol.ObtenerAsync("PONENTE");
Afirmar("PUT reemplaza todo (incluida 'descripcion' a null)",
    filasPutRol == 1 && trasPutRol.Nombre == "Ponente externo" && trasPutRol.Descripcion is null);

int filasPatchRol = await servicioRol.ActualizarAsync("PONENTE", new RolActualizar { Descripcion = "Actualizada" });
var trasPatchRol = await servicioRol.ObtenerAsync("PONENTE");
Afirmar("PATCH cambia solo 'descripcion' y deja 'nombre' igual",
    filasPatchRol == 1 && trasPatchRol.Descripcion == "Actualizada" && trasPatchRol.Nombre == "Ponente externo");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioRol.ActualizarAsync("PONENTE", new RolActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioRol.ActualizarAsync("NO_VA", new RolActualizar { Nombre = "x" }));

int eliminadasRol = await servicioRol.EliminarAsync("PONENTE");
Afirmar("DELETE elimina 1 fila", eliminadasRol == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioRol.EliminarAsync("PONENTE"));

// ============================================================================
//  Prueba de capas: ServicioTipoDocumento + repositorio falso en memoria
// ============================================================================
var servicioTipoDoc = new ServicioTipoDocumento(new RepositorioTipoDocumentoFalso());

Console.WriteLine("\n== Prueba de capas: ServicioTipoDocumento + repositorio falso en memoria ==\n");

var (datosTdoc, totalTdoc) = await servicioTipoDoc.ListarAsync(null);
Afirmar("Listar() devuelve los 6 tipos de documento semilla", totalTdoc == 6 && datosTdoc.Count == 6);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioTipoDoc.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioTipoDoc.ObtenerAsync("NO_VA"));

await servicioTipoDoc.CrearAsync(new TipoDocumentoCrear
{
    IdTipoDocumento = "RC",
    Nombre = "Registro civil",
});
var tdocCreado = await servicioTipoDoc.ObtenerAsync("RC");
Afirmar("Crear() aplica 'activo=true' por defecto y Obtener() lo encuentra",
    tdocCreado.Nombre == "Registro civil" && tdocCreado.Activo);

int filasPutTdoc = await servicioTipoDoc.ReemplazarAsync("RC", new TipoDocumentoReemplazo
{
    Nombre = "Registro civil de nacimiento",
    Activo = false,
});
var trasPutTdoc = await servicioTipoDoc.ObtenerAsync("RC");
Afirmar("PUT reemplaza todo",
    filasPutTdoc == 1 && trasPutTdoc.Nombre == "Registro civil de nacimiento" && !trasPutTdoc.Activo);

int filasPatchTdoc = await servicioTipoDoc.ActualizarAsync("RC", new TipoDocumentoActualizar { Activo = true });
var trasPatchTdoc = await servicioTipoDoc.ObtenerAsync("RC");
Afirmar("PATCH cambia solo 'activo' y deja 'nombre' igual",
    filasPatchTdoc == 1 && trasPatchTdoc.Activo && trasPatchTdoc.Nombre == "Registro civil de nacimiento");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioTipoDoc.ActualizarAsync("RC", new TipoDocumentoActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioTipoDoc.ActualizarAsync("NO_VA", new TipoDocumentoActualizar { Activo = false }));

int eliminadasTdoc = await servicioTipoDoc.EliminarAsync("RC");
Afirmar("DELETE elimina 1 fila", eliminadasTdoc == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioTipoDoc.EliminarAsync("RC"));

// ============================================================================
//  Prueba de capas: ServicioTipoEvento + repositorio falso en memoria
// ============================================================================
var servicioTipoEvt = new ServicioTipoEvento(new RepositorioTipoEventoFalso());

Console.WriteLine("\n== Prueba de capas: ServicioTipoEvento + repositorio falso en memoria ==\n");

var (datosTevt, totalTevt) = await servicioTipoEvt.ListarAsync(null);
Afirmar("Listar() devuelve los 8 tipos de evento semilla", totalTevt == 8 && datosTevt.Count == 8);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioTipoEvt.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioTipoEvt.ObtenerAsync("NO_VA"));

await servicioTipoEvt.CrearAsync(new TipoEventoCrear
{
    IdTipoEvento = "TEST",
    Nombre = "Prueba",
});
var tevtCreado = await servicioTipoEvt.ObtenerAsync("TEST");
Afirmar("Crear() inserta y Obtener() lo encuentra", tevtCreado.Nombre == "Prueba");

// A diferencia de Sede/Rol/TipoDocumento, 'nombre' NO es único aquí:
// crear otro tipo de evento con el MISMO nombre debe funcionar sin problema.
await servicioTipoEvt.CrearAsync(new TipoEventoCrear
{
    IdTipoEvento = "TES2",
    Nombre = "Prueba",
});
Afirmar("Crear() permite 'nombre' repetido (no hay UNIQUE en tipo_evento)",
    (await servicioTipoEvt.ObtenerAsync("TES2")).Nombre == "Prueba");

int filasPutTevt = await servicioTipoEvt.ReemplazarAsync("TEST", new TipoEventoReemplazo
{
    Nombre = "Prueba renombrada",
    Activo = false,
});
var trasPutTevt = await servicioTipoEvt.ObtenerAsync("TEST");
Afirmar("PUT reemplaza todo",
    filasPutTevt == 1 && trasPutTevt.Nombre == "Prueba renombrada" && !trasPutTevt.Activo);

int filasPatchTevt = await servicioTipoEvt.ActualizarAsync("TEST", new TipoEventoActualizar { Activo = true });
var trasPatchTevt = await servicioTipoEvt.ObtenerAsync("TEST");
Afirmar("PATCH cambia solo 'activo' y deja 'nombre' igual",
    filasPatchTevt == 1 && trasPatchTevt.Activo && trasPatchTevt.Nombre == "Prueba renombrada");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioTipoEvt.ActualizarAsync("TEST", new TipoEventoActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioTipoEvt.ActualizarAsync("NO_VA", new TipoEventoActualizar { Activo = false }));

int eliminadasTevt1 = await servicioTipoEvt.EliminarAsync("TEST");
int eliminadasTevt2 = await servicioTipoEvt.EliminarAsync("TES2");
Afirmar("DELETE elimina ambas filas", eliminadasTevt1 == 1 && eliminadasTevt2 == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion",
    () => servicioTipoEvt.EliminarAsync("TEST"));

// ============================================================================
//  Prueba de capas: ServicioTipoPregunta + repositorio falso (cierre de la v1)
// ============================================================================
var servicioTipoPreg = new ServicioTipoPregunta(new RepositorioTipoPreguntaFalso());

Console.WriteLine("\n== Prueba de capas: ServicioTipoPregunta + repositorio falso en memoria ==\n");

var (datosTpreg, totalTpreg) = await servicioTipoPreg.ListarAsync(null);
Afirmar("Listar() devuelve los 2 tipos de pregunta semilla", totalTpreg == 2 && datosTpreg.Count == 2);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioTipoPreg.ListarAsync(0));

// Regla de negocio: exactamente UNO de los tres destinos en true.
await AfirmarLanza<ArgumentException>(
    "Crear() con los tres destinos en false lanza ArgumentException",
    () => servicioTipoPreg.CrearAsync(new TipoPreguntaCrear { IdTipoPregunta = "MALA", Nombre = "Mala" }));
await AfirmarLanza<ArgumentException>(
    "Crear() con dos destinos en true lanza ArgumentException",
    () => servicioTipoPreg.CrearAsync(new TipoPreguntaCrear
    { IdTipoPregunta = "MALA", Nombre = "Mala", GuardaNumero = true, GuardaTexto = true }));

await servicioTipoPreg.CrearAsync(new TipoPreguntaCrear { IdTipoPregunta = "SI_NO", Nombre = "Sí o no", GuardaOpcion = true });
var tpregCreado = await servicioTipoPreg.ObtenerAsync("SI_NO");
Afirmar("Crear() inserta y Obtener() lo encuentra", tpregCreado.GuardaOpcion && !tpregCreado.GuardaNumero);

int filasPatchTpreg = await servicioTipoPreg.ActualizarAsync("SI_NO", new TipoPreguntaActualizar { Nombre = "Sí/No" });
Afirmar("PATCH cambia solo 'nombre'", filasPatchTpreg == 1 && (await servicioTipoPreg.ObtenerAsync("SI_NO")).Nombre == "Sí/No");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioTipoPreg.ActualizarAsync("SI_NO", new TipoPreguntaActualizar()));

Afirmar("DELETE elimina 1 fila", await servicioTipoPreg.EliminarAsync("SI_NO") == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioTipoPreg.EliminarAsync("SI_NO"));

// ============================================================================
//  Prueba de capas: ServicioTipoVinculacion + repositorio falso (cierre de la v1)
// ============================================================================
var servicioTipoVinc = new ServicioTipoVinculacion(new RepositorioTipoVinculacionFalso());

Console.WriteLine("\n== Prueba de capas: ServicioTipoVinculacion + repositorio falso en memoria ==\n");

var (datosTvin, totalTvin) = await servicioTipoVinc.ListarAsync(null);
Afirmar("Listar() devuelve los 2 tipos de vinculación semilla", totalTvin == 2 && datosTvin.Count == 2);

await servicioTipoVinc.CrearAsync(new TipoVinculacionCrear { IdTipoVinculacion = "EGRESADO", Nombre = "Egresado", EsInterno = true });
var tvinCreado = await servicioTipoVinc.ObtenerAsync("EGRESADO");
Afirmar("Crear() aplica los valores por defecto (exige_programa y activo = true)",
    tvinCreado.EsInterno && tvinCreado.ExigePrograma && tvinCreado.Activo);

int filasPutTvin = await servicioTipoVinc.ReemplazarAsync("EGRESADO", new TipoVinculacionReemplazo
{ Nombre = "Egresado USB", EsInterno = false, ExigePrograma = false, Activo = false });
var trasPutTvin = await servicioTipoVinc.ObtenerAsync("EGRESADO");
Afirmar("PUT reemplaza todo", filasPutTvin == 1 && !trasPutTvin.EsInterno && !trasPutTvin.ExigePrograma && !trasPutTvin.Activo);

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioTipoVinc.ActualizarAsync("NO_VA", new TipoVinculacionActualizar { Activo = true }));

Afirmar("DELETE elimina 1 fila", await servicioTipoVinc.EliminarAsync("EGRESADO") == 1);

// ============================================================================
//  v2 · Prueba de capas: ServicioFacultad + repositorio falso
//  Lo nuevo: la AUTORREFERENCIA. El servicio debe rechazar (400) una
//  facultad que se nombra a sí misma como padre, SIN llegar al repositorio.
// ============================================================================
var repoFacultadFalso = new RepositorioFacultadFalso();
var servicioFacultad = new ServicioFacultad(repoFacultadFalso);

Console.WriteLine("\n== v2 · Prueba de capas: ServicioFacultad + repositorio falso en memoria ==\n");

var (datosFac, totalFac) = await servicioFacultad.ListarAsync(null);
Afirmar("Listar() devuelve las 2 facultades semilla", totalFac == 2 && datosFac.Count == 2);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioFacultad.ListarAsync(0));

await AfirmarLanza<ArgumentException>(
    "POST con fk_facultad_padre == id_facultad lanza ArgumentException (400)",
    () => servicioFacultad.CrearAsync(new FacultadCrear { IdFacultad = "X", Nombre = "X", FkFacultadPadre = "X" }));
Afirmar("...y la autorreferencia NO llegó al repositorio", repoFacultadFalso.Inserciones == 0);

await servicioFacultad.CrearAsync(new FacultadCrear { IdFacultad = "F_ING", Nombre = "Ingenierias", FkFacultadPadre = "USB" });
var facCreada = await servicioFacultad.ObtenerAsync("F_ING");
Afirmar("Crear() inserta con padre y activo = true por defecto",
    facCreada.FkFacultadPadre == "USB" && facCreada.Activo);

await AfirmarLanza<ArgumentException>(
    "PUT con padre == id de la URL lanza ArgumentException",
    () => servicioFacultad.ReemplazarAsync("F_ING", new FacultadReemplazo { Nombre = "Ing", FkFacultadPadre = "F_ING", Activo = true }));
await AfirmarLanza<ArgumentException>(
    "PATCH con padre == id de la URL lanza ArgumentException",
    () => servicioFacultad.ActualizarAsync("F_ING", new FacultadActualizar { FkFacultadPadre = "F_ING" }));

int filasPutFac = await servicioFacultad.ReemplazarAsync("F_ING", new FacultadReemplazo { Nombre = "Ingenierías", Activo = false });
var trasPutFac = await servicioFacultad.ObtenerAsync("F_ING");
Afirmar("PUT sin padre deja fk_facultad_padre en null (reemplazo completo)",
    filasPutFac == 1 && trasPutFac.FkFacultadPadre is null && !trasPutFac.Activo);

int filasPatchFac = await servicioFacultad.ActualizarAsync("F_ING", new FacultadActualizar { FkFacultadPadre = "USB" });
var trasPatchFac = await servicioFacultad.ObtenerAsync("F_ING");
Afirmar("PATCH cambia solo el padre y deja 'nombre' igual",
    filasPatchFac == 1 && trasPatchFac.FkFacultadPadre == "USB" && trasPatchFac.Nombre == "Ingenierías");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioFacultad.ActualizarAsync("F_ING", new FacultadActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"NO_VA\") lanza NoEncontradoExcepcion", () => servicioFacultad.ObtenerAsync("NO_VA"));

Afirmar("DELETE elimina 1 fila", await servicioFacultad.EliminarAsync("F_ING") == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioFacultad.EliminarAsync("F_ING"));

// ============================================================================
//  v2 · Prueba de capas: ServicioDependencia + repositorio falso
// ============================================================================
var repoDependenciaFalso = new RepositorioDependenciaFalso();
var servicioDependencia = new ServicioDependencia(repoDependenciaFalso);

Console.WriteLine("\n== v2 · Prueba de capas: ServicioDependencia + repositorio falso en memoria ==\n");

var (datosDep, totalDep) = await servicioDependencia.ListarAsync(null);
Afirmar("Listar() devuelve las 2 dependencias raíz semilla", totalDep == 2 && datosDep.Count == 2);

await AfirmarLanza<ArgumentException>(
    "POST con fk_dependencia_padre == id_dependencia lanza ArgumentException (400)",
    () => servicioDependencia.CrearAsync(new DependenciaCrear { IdDependencia = "Y", Nombre = "Y", FkDependenciaPadre = "Y" }));
Afirmar("...y la autorreferencia NO llegó al repositorio", repoDependenciaFalso.Inserciones == 0);

await servicioDependencia.CrearAsync(new DependenciaCrear { IdDependencia = "PASTORAL", Nombre = "Pastoral", FkDependenciaPadre = "BIENESTAR" });
Afirmar("Crear() inserta la hija con su padre",
    (await servicioDependencia.ObtenerAsync("PASTORAL")).FkDependenciaPadre == "BIENESTAR");

await AfirmarLanza<ArgumentException>(
    "PATCH con padre == id de la URL lanza ArgumentException",
    () => servicioDependencia.ActualizarAsync("PASTORAL", new DependenciaActualizar { FkDependenciaPadre = "PASTORAL" }));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PUT a \"NO_VA\" lanza NoEncontradoExcepcion",
    () => servicioDependencia.ReemplazarAsync("NO_VA", new DependenciaReemplazo { Nombre = "N", Activo = true }));

Afirmar("DELETE elimina 1 fila", await servicioDependencia.EliminarAsync("PASTORAL") == 1);

// ============================================================================
//  v2 · Prueba de capas: ServicioProgramaAcademico + repositorio falso
//  (el formato del código y la lista de niveles son de FORMA: los prueba la
//   petición contra Docker, no esta prueba de capas)
// ============================================================================
var servicioPrograma = new ServicioProgramaAcademico(new RepositorioProgramaAcademicoFalso());

Console.WriteLine("\n== v2 · Prueba de capas: ServicioProgramaAcademico + repositorio falso en memoria ==\n");

var (datosProg, totalProg) = await servicioPrograma.ListarAsync(null);
Afirmar("Listar() devuelve el programa semilla", totalProg == 1 && datosProg.Count == 1);

await AfirmarLanza<ArgumentException>(
    "Listar(-1) lanza ArgumentException", () => servicioPrograma.ListarAsync(-1));

await servicioPrograma.CrearAsync(new ProgramaAcademicoCrear { Codigo = "Z9999", Nombre = "Programa de prueba" });
var progCreado = await servicioPrograma.ObtenerAsync("Z9999");
Afirmar("Crear() aplica los DEFAULT (nivel PREGRADO, activo true, sin facultad)",
    progCreado.Nivel == "PREGRADO" && progCreado.Activo && progCreado.FkFacultad is null);

int filasPutProg = await servicioPrograma.ReemplazarAsync("Z9999", new ProgramaAcademicoReemplazo
{ Nombre = "Maestría de prueba", FkFacultad = "F_ING", Nivel = "MAESTRIA", Activo = false });
var trasPutProg = await servicioPrograma.ObtenerAsync("Z9999");
Afirmar("PUT reemplaza todo", filasPutProg == 1 && trasPutProg.Nivel == "MAESTRIA"
    && trasPutProg.FkFacultad == "F_ING" && !trasPutProg.Activo);

int filasPatchProg = await servicioPrograma.ActualizarAsync("Z9999", new ProgramaAcademicoActualizar { Activo = true });
var trasPatchProg = await servicioPrograma.ObtenerAsync("Z9999");
Afirmar("PATCH cambia solo 'activo' y deja 'nivel' igual",
    filasPatchProg == 1 && trasPatchProg.Activo && trasPatchProg.Nivel == "MAESTRIA");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioPrograma.ActualizarAsync("Z9999", new ProgramaAcademicoActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(\"Q0000\") lanza NoEncontradoExcepcion", () => servicioPrograma.ObtenerAsync("Q0000"));

Afirmar("DELETE elimina 1 fila", await servicioPrograma.EliminarAsync("Z9999") == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioPrograma.EliminarAsync("Z9999"));

// ============================================================================
//  v2 · Prueba de capas: ServicioAliasPrograma + repositorio falso
//  El repositorio falso simula IDENTITY (contador) y DEFAULT now().
// ============================================================================
var servicioAlias = new ServicioAliasPrograma(new RepositorioAliasProgramaFalso());

Console.WriteLine("\n== v2 · Prueba de capas: ServicioAliasPrograma + repositorio falso en memoria ==\n");

var (datosAli, totalAli) = await servicioAlias.ListarAsync(null);
Afirmar("Listar() devuelve los 3 alias semilla de M0286",
    totalAli == 3 && datosAli.All(a => a.FkPrograma == "M0286"));

var aliasCreado = await servicioAlias.CrearAsync(new AliasProgramaCrear { TextoNormalizado = "ing de datos", FkPrograma = "M0286" });
Afirmar("Crear() devuelve el id y la fecha generados por la 'BD'",
    aliasCreado.IdAlias == 5 && aliasCreado.CreadoEn != default);
Afirmar("Crear() aplica origen = FORMULARIO por defecto", aliasCreado.Origen == "FORMULARIO");

var aliasLeido = await servicioAlias.ObtenerAsync(aliasCreado.IdAlias);
Afirmar("Obtener() encuentra el alias creado", aliasLeido.TextoNormalizado == "ing de datos");

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioAlias.ObtenerAsync(999));

Afirmar("DELETE elimina 1 fila", await servicioAlias.EliminarAsync(aliasCreado.IdAlias) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioAlias.EliminarAsync(aliasCreado.IdAlias));

// Comprobación por REFLEXIÓN: la interfaz de verdad no declara un método
// de actualizar (la ausencia es la especificación, ADR D12).
Afirmar("IRepositorioAliasPrograma no declara ActualizarAsync",
    typeof(IRepositorioAliasPrograma).GetMethod("ActualizarAsync") is null);

// ============================================================================
//  v2 · Prueba de capas: ServicioCatedra + repositorio falso
//  El repositorio falso simula el RETURNING (id_catedra, nombre_asis): así
//  se verifica NombreAsis sin PostgreSQL.
// ============================================================================
var servicioCatedra = new ServicioCatedra(new RepositorioCatedraFalso());

Console.WriteLine("\n== v2 · Prueba de capas: ServicioCatedra + repositorio falso en memoria ==\n");

var (datosCat, totalCat) = await servicioCatedra.ListarAsync(null);
Afirmar("Listar() sin límite devuelve TODAS las cátedras semilla", datosCat.Count == 3 && totalCat == 3);

var (datosCat1, totalCat1) = await servicioCatedra.ListarAsync(1);
Afirmar("Listar(1) devuelve 1 fila pero 'total' es el de la tabla (3)", datosCat1.Count == 1 && totalCat1 == 3);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioCatedra.ListarAsync(0));

var nombreLargo = "CATEDRA ABIERTA DE PRUEBA CON UN NOMBRE BASTANTE LARGO";
var (idCat, nombreAsisCat) = await servicioCatedra.CrearAsync(new CatedraCrear
{ IdEventoAsis = "000999001", Nombre = nombreLargo, FkTipoEvento = "CAAB" });
Afirmar("Crear() devuelve el id generado", idCat == 5505);
Afirmar("Crear() devuelve nombre_asis = primeros 30 caracteres",
    nombreAsisCat == nombreLargo[..30] && nombreAsisCat.Length == 30);

var catCreada = await servicioCatedra.ObtenerAsync(idCat);
Afirmar("Crear() aplica activo = true y deja la dependencia en null",
    catCreada.Activo && catCreada.FkDependencia is null);

int filasPutCat = await servicioCatedra.ReemplazarAsync(idCat, new CatedraReemplazo
{ IdEventoAsis = "000999001", Nombre = "Corta", FkTipoEvento = "DEPR", FkDependencia = "DEPORTES", Activo = false });
var trasPutCat = await servicioCatedra.ObtenerAsync(idCat);
Afirmar("PUT reemplaza todo (y la 'BD' recalcula nombre_asis)",
    filasPutCat == 1 && trasPutCat.FkTipoEvento == "DEPR" && trasPutCat.FkDependencia == "DEPORTES"
    && !trasPutCat.Activo && trasPutCat.NombreAsis == "Corta");

int filasPatchCat = await servicioCatedra.ActualizarAsync(idCat, new CatedraActualizar { Activo = true });
var trasPatchCat = await servicioCatedra.ObtenerAsync(idCat);
Afirmar("PATCH cambia solo 'activo'", filasPatchCat == 1 && trasPatchCat.Activo && trasPatchCat.Nombre == "Corta");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioCatedra.ActualizarAsync(idCat, new CatedraActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(99999) lanza NoEncontradoExcepcion", () => servicioCatedra.ObtenerAsync(99999));

Afirmar("DELETE elimina 1 fila", await servicioCatedra.EliminarAsync(idCat) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioCatedra.EliminarAsync(idCat));

// RNF7 por REFLEXIÓN: ninguna petición de Catedra declara 'NombreAsis'.
Afirmar("Ninguna petición de Catedra tiene la propiedad NombreAsis (columna generada)",
    new[] { typeof(CatedraCrear), typeof(CatedraReemplazo), typeof(CatedraActualizar) }
        .All(t => t.GetProperty("NombreAsis") is null));

// --------------------------------------------------------------------------
//  Resultado final -> código de salida del proceso.
// --------------------------------------------------------------------------
Console.WriteLine($"\n{pasadas} pasadas, {fallidas} fallidas.");
return fallidas == 0 ? 0 : 1;


// ==========================================================================
//  Repositorio FALSO: implementa el MISMO contrato IRepositorioSede, pero
//  con un diccionario en memoria. Ninguna línea de PostgreSQL.
//  (En C#, las declaraciones de tipos van después de las instrucciones
//   top-level.)
// ==========================================================================
class RepositorioSedeFalso : IRepositorioSede
{
    private readonly Dictionary<string, Sede> _tabla = new();

    public RepositorioSedeFalso()
    {
        // Dos sedes semilla, como en el script real.
        _tabla["SAN_BENITO"] = new Sede
        {
            IdSede = "SAN_BENITO",
            Nombre = "Campus San Benito",
            Direccion = "Carrera 56C 51-110, Medellin",
            EsVirtual = false,
            Activo = true,
        };
        _tabla["VIRTUAL"] = new Sede
        {
            IdSede = "VIRTUAL",
            Nombre = "Virtual",
            Direccion = null,
            EsVirtual = true,
            Activo = true,
        };
    }

    public Task<List<Sede>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(s => s.IdSede).Take(limite).ToList());

    public Task<Sede?> ObtenerPorIdAsync(string idSede) =>
        Task.FromResult(_tabla.TryGetValue(idSede, out var sede) ? sede : null);

    public Task CrearAsync(Sede sede)
    {
        // Simula la violación de la clave primaria (pk_sede).
        if (_tabla.ContainsKey(sede.IdSede))
            throw new InvalidOperationException($"id_sede '{sede.IdSede}' duplicado (simulado).");

        _tabla[sede.IdSede] = sede;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idSede, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idSede, out var sede))
            return Task.FromResult(0); // no existe

        // Aplica solo las columnas presentes en el diccionario.
        if (datos.TryGetValue("nombre", out var n)) sede.Nombre = (string)n!;
        if (datos.TryGetValue("direccion", out var d)) sede.Direccion = (string?)d;
        if (datos.TryGetValue("es_virtual", out var v)) sede.EsVirtual = (bool)v!;
        if (datos.TryGetValue("activo", out var a)) sede.Activo = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idSede) =>
        Task.FromResult(_tabla.Remove(idSede) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'Modalidad': mismo patrón que RepositorioSedeFalso.
// ==========================================================================
class RepositorioModalidadFalso : IRepositorioModalidad
{
    private readonly Dictionary<string, Modalidad> _tabla = new();

    public RepositorioModalidadFalso()
    {
        // Las 4 modalidades semilla del script real.
        _tabla["PRESENCIAL"] = new Modalidad { IdModalidad = "PRESENCIAL", Nombre = "Presencial", CanalDifusion = "QR", Activo = true };
        _tabla["VIRTUAL"] = new Modalidad { IdModalidad = "VIRTUAL", Nombre = "Virtual", CanalDifusion = "ENLACE", Activo = true };
        _tabla["TELEPRESENCIAL"] = new Modalidad { IdModalidad = "TELEPRESENCIAL", Nombre = "Telepresencial", CanalDifusion = "ENLACE", Activo = true };
        _tabla["HIBRIDA"] = new Modalidad { IdModalidad = "HIBRIDA", Nombre = "Hibrida", CanalDifusion = "AMBOS", Activo = true };
    }

    public Task<List<Modalidad>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(m => m.IdModalidad).Take(limite).ToList());

    public Task<Modalidad?> ObtenerPorIdAsync(string idModalidad) =>
        Task.FromResult(_tabla.TryGetValue(idModalidad, out var modalidad) ? modalidad : null);

    public Task CrearAsync(Modalidad modalidad)
    {
        if (_tabla.ContainsKey(modalidad.IdModalidad))
            throw new InvalidOperationException($"id_modalidad '{modalidad.IdModalidad}' duplicado (simulado).");

        _tabla[modalidad.IdModalidad] = modalidad;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idModalidad, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idModalidad, out var modalidad))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) modalidad.Nombre = (string)n!;
        if (datos.TryGetValue("canal_difusion", out var c)) modalidad.CanalDifusion = (string)c!;
        if (datos.TryGetValue("activo", out var a)) modalidad.Activo = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idModalidad) =>
        Task.FromResult(_tabla.Remove(idModalidad) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'EstadoProceso': mismo patrón que RepositorioSedeFalso.
//  NO valida la regla de coherencia (es_exitoso implica es_final): esa
//  regla es de NEGOCIO y vive en ServicioEstadoProceso, que es justo lo
//  que esta prueba de capas está verificando.
// ==========================================================================
class RepositorioEstadoProcesoFalso : IRepositorioEstadoProceso
{
    private readonly Dictionary<string, EstadoProceso> _tabla = new();

    public RepositorioEstadoProcesoFalso()
    {
        // Los 6 estados semilla del script real.
        _tabla["GENERADO"] = new EstadoProceso { IdEstado = "GENERADO", Nombre = "Generado, sin enviar", EsFinal = false, EsExitoso = false };
        _tabla["EN_COLA"] = new EstadoProceso { IdEstado = "EN_COLA", Nombre = "En cola", EsFinal = false, EsExitoso = false };
        _tabla["EN_CURSO"] = new EstadoProceso { IdEstado = "EN_CURSO", Nombre = "En curso", EsFinal = false, EsExitoso = false };
        _tabla["ERROR"] = new EstadoProceso { IdEstado = "ERROR", Nombre = "Error", EsFinal = true, EsExitoso = false };
        _tabla["CORRECTO"] = new EstadoProceso { IdEstado = "CORRECTO", Nombre = "Correcto", EsFinal = true, EsExitoso = true };
        _tabla["INCORRECTO"] = new EstadoProceso { IdEstado = "INCORRECTO", Nombre = "Incorrecto", EsFinal = true, EsExitoso = false };
    }

    public Task<List<EstadoProceso>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(e => e.IdEstado).Take(limite).ToList());

    public Task<EstadoProceso?> ObtenerPorIdAsync(string idEstado) =>
        Task.FromResult(_tabla.TryGetValue(idEstado, out var estado) ? estado : null);

    public Task CrearAsync(EstadoProceso estado)
    {
        if (_tabla.ContainsKey(estado.IdEstado))
            throw new InvalidOperationException($"id_estado '{estado.IdEstado}' duplicado (simulado).");

        _tabla[estado.IdEstado] = estado;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idEstado, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idEstado, out var estado))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) estado.Nombre = (string)n!;
        if (datos.TryGetValue("es_final", out var f)) estado.EsFinal = (bool)f!;
        if (datos.TryGetValue("es_exitoso", out var x)) estado.EsExitoso = (bool)x!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idEstado) =>
        Task.FromResult(_tabla.Remove(idEstado) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'Encuesta': mismo patrón, pero con clave 'long' y
//  un contador propio que simula la columna IDENTITY de la BD real
//  (3_plan.md §4.7) — 'CrearAsync' asigna el siguiente id y lo devuelve.
// ==========================================================================
class RepositorioEncuestaFalso : IRepositorioEncuesta
{
    private readonly Dictionary<long, Encuesta> _tabla = new();
    private long _siguienteId = 2; // el id 1 ya lo usa la fila semilla

    public RepositorioEncuestaFalso()
    {
        // La encuesta semilla del script real.
        _tabla[1] = new Encuesta
        {
            IdEncuesta = 1,
            Nombre = "Calidad de la catedra abierta",
            Version = 1,
            VigenteDesde = new DateOnly(2026, 1, 1),
            VigenteHasta = null,
            Activa = true,
        };
    }

    public Task<List<Encuesta>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(e => e.IdEncuesta).Take(limite).ToList());

    public Task<Encuesta?> ObtenerPorIdAsync(long idEncuesta) =>
        Task.FromResult(_tabla.TryGetValue(idEncuesta, out var encuesta) ? encuesta : null);

    public Task<long> CrearAsync(Encuesta encuesta)
    {
        // Simula la violación de uq_encuesta_ver (nombre, version).
        if (_tabla.Values.Any(e => e.Nombre == encuesta.Nombre && e.Version == encuesta.Version))
            throw new InvalidOperationException(
                $"(nombre, version) '({encuesta.Nombre}, {encuesta.Version})' duplicado (simulado).");

        encuesta.IdEncuesta = _siguienteId++;
        _tabla[encuesta.IdEncuesta] = encuesta;
        return Task.FromResult(encuesta.IdEncuesta);
    }

    public Task<int> ActualizarAsync(long idEncuesta, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idEncuesta, out var encuesta))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) encuesta.Nombre = (string)n!;
        if (datos.TryGetValue("version", out var v)) encuesta.Version = (short)v!;
        if (datos.TryGetValue("vigente_desde", out var vd)) encuesta.VigenteDesde = (DateOnly)vd!;
        if (datos.TryGetValue("vigente_hasta", out var vh)) encuesta.VigenteHasta = (DateOnly?)vh;
        if (datos.TryGetValue("activa", out var a)) encuesta.Activa = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idEncuesta) =>
        Task.FromResult(_tabla.Remove(idEncuesta) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'PeriodoAcademico': mismo patrón que
//  RepositorioSedeFalso. NO valida 'fecha_fin > fecha_ini': esa regla es
//  de NEGOCIO y vive en ServicioPeriodoAcademico, que es justo lo que
//  esta prueba de capas está verificando.
// ==========================================================================
class RepositorioPeriodoAcademicoFalso : IRepositorioPeriodoAcademico
{
    private readonly Dictionary<string, PeriodoAcademico> _tabla = new();

    public RepositorioPeriodoAcademicoFalso()
    {
        // Los 4 periodos semilla del script real.
        _tabla["2025-1"] = new PeriodoAcademico { IdPeriodo = "2025-1", FechaIni = new DateOnly(2025, 1, 20), FechaFin = new DateOnly(2025, 6, 7), Activo = true };
        _tabla["2025-2"] = new PeriodoAcademico { IdPeriodo = "2025-2", FechaIni = new DateOnly(2025, 7, 21), FechaFin = new DateOnly(2025, 12, 6), Activo = true };
        _tabla["2026-1"] = new PeriodoAcademico { IdPeriodo = "2026-1", FechaIni = new DateOnly(2026, 1, 19), FechaFin = new DateOnly(2026, 6, 6), Activo = true };
        _tabla["2026-2"] = new PeriodoAcademico { IdPeriodo = "2026-2", FechaIni = new DateOnly(2026, 7, 20), FechaFin = new DateOnly(2026, 12, 5), Activo = true };
    }

    public Task<List<PeriodoAcademico>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(p => p.IdPeriodo).Take(limite).ToList());

    public Task<PeriodoAcademico?> ObtenerPorIdAsync(string idPeriodo) =>
        Task.FromResult(_tabla.TryGetValue(idPeriodo, out var periodo) ? periodo : null);

    public Task CrearAsync(PeriodoAcademico periodo)
    {
        if (_tabla.ContainsKey(periodo.IdPeriodo))
            throw new InvalidOperationException($"id_periodo '{periodo.IdPeriodo}' duplicado (simulado).");

        _tabla[periodo.IdPeriodo] = periodo;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idPeriodo, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idPeriodo, out var periodo))
            return Task.FromResult(0);

        if (datos.TryGetValue("fecha_ini", out var fi)) periodo.FechaIni = (DateOnly)fi!;
        if (datos.TryGetValue("fecha_fin", out var ff)) periodo.FechaFin = (DateOnly)ff!;
        if (datos.TryGetValue("activo", out var a)) periodo.Activo = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idPeriodo) =>
        Task.FromResult(_tabla.Remove(idPeriodo) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'Rol': mismo patrón que RepositorioSedeFalso.
// ==========================================================================
class RepositorioRolFalso : IRepositorioRol
{
    private readonly Dictionary<string, Rol> _tabla = new();

    public RepositorioRolFalso()
    {
        // Los 3 roles semilla del script real.
        _tabla["ADMIN"] = new Rol { IdRol = "ADMIN", Nombre = "Administrador", Descripcion = "Gestiona catedras, enlaces, cargas y migraciones" };
        _tabla["CONSULTA"] = new Rol { IdRol = "CONSULTA", Nombre = "Consulta", Descripcion = "Solo lectura de informes" };
        _tabla["COORDINADOR"] = new Rol { IdRol = "COORDINADOR", Nombre = "Coordinador de dependencia", Descripcion = "Ve y gestiona lo de su dependencia" };
    }

    public Task<List<Rol>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(r => r.IdRol).Take(limite).ToList());

    public Task<Rol?> ObtenerPorIdAsync(string idRol) =>
        Task.FromResult(_tabla.TryGetValue(idRol, out var rol) ? rol : null);

    public Task CrearAsync(Rol rol)
    {
        if (_tabla.ContainsKey(rol.IdRol))
            throw new InvalidOperationException($"id_rol '{rol.IdRol}' duplicado (simulado).");

        _tabla[rol.IdRol] = rol;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idRol, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idRol, out var rol))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) rol.Nombre = (string)n!;
        if (datos.TryGetValue("descripcion", out var d)) rol.Descripcion = (string?)d;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idRol) =>
        Task.FromResult(_tabla.Remove(idRol) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'TipoDocumento': mismo patrón que RepositorioSedeFalso.
// ==========================================================================
class RepositorioTipoDocumentoFalso : IRepositorioTipoDocumento
{
    private readonly Dictionary<string, TipoDocumento> _tabla = new();

    public RepositorioTipoDocumentoFalso()
    {
        // Los 6 tipos de documento semilla del script real.
        _tabla["CC"] = new TipoDocumento { IdTipoDocumento = "CC", Nombre = "Cedula de ciudadania", Activo = true };
        _tabla["CE"] = new TipoDocumento { IdTipoDocumento = "CE", Nombre = "Cedula de extranjeria", Activo = true };
        _tabla["NIT"] = new TipoDocumento { IdTipoDocumento = "NIT", Nombre = "Numero de identificacion tributaria", Activo = true };
        _tabla["PA"] = new TipoDocumento { IdTipoDocumento = "PA", Nombre = "Pasaporte", Activo = true };
        _tabla["PEP"] = new TipoDocumento { IdTipoDocumento = "PEP", Nombre = "Permiso especial de permanencia", Activo = true };
        _tabla["TI"] = new TipoDocumento { IdTipoDocumento = "TI", Nombre = "Tarjeta de identidad", Activo = true };
    }

    public Task<List<TipoDocumento>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(t => t.IdTipoDocumento).Take(limite).ToList());

    public Task<TipoDocumento?> ObtenerPorIdAsync(string idTipoDocumento) =>
        Task.FromResult(_tabla.TryGetValue(idTipoDocumento, out var tipo) ? tipo : null);

    public Task CrearAsync(TipoDocumento tipoDocumento)
    {
        if (_tabla.ContainsKey(tipoDocumento.IdTipoDocumento))
            throw new InvalidOperationException($"id_tipo_documento '{tipoDocumento.IdTipoDocumento}' duplicado (simulado).");

        _tabla[tipoDocumento.IdTipoDocumento] = tipoDocumento;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idTipoDocumento, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idTipoDocumento, out var tipo))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) tipo.Nombre = (string)n!;
        if (datos.TryGetValue("activo", out var a)) tipo.Activo = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idTipoDocumento) =>
        Task.FromResult(_tabla.Remove(idTipoDocumento) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'TipoEvento': mismo patrón que RepositorioSedeFalso.
//  NO valida unicidad de 'nombre': esta tabla no tiene esa restricción.
// ==========================================================================
class RepositorioTipoEventoFalso : IRepositorioTipoEvento
{
    private readonly Dictionary<string, TipoEvento> _tabla = new();

    public RepositorioTipoEventoFalso()
    {
        // Los 8 tipos de evento semilla del script real.
        _tabla["ARCU"] = new TipoEvento { IdTipoEvento = "ARCU", Nombre = "Arte y cultura", Activo = true };
        _tabla["CAAB"] = new TipoEvento { IdTipoEvento = "CAAB", Nombre = "Catedra abierta", Activo = true };
        _tabla["DEPR"] = new TipoEvento { IdTipoEvento = "DEPR", Nombre = "Deporte y recreacion", Activo = true };
        _tabla["DIIB"] = new TipoEvento { IdTipoEvento = "DIIB", Nombre = "Difusion institucional", Activo = true };
        _tabla["DLLH"] = new TipoEvento { IdTipoEvento = "DLLH", Nombre = "Desarrollo humano", Activo = true };
        _tabla["EIIC"] = new TipoEvento { IdTipoEvento = "EIIC", Nombre = "Encuentro de investigacion", Activo = true };
        _tabla["MTG "] = new TipoEvento { IdTipoEvento = "MTG ", Nombre = "Mesa de trabajo o grupo", Activo = true };
        _tabla["SLDI"] = new TipoEvento { IdTipoEvento = "SLDI", Nombre = "Salud integral", Activo = true };
    }

    public Task<List<TipoEvento>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(t => t.IdTipoEvento).Take(limite).ToList());

    public Task<TipoEvento?> ObtenerPorIdAsync(string idTipoEvento) =>
        Task.FromResult(_tabla.TryGetValue(idTipoEvento, out var tipo) ? tipo : null);

    public Task CrearAsync(TipoEvento tipoEvento)
    {
        if (_tabla.ContainsKey(tipoEvento.IdTipoEvento))
            throw new InvalidOperationException($"id_tipo_evento '{tipoEvento.IdTipoEvento}' duplicado (simulado).");

        _tabla[tipoEvento.IdTipoEvento] = tipoEvento;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idTipoEvento, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idTipoEvento, out var tipo))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) tipo.Nombre = (string)n!;
        if (datos.TryGetValue("activo", out var a)) tipo.Activo = (bool)a!;

        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idTipoEvento) =>
        Task.FromResult(_tabla.Remove(idTipoEvento) ? 1 : 0);
}


// ==========================================================================
//  Repositorio FALSO de 'TipoPregunta' (cierre de la v1). NO valida la regla
//  "un solo destino": es de negocio y la prueba verifica justo eso.
// ==========================================================================
class RepositorioTipoPreguntaFalso : IRepositorioTipoPregunta
{
    private readonly Dictionary<string, TipoPregunta> _tabla = new();

    public RepositorioTipoPreguntaFalso()
    {
        _tabla["ESCALA"] = new TipoPregunta { IdTipoPregunta = "ESCALA", Nombre = "Escala", GuardaNumero = true };
        _tabla["ABIERTA"] = new TipoPregunta { IdTipoPregunta = "ABIERTA", Nombre = "Abierta", GuardaTexto = true };
    }

    public Task<List<TipoPregunta>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(t => t.IdTipoPregunta).Take(limite).ToList());

    public Task<TipoPregunta?> ObtenerPorIdAsync(string idTipoPregunta) =>
        Task.FromResult(_tabla.TryGetValue(idTipoPregunta, out var tipo) ? tipo : null);

    public Task CrearAsync(TipoPregunta tipoPregunta)
    {
        if (_tabla.ContainsKey(tipoPregunta.IdTipoPregunta))
            throw new InvalidOperationException("id_tipo_pregunta duplicado (simulado).");
        _tabla[tipoPregunta.IdTipoPregunta] = tipoPregunta;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idTipoPregunta, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idTipoPregunta, out var tipo))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) tipo.Nombre = (string)n!;
        if (datos.TryGetValue("guarda_numero", out var gn)) tipo.GuardaNumero = (bool)gn!;
        if (datos.TryGetValue("guarda_texto", out var gt)) tipo.GuardaTexto = (bool)gt!;
        if (datos.TryGetValue("guarda_opcion", out var go)) tipo.GuardaOpcion = (bool)go!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idTipoPregunta) =>
        Task.FromResult(_tabla.Remove(idTipoPregunta) ? 1 : 0);
}

// ==========================================================================
//  Repositorio FALSO de 'TipoVinculacion' (cierre de la v1).
// ==========================================================================
class RepositorioTipoVinculacionFalso : IRepositorioTipoVinculacion
{
    private readonly Dictionary<string, TipoVinculacion> _tabla = new();

    public RepositorioTipoVinculacionFalso()
    {
        _tabla["ESTUDIANTE"] = new TipoVinculacion { IdTipoVinculacion = "ESTUDIANTE", Nombre = "Estudiante", EsInterno = true };
        _tabla["EXTERNO"] = new TipoVinculacion { IdTipoVinculacion = "EXTERNO", Nombre = "Externo", EsInterno = false, ExigePrograma = false };
    }

    public Task<List<TipoVinculacion>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(t => t.IdTipoVinculacion).Take(limite).ToList());

    public Task<TipoVinculacion?> ObtenerPorIdAsync(string idTipoVinculacion) =>
        Task.FromResult(_tabla.TryGetValue(idTipoVinculacion, out var tipo) ? tipo : null);

    public Task CrearAsync(TipoVinculacion tipoVinculacion)
    {
        if (_tabla.ContainsKey(tipoVinculacion.IdTipoVinculacion))
            throw new InvalidOperationException("id_tipo_vinculacion duplicado (simulado).");
        _tabla[tipoVinculacion.IdTipoVinculacion] = tipoVinculacion;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idTipoVinculacion, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idTipoVinculacion, out var tipo))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) tipo.Nombre = (string)n!;
        if (datos.TryGetValue("es_interno", out var ei)) tipo.EsInterno = (bool)ei!;
        if (datos.TryGetValue("exige_programa", out var ep)) tipo.ExigePrograma = (bool)ep!;
        if (datos.TryGetValue("activo", out var a)) tipo.Activo = (bool)a!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idTipoVinculacion) =>
        Task.FromResult(_tabla.Remove(idTipoVinculacion) ? 1 : 0);
}

// ==========================================================================
//  v2 · Repositorio FALSO de 'Facultad'. Cuenta las inserciones para poder
//  comprobar que una autorreferencia se detuvo en el SERVICIO (nunca llegó
//  aquí). NO simula la FK del padre: eso es trabajo del motor real.
// ==========================================================================
class RepositorioFacultadFalso : IRepositorioFacultad
{
    private readonly Dictionary<string, Facultad> _tabla = new();

    /// <summary>Cuántas veces se llamó a CrearAsync.</summary>
    public int Inserciones { get; private set; }

    public RepositorioFacultadFalso()
    {
        _tabla["USB"] = new Facultad { IdFacultad = "USB", Nombre = "Universidad de San Buenaventura" };
        _tabla["F_OTRA"] = new Facultad { IdFacultad = "F_OTRA", Nombre = "Otra", FkFacultadPadre = "USB" };
    }

    public Task<List<Facultad>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(f => f.IdFacultad).Take(limite).ToList());

    public Task<Facultad?> ObtenerPorIdAsync(string idFacultad) =>
        Task.FromResult(_tabla.TryGetValue(idFacultad, out var facultad) ? facultad : null);

    public Task CrearAsync(Facultad facultad)
    {
        Inserciones++;
        if (_tabla.ContainsKey(facultad.IdFacultad))
            throw new InvalidOperationException("id_facultad duplicado (simulado).");
        _tabla[facultad.IdFacultad] = facultad;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idFacultad, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idFacultad, out var facultad))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) facultad.Nombre = (string)n!;
        if (datos.TryGetValue("fk_facultad_padre", out var p)) facultad.FkFacultadPadre = (string?)p;
        if (datos.TryGetValue("activo", out var a)) facultad.Activo = (bool)a!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idFacultad) =>
        Task.FromResult(_tabla.Remove(idFacultad) ? 1 : 0);
}

// ==========================================================================
//  v2 · Repositorio FALSO de 'Dependencia'. Mismo patrón que el de Facultad.
// ==========================================================================
class RepositorioDependenciaFalso : IRepositorioDependencia
{
    private readonly Dictionary<string, Dependencia> _tabla = new();

    public int Inserciones { get; private set; }

    public RepositorioDependenciaFalso()
    {
        _tabla["VICE_ACAD"] = new Dependencia { IdDependencia = "VICE_ACAD", Nombre = "Vicerrectoria Academica" };
        _tabla["BIENESTAR"] = new Dependencia { IdDependencia = "BIENESTAR", Nombre = "Bienestar Institucional" };
    }

    public Task<List<Dependencia>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(d => d.IdDependencia).Take(limite).ToList());

    public Task<Dependencia?> ObtenerPorIdAsync(string idDependencia) =>
        Task.FromResult(_tabla.TryGetValue(idDependencia, out var dependencia) ? dependencia : null);

    public Task CrearAsync(Dependencia dependencia)
    {
        Inserciones++;
        if (_tabla.ContainsKey(dependencia.IdDependencia))
            throw new InvalidOperationException("id_dependencia duplicado (simulado).");
        _tabla[dependencia.IdDependencia] = dependencia;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string idDependencia, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idDependencia, out var dependencia))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) dependencia.Nombre = (string)n!;
        if (datos.TryGetValue("fk_dependencia_padre", out var p)) dependencia.FkDependenciaPadre = (string?)p;
        if (datos.TryGetValue("activo", out var a)) dependencia.Activo = (bool)a!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string idDependencia) =>
        Task.FromResult(_tabla.Remove(idDependencia) ? 1 : 0);
}


// ==========================================================================
//  v2 · Repositorio FALSO de 'ProgramaAcademico'.
// ==========================================================================
class RepositorioProgramaAcademicoFalso : IRepositorioProgramaAcademico
{
    private readonly Dictionary<string, ProgramaAcademico> _tabla = new();

    public RepositorioProgramaAcademicoFalso()
    {
        _tabla["M0286"] = new ProgramaAcademico { Codigo = "M0286", Nombre = "Ingenieria de Datos y Software", FkFacultad = "F_ING" };
    }

    public Task<List<ProgramaAcademico>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(p => p.Codigo).Take(limite).ToList());

    public Task<ProgramaAcademico?> ObtenerPorIdAsync(string codigo) =>
        Task.FromResult(_tabla.TryGetValue(codigo, out var programa) ? programa : null);

    public Task CrearAsync(ProgramaAcademico programa)
    {
        if (_tabla.ContainsKey(programa.Codigo))
            throw new InvalidOperationException("codigo duplicado (simulado).");
        _tabla[programa.Codigo] = programa;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string codigo, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(codigo, out var programa))
            return Task.FromResult(0);

        if (datos.TryGetValue("nombre", out var n)) programa.Nombre = (string)n!;
        if (datos.TryGetValue("fk_facultad", out var f)) programa.FkFacultad = (string?)f;
        if (datos.TryGetValue("nivel", out var nv)) programa.Nivel = (string)nv!;
        if (datos.TryGetValue("activo", out var a)) programa.Activo = (bool)a!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string codigo) =>
        Task.FromResult(_tabla.Remove(codigo) ? 1 : 0);
}

// ==========================================================================
//  v2 · Repositorio FALSO de 'AliasPrograma'. Sin ActualizarAsync: si se
//  escribiera, el compilador no se quejaría, pero tampoco hay a quién
//  servirle (la interfaz no lo declara). Simula IDENTITY y DEFAULT now().
// ==========================================================================
class RepositorioAliasProgramaFalso : IRepositorioAliasPrograma
{
    private readonly Dictionary<long, AliasPrograma> _tabla = new();
    private long _siguienteId = 5; // en el script real los ids semilla son 1, 3 y 4

    public RepositorioAliasProgramaFalso()
    {
        _tabla[1] = new AliasPrograma { IdAlias = 1, TextoNormalizado = "ingenieria de datos y software", FkPrograma = "M0286" };
        _tabla[3] = new AliasPrograma { IdAlias = 3, TextoNormalizado = "ingenieria de datos", FkPrograma = "M0286" };
        _tabla[4] = new AliasPrograma { IdAlias = 4, TextoNormalizado = "ing. datos y software", FkPrograma = "M0286" };
    }

    public Task<List<AliasPrograma>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(a => a.IdAlias).Take(limite).ToList());

    public Task<AliasPrograma?> ObtenerPorIdAsync(long idAlias) =>
        Task.FromResult(_tabla.TryGetValue(idAlias, out var alias) ? alias : null);

    public Task<(long IdAlias, DateTime CreadoEn)> CrearAsync(AliasPrograma alias)
    {
        if (_tabla.Values.Any(a => a.TextoNormalizado == alias.TextoNormalizado))
            throw new InvalidOperationException("texto_normalizado duplicado (simulado).");

        long id = _siguienteId++;          // simula IDENTITY
        var ahora = DateTime.UtcNow;       // simula DEFAULT now()
        _tabla[id] = new AliasPrograma
        {
            IdAlias = id, TextoNormalizado = alias.TextoNormalizado,
            FkPrograma = alias.FkPrograma, Origen = alias.Origen, CreadoEn = ahora,
        };
        return Task.FromResult((id, ahora));
    }

    public Task<int> EliminarAsync(long idAlias) =>
        Task.FromResult(_tabla.Remove(idAlias) ? 1 : 0);
}


// ==========================================================================
//  v2 · Repositorio FALSO de 'Catedra'. Simula lo que hace la BD:
//   · IDENTITY  -> contador '_siguienteId';
//   · columna GENERADA nombre_asis -> left(nombre, 30), recalculada en
//     cada INSERT/UPDATE (aquí, en el método privado 'Generar').
// ==========================================================================
class RepositorioCatedraFalso : IRepositorioCatedra
{
    private readonly Dictionary<long, Catedra> _tabla = new();
    private long _siguienteId = 5505; // en el script real el último id es 5504

    public RepositorioCatedraFalso()
    {
        Guardar(new Catedra { IdCatedra = 1, IdEventoAsis = "000023636", Nombre = "PELICULA:LA MIRADA DEL AMOR", FkTipoEvento = "CAAB" });
        Guardar(new Catedra { IdCatedra = 5497, IdEventoAsis = "000900001", Nombre = "Taller de desarrollo humano", FkTipoEvento = "DLLH", FkDependencia = "DESA_HUM" });
        Guardar(new Catedra { IdCatedra = 5502, IdEventoAsis = "000913190", Nombre = "CATEDRA DE PRUEBA DE CERTIFICACION", FkTipoEvento = "CAAB" });
    }

    // Lo que hace PostgreSQL con 'GENERATED ALWAYS AS (left(nombre, 30))'.
    private static string Generar(string nombre) => nombre.Length > 30 ? nombre[..30] : nombre;

    private void Guardar(Catedra c)
    {
        c.NombreAsis = Generar(c.Nombre);
        _tabla[c.IdCatedra] = c;
    }

    public Task<List<Catedra>> ObtenerTodosAsync(int? limite) =>
        Task.FromResult(_tabla.Values.OrderBy(c => c.IdCatedra).Take(limite ?? int.MaxValue).ToList());

    public Task<int> ContarAsync() => Task.FromResult(_tabla.Count);

    public Task<Catedra?> ObtenerPorIdAsync(long idCatedra) =>
        Task.FromResult(_tabla.TryGetValue(idCatedra, out var catedra) ? catedra : null);

    public Task<(long IdCatedra, string NombreAsis)> CrearAsync(Catedra catedra)
    {
        if (_tabla.Values.Any(c => c.IdEventoAsis == catedra.IdEventoAsis))
            throw new InvalidOperationException("id_evento_asis duplicado (simulado).");

        catedra.IdCatedra = _siguienteId++;
        catedra.CreadoEn = DateTime.UtcNow;
        Guardar(catedra);
        return Task.FromResult((catedra.IdCatedra, catedra.NombreAsis));
    }

    public Task<int> ActualizarAsync(long idCatedra, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idCatedra, out var c))
            return Task.FromResult(0);

        if (datos.TryGetValue("id_evento_asis", out var e)) c.IdEventoAsis = (string)e!;
        if (datos.TryGetValue("nombre", out var n)) c.Nombre = (string)n!;
        if (datos.TryGetValue("fk_tipo_evento", out var t)) c.FkTipoEvento = (string)t!;
        if (datos.TryGetValue("fk_dependencia", out var d)) c.FkDependencia = (string?)d;
        if (datos.TryGetValue("activo", out var a)) c.Activo = (bool)a!;
        Guardar(c); // recalcula nombre_asis, como la BD
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idCatedra) =>
        Task.FromResult(_tabla.Remove(idCatedra) ? 1 : 0);
}
