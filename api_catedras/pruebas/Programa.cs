// ==========================================================================
//  pruebas/Programa.cs  ·  Proyecto Cátedras · Versiones 1, 2, 3, 4 y 5
//  (criterio 6 de la v1 · criterio 8 de la v2 · criterio 8 de la v3 ·
//   criterio 7 de la v4 · criterio 8 de la v5)
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
var repoProgramaFalso = new RepositorioProgramaAcademicoFalso();
var servicioPrograma = new ServicioProgramaAcademico(repoProgramaFalso);

Console.WriteLine("\n== v2 · Prueba de capas: ServicioProgramaAcademico + repositorio falso en memoria ==\n");

var (datosProg, totalProg) = await servicioPrograma.ListarAsync(null);
Afirmar("Listar() devuelve el programa semilla", totalProg == 1 && datosProg.Count == 1);

await AfirmarLanza<ArgumentException>(
    "Listar(-1) lanza ArgumentException", () => servicioPrograma.ListarAsync(-1));

await servicioPrograma.CrearAsync(new ProgramaAcademicoCrear { Codigo = "Z9999", Nombre = "Programa de prueba" });
var progCreado = await servicioPrograma.ObtenerAsync("Z9999");
Afirmar("Crear() aplica los DEFAULT (nivel PREGRADO, activo true, sin facultad)",
    progCreado.Nivel == "PREGRADO" && progCreado.Activo && progCreado.FkFacultad is null);
Afirmar("Crear() sin 'alias' entrega 0 alias al repositorio (retrofit V2)",
    repoProgramaFalso.UltimosAlias.Count == 0);

// Retrofit V2 (Guía IA, Regla 1): maestro + detalles en la misma llamada.
await servicioPrograma.CrearAsync(new ProgramaAcademicoCrear
{
    Codigo = "Z9998",
    Nombre = "Programa con alias",
    Alias =
    [
        new AliasInicial { TextoNormalizado = "prog con alias" },
        new AliasInicial { TextoNormalizado = "pca", Origen = "CARGA" },
    ],
});
var aliasEntregados = repoProgramaFalso.UltimosAlias;
Afirmar("Crear() con 2 alias los entrega JUNTO con el programa, en una sola llamada",
    aliasEntregados.Count == 2);
Afirmar("cada alias inicial apunta al programa que se crea (fk_programa = 'Z9998')",
    aliasEntregados.All(a => a.FkPrograma == "Z9998"));
Afirmar("'origen' toma el DEFAULT 'FORMULARIO' si no llega, y respeta el enviado",
    aliasEntregados[0].Origen == "FORMULARIO" && aliasEntregados[1].Origen == "CARGA");
Afirmar("la petición de alias inicial NO tiene 'fk_programa' (lo pone el servicio)",
    typeof(AliasInicial).GetProperty("FkPrograma") is null);
await servicioPrograma.EliminarAsync("Z9998");

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

// RETROFIT criterio 3: sp_actualizar_alias_programa ahora SÍ tiene llamador
// (PUT/PATCH /api/AliasPrograma/{id}). Esto revierte el ADR D12 ("tabla
// solo-anexo"); ver AJUSTES_PATRON_SEDE_LOTE.md §2 · AliasPrograma.
Afirmar("IRepositorioAliasPrograma declara ActualizarAsync (retrofit: revierte ADR D12)",
    typeof(IRepositorioAliasPrograma).GetMethod("ActualizarAsync") is not null);

// PUT: reemplaza todo; 'origen' ausente vuelve a FORMULARIO (la columna no admite NULL).
Afirmar("PUT (Reemplazar) del alias semilla 3 cambia 1 fila",
    await servicioAlias.ReemplazarAsync(3, new AliasProgramaReemplazo { TextoNormalizado = "ing datos (put)", FkPrograma = "M0286" }) == 1);
var aliasPut = await servicioAlias.ObtenerAsync(3);
Afirmar("...y deja el texto nuevo con origen = FORMULARIO por defecto",
    aliasPut.TextoNormalizado == "ing datos (put)" && aliasPut.Origen == "FORMULARIO");
await AfirmarLanza<NoEncontradoExcepcion>(
    "PUT a un alias inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioAlias.ReemplazarAsync(999, new AliasProgramaReemplazo { TextoNormalizado = "x", FkPrograma = "M0286" }));

// PATCH: solo cambia lo enviado.
await servicioAlias.ActualizarAsync(3, new AliasProgramaActualizar { Origen = "CARGA_MASIVA" });
var aliasPatch = await servicioAlias.ObtenerAsync(3);
Afirmar("PATCH cambia solo 'origen' y conserva el texto del PUT",
    aliasPatch.Origen == "CARGA_MASIVA" && aliasPatch.TextoNormalizado == "ing datos (put)");
await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException (400)",
    () => servicioAlias.ActualizarAsync(3, new AliasProgramaActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a un alias inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioAlias.ActualizarAsync(999, new AliasProgramaActualizar { Origen = "FORMULARIO" }));

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

// ============================================================================
//  v3 · Prueba de capas: ServicioAsistente + repositorio falso
//  El repositorio falso simula el CALL a 'sp_guardar_asistente': devuelve un
//  id fijo y GUARDA las 4 listas de detalle que le entregó el servicio, para
//  comprobar sin PostgreSQL cómo las armó.
// ============================================================================
var repoAsistente = new RepositorioAsistenteFalso();
var servicioAsistente = new ServicioAsistente(repoAsistente);

Console.WriteLine("\n== v3 · Prueba de capas: ServicioAsistente + repositorio falso en memoria ==\n");

var (datosAsi, totalAsi) = await servicioAsistente.ListarAsync(null, null, null);
Afirmar("Listar() devuelve los 3 asistentes semilla", totalAsi == 3 && datosAsi.Count == 3);

var (datosAsiQ, _) = await servicioAsistente.ListarAsync("garcia", null, null);
Afirmar("Listar(q='garcia') filtra sin distinguir mayúsculas", datosAsiQ.Count == 1 && datosAsiQ[0].Apellidos == "Garcia");

var (datosAsiP2, _) = await servicioAsistente.ListarAsync(null, 2, 2);
Afirmar("Listar(limite 2, página 2) salta las 2 primeras filas", datosAsiP2.Count == 1 && datosAsiP2[0].IdAsistente == 3);

await AfirmarLanza<ArgumentException>(
    "Listar(pagina 0) lanza ArgumentException", () => servicioAsistente.ListarAsync(null, null, 0));

// Reglas que cruzan campos: el servicio las corta ANTES del repositorio.
int llamadasAntes = repoAsistente.LlamadasCrear;
await AfirmarLanza<ArgumentException>(
    "Crear() no externo sin id_asis lanza ArgumentException (400)",
    () => servicioAsistente.CrearAsync(new AsistenteCrear { Nombres = "A", Apellidos = "B", CorreoPersonal = "a@b.co" }));
await AfirmarLanza<ArgumentException>(
    "Crear() sin ningún correo lanza ArgumentException (400)",
    () => servicioAsistente.CrearAsync(new AsistenteCrear { Nombres = "A", Apellidos = "B", IdAsis = "ABC12345" }));
Afirmar("...y en ninguno de los dos casos se llamó al repositorio", repoAsistente.LlamadasCrear == llamadasAntes);

var externo = await servicioAsistente.CrearAsync(new AsistenteCrear
{ Nombres = "Ext", Apellidos = "Erno", EsExterno = true, CorreoPersonal = "ext@x.co" });
Afirmar("Crear() externo sin id_asis es válido", externo.IdAsistente > 0 && externo.IdAsis is null && externo.EsExterno);

var conDetalles = await servicioAsistente.CrearAsync(new AsistenteCrear
{
    Nombres = "Prueba", Apellidos = "Uno", IdAsis = "Z9999999", CorreoInstitucional = "p.uno@usbmed.edu.co",
    Documentos = [new DetalleDocumentoInicial { FkTipoDocumento = "CC", Numero = "999999999" }],
    Vinculaciones = [new DetalleVinculacionInicial { FkTipoVinculacion = "ESTUDIANTE" }],
    Programas = [new DetalleProgramaInicial { FkPrograma = "M0286", FkPeriodo = "2026-1" }],
    Consentimiento = new DetalleConsentimientoInicial { VersionPolitica = "v1.0" },
});
long idNuevo = conDetalles.IdAsistente;
Afirmar("Crear() con detalles devuelve el id que generó la 'BD'", idNuevo == repoAsistente.UltimoId);
Afirmar("Crear() aplica es_externo = false y activo = true", !conDetalles.EsExterno && conDetalles.Activo);
Afirmar("Los 4 detalles llegaron al repositorio en la MISMA llamada",
    repoAsistente.UltimosDocumentos.Count == 1 && repoAsistente.UltimasVinculaciones.Count == 1
    && repoAsistente.UltimosProgramas.Count == 1 && repoAsistente.UltimoConsentimiento is not null);
Afirmar("Tras el CALL, cada detalle apunta al asistente nuevo (fk_asistente)",
    repoAsistente.UltimosDocumentos.All(d => d.FkAsistente == idNuevo)
    && repoAsistente.UltimasVinculaciones.All(v => v.FkAsistente == idNuevo)
    && repoAsistente.UltimosProgramas.All(p => p.FkAsistente == idNuevo)
    && repoAsistente.UltimoConsentimiento!.FkAsistente == idNuevo);
Afirmar("Detalles con sus DEFAULT: vigente=true, fecha_ini=hoy, estado=ACTIVO, canal=WEB",
    repoAsistente.UltimosDocumentos[0].Vigente
    && repoAsistente.UltimasVinculaciones[0].FechaIni == DateOnly.FromDateTime(DateTime.Today)
    && repoAsistente.UltimosProgramas[0].Estado == "ACTIVO"
    && repoAsistente.UltimoConsentimiento!.Canal == "WEB");

var sinDetalles = await servicioAsistente.CrearAsync(new AsistenteCrear
{ Nombres = "Solo", Apellidos = "Maestro", IdAsis = "SOLO0001", CorreoPersonal = "s@x.co" });
Afirmar("Crear() sin bloques de detalle entrega 4 listas vacías / null",
    repoAsistente.UltimosDocumentos.Count == 0 && repoAsistente.UltimasVinculaciones.Count == 0
    && repoAsistente.UltimosProgramas.Count == 0 && repoAsistente.UltimoConsentimiento is null);

await AfirmarLanza<ArgumentException>(
    "PUT no externo sin id_asis lanza ArgumentException",
    () => servicioAsistente.ReemplazarAsync(idNuevo, new AsistenteReemplazo
    { Nombres = "P", Apellidos = "U", EsExterno = false, Activo = true, CorreoPersonal = "x@y.co" }));

int filasPutAsi = await servicioAsistente.ReemplazarAsync(idNuevo, new AsistenteReemplazo
{ IdAsis = "Z9999999", Nombres = "Prueba", Apellidos = "Editada", CorreoPersonal = "x@y.co", EsExterno = false, Activo = false });
var trasPutAsi = await servicioAsistente.ObtenerAsync(idNuevo);
Afirmar("PUT reemplaza todo (el correo institucional que no llegó queda null)",
    filasPutAsi == 1 && trasPutAsi.Apellidos == "Editada" && trasPutAsi.CorreoInstitucional is null && !trasPutAsi.Activo);

int filasPatchAsi = await servicioAsistente.ActualizarAsync(idNuevo, new AsistenteActualizar { Celular = "3001234567" });
var trasPatchAsi = await servicioAsistente.ObtenerAsync(idNuevo);
Afirmar("PATCH cambia solo 'celular'", filasPatchAsi == 1 && trasPatchAsi.Celular == "3001234567" && trasPatchAsi.Apellidos == "Editada");

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException", () => servicioAsistente.ActualizarAsync(idNuevo, new AsistenteActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(99999) lanza NoEncontradoExcepcion", () => servicioAsistente.ObtenerAsync(99999));
Afirmar("DELETE elimina 1 fila", await servicioAsistente.EliminarAsync(sinDetalles.IdAsistente) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioAsistente.EliminarAsync(sinDetalles.IdAsistente));

Afirmar("Ninguna petición de detalle inicial declara 'FkAsistente' (lo fija el SP)",
    new[] { typeof(DetalleDocumentoInicial), typeof(DetalleVinculacionInicial),
            typeof(DetalleProgramaInicial), typeof(DetalleConsentimientoInicial) }
        .All(t => t.GetProperty("FkAsistente") is null));

// ============================================================================
//  v3 · Prueba de capas: ServicioDocumentoAsistente + repositorio falso
// ============================================================================
var servicioDocumento = new ServicioDocumentoAsistente(new RepositorioDocumentoAsistenteFalso());

Console.WriteLine("\n== v3 · Prueba de capas: ServicioDocumentoAsistente + repositorio falso en memoria ==\n");

var (datosDoc, totalDoc) = await servicioDocumento.ListarAsync(null, null);
Afirmar("Listar() devuelve los 2 documentos semilla", totalDoc == 2);
var (datosDocA1, _) = await servicioDocumento.ListarAsync(1, null);
Afirmar("Listar(fkAsistente=1) solo trae los del asistente 1", datosDocA1.Count == 1 && datosDocA1[0].FkAsistente == 1);

var docCreado = await servicioDocumento.CrearAsync(new DocumentoAsistenteCrear
{ FkAsistente = 2, FkTipoDocumento = "TI", Numero = "1234567" });
Afirmar("Crear() devuelve id y fecha generados, vigente = true por defecto",
    docCreado.IdDocumento == 3 && docCreado.RegistradoEn != default && docCreado.Vigente);

int filasPatchDoc = await servicioDocumento.ActualizarAsync(docCreado.IdDocumento, new DocumentoAsistenteActualizar { Vigente = false });
Afirmar("PATCH {vigente:false} cambia solo 'vigente'",
    filasPatchDoc == 1 && !(await servicioDocumento.ObtenerAsync(docCreado.IdDocumento)).Vigente);

int filasPutDoc = await servicioDocumento.ReemplazarAsync(docCreado.IdDocumento, new DocumentoAsistenteReemplazo
{ FkTipoDocumento = "CC", Numero = "7654321", Vigente = true });
var trasPutDoc = await servicioDocumento.ObtenerAsync(docCreado.IdDocumento);
Afirmar("PUT reemplaza tipo, número y vigente (el dueño no cambia)",
    filasPutDoc == 1 && trasPutDoc.FkTipoDocumento == "CC" && trasPutDoc.Numero == "7654321" && trasPutDoc.FkAsistente == 2);

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioDocumento.ActualizarAsync(docCreado.IdDocumento, new DocumentoAsistenteActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "PUT sobre id inexistente lanza NoEncontradoExcepcion",
    () => servicioDocumento.ReemplazarAsync(999, new DocumentoAsistenteReemplazo { FkTipoDocumento = "CC", Numero = "1111", Vigente = true }));
Afirmar("DELETE elimina 1 fila", await servicioDocumento.EliminarAsync(docCreado.IdDocumento) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioDocumento.EliminarAsync(docCreado.IdDocumento));

// ============================================================================
//  v3 · Prueba de capas: ServicioVinculacionAsistente + repositorio falso
//  Tabla de LLAVE COMPUESTA (ADR D16): sin id, se abre y se cierra.
// ============================================================================
var repoVinculacion = new RepositorioVinculacionAsistenteFalso();
var servicioVinculacion = new ServicioVinculacionAsistente(repoVinculacion);

Console.WriteLine("\n== v3 · Prueba de capas: ServicioVinculacionAsistente + repositorio falso en memoria ==\n");

var vinc = await servicioVinculacion.CrearAsync(new VinculacionAsistenteCrear { FkAsistente = 1, FkTipoVinculacion = "DOCENTE" });
Afirmar("Crear() abre con fecha_ini = hoy y sin fecha_fin",
    vinc.FechaIni == DateOnly.FromDateTime(DateTime.Today) && vinc.FechaFin is null);

var (datosVinA1, _) = await servicioVinculacion.ListarAsync(1, null);
Afirmar("Listar(fkAsistente=1) trae la semilla y la nueva", datosVinA1.Count == 2);

var cierre = new VinculacionAsistenteCerrar
{ FkAsistente = 1, FkTipoVinculacion = "DOCENTE", FechaIni = vinc.FechaIni, FechaFin = vinc.FechaIni.AddDays(30) };

await AfirmarLanza<ArgumentException>(
    "Cerrar con fecha_fin anterior a fecha_ini lanza ArgumentException",
    () => servicioVinculacion.CerrarAsync(new VinculacionAsistenteCerrar
    { FkAsistente = 1, FkTipoVinculacion = "DOCENTE", FechaIni = vinc.FechaIni, FechaFin = vinc.FechaIni.AddDays(-1) }));

var cerrada = await servicioVinculacion.CerrarAsync(cierre);
Afirmar("Cerrar() pone fecha_fin", cerrada.FechaFin == vinc.FechaIni.AddDays(30));

int cierresAntes = repoVinculacion.LlamadasCerrar;
await AfirmarLanza<ArgumentException>(
    "Cerrar dos veces lanza ArgumentException (400)", () => servicioVinculacion.CerrarAsync(cierre));
Afirmar("...y la segunda vez NO se llamó al UPDATE del repositorio", repoVinculacion.LlamadasCerrar == cierresAntes);

await AfirmarLanza<NoEncontradoExcepcion>(
    "Cerrar una llave inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioVinculacion.CerrarAsync(new VinculacionAsistenteCerrar
    { FkAsistente = 1, FkTipoVinculacion = "EGRESADO", FechaIni = new DateOnly(2020, 1, 1) }));

Afirmar("IRepositorioVinculacionAsistente no declara ObtenerPorId/Actualizar/Eliminar (ADR D16)",
    new[] { "ObtenerPorIdAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioVinculacionAsistente).GetMethod(m) is null));

// ============================================================================
//  v3 · Prueba de capas: ServicioProgramaAsistente + repositorio falso
// ============================================================================
var repoProgAsi = new RepositorioProgramaAsistenteFalso();
var servicioProgAsi = new ServicioProgramaAsistente(repoProgAsi);

Console.WriteLine("\n== v3 · Prueba de capas: ServicioProgramaAsistente + repositorio falso en memoria ==\n");

var matricula = await servicioProgAsi.CrearAsync(new ProgramaAsistenteCrear { FkAsistente = 2, FkPrograma = "M0286", FkPeriodo = "2026-2" });
Afirmar("Crear() aplica estado = ACTIVO por defecto", matricula.Estado == "ACTIVO");

var cambio = new ProgramaAsistenteCambiarEstado { FkAsistente = 2, FkPrograma = "M0286", FkPeriodo = "2026-2", Estado = "GRADUADO" };
var cambiada = await servicioProgAsi.CambiarEstadoAsync(cambio);
Afirmar("CambiarEstado() deja la matrícula en GRADUADO", cambiada.Estado == "GRADUADO");

int cambiosAntes = repoProgAsi.LlamadasCambiar;
await AfirmarLanza<ArgumentException>(
    "CambiarEstado al MISMO estado lanza ArgumentException (400)", () => servicioProgAsi.CambiarEstadoAsync(cambio));
Afirmar("...sin llamar al UPDATE del repositorio", repoProgAsi.LlamadasCambiar == cambiosAntes);

await AfirmarLanza<NoEncontradoExcepcion>(
    "CambiarEstado de una llave inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioProgAsi.CambiarEstadoAsync(new ProgramaAsistenteCambiarEstado
    { FkAsistente = 2, FkPrograma = "XXXX", FkPeriodo = "2026-2", Estado = "RETIRADO" }));

Afirmar("IRepositorioProgramaAsistente no declara ObtenerPorId/Actualizar/Eliminar (ADR D16)",
    new[] { "ObtenerPorIdAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioProgramaAsistente).GetMethod(m) is null));

// ============================================================================
//  v3 · Prueba de capas: ServicioConsentimientoDatos + repositorio falso
// ============================================================================
var servicioConsentimiento = new ServicioConsentimientoDatos(new RepositorioConsentimientoDatosFalso());

Console.WriteLine("\n== v3 · Prueba de capas: ServicioConsentimientoDatos + repositorio falso en memoria ==\n");

var consentimiento = await servicioConsentimiento.CrearAsync(new ConsentimientoDatosCrear { FkAsistente = 1, VersionPolitica = "v2.0" });
Afirmar("Crear() devuelve id y aceptado_en de la 'BD', canal = WEB por defecto",
    consentimiento.IdConsentimiento == 2 && consentimiento.AceptadoEn != default && consentimiento.Canal == "WEB");
Afirmar("Obtener() encuentra el consentimiento creado",
    (await servicioConsentimiento.ObtenerAsync(consentimiento.IdConsentimiento)).VersionPolitica == "v2.0");
var (datosConA1, _) = await servicioConsentimiento.ListarAsync(1, null);
Afirmar("Listar(fkAsistente=1) trae la semilla y el nuevo", datosConA1.Count == 2);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioConsentimiento.ObtenerAsync(999));
Afirmar("IRepositorioConsentimientoDatos no declara Actualizar ni Eliminar (RF113)",
    typeof(IRepositorioConsentimientoDatos).GetMethod("ActualizarAsync") is null
    && typeof(IRepositorioConsentimientoDatos).GetMethod("EliminarAsync") is null);

// ============================================================================
//  v3 · Prueba de capas: ServicioLoteCargaAsistente + ServicioNovedadCarga
//  El repositorio falso de lotes cuenta las llamadas: así se demuestra que
//  los 400 por contadores lo lanza el SERVICIO sin tocar el repositorio.
//  El falso de novedades COMPARTE la lista donde el de lotes guarda las
//  novedades al cerrar (simula que el SP las insertó en la misma BD).
// ============================================================================
var tablaNovedades = new List<NovedadCarga>();
var repoLote = new RepositorioLoteCargaAsistenteFalso(tablaNovedades);
var servicioLote = new ServicioLoteCargaAsistente(repoLote);
var servicioNovedad = new ServicioNovedadCarga(new RepositorioNovedadCargaFalso(tablaNovedades));

Console.WriteLine("\n== v3 · Prueba de capas: ServicioLoteCargaAsistente + ServicioNovedadCarga ==\n");

var lote = await servicioLote.CrearAsync(new LoteCargaAsistenteCrear { NombreArchivo = "prueba.xlsx" });
Afirmar("Crear() abre el lote con contadores en 0 y sin usuario",
    lote.IdLoteCarga == 2 && lote.FilasLeidas == 0 && lote.FkUsuario is null && !lote.EstaCerrado);

int llamadasLoteAntes = repoLote.LlamadasTotales;
await AfirmarLanza<ArgumentException>(
    "Cerrar con aceptadas + rechazadas > leidas lanza ArgumentException (400)",
    () => servicioLote.CerrarAsync(lote.IdLoteCarga, new LoteCargaAsistenteCerrar
    { FilasLeidas = 2, FilasAceptadas = 2, FilasRechazadas = 1, Novedades = [new NovedadInicial { NumeroFila = 3, ContenidoCrudo = "x", Motivo = "m" }] }));
await AfirmarLanza<ArgumentException>(
    "Cerrar sin una novedad por fila rechazada lanza ArgumentException (400)",
    () => servicioLote.CerrarAsync(lote.IdLoteCarga, new LoteCargaAsistenteCerrar
    { FilasLeidas = 3, FilasAceptadas = 2, FilasRechazadas = 1 }));
Afirmar("...y en esos dos casos el repositorio NUNCA se invocó", repoLote.LlamadasTotales == llamadasLoteAntes);

await AfirmarLanza<NoEncontradoExcepcion>(
    "Cerrar un lote inexistente lanza NoEncontradoExcepcion",
    () => servicioLote.CerrarAsync(999, new LoteCargaAsistenteCerrar { FilasLeidas = 0, FilasAceptadas = 0, FilasRechazadas = 0 }));

var cerradoLote = await servicioLote.CerrarAsync(lote.IdLoteCarga, new LoteCargaAsistenteCerrar
{
    FilasLeidas = 3, FilasAceptadas = 2, FilasRechazadas = 1,
    Novedades = [new NovedadInicial { NumeroFila = 2, ContenidoCrudo = "Ana;;", Motivo = "correo duplicado" }],
});
Afirmar("Cerrar() guarda los 3 contadores y queda 'cerrado'",
    cerradoLote.FilasLeidas == 3 && cerradoLote.FilasAceptadas == 2 && cerradoLote.FilasRechazadas == 1 && cerradoLote.EstaCerrado);

var (novedadesLote, totalNov) = await servicioNovedad.ListarAsync(lote.IdLoteCarga, null);
Afirmar("La novedad quedó con fk_lote_carga = el lote cerrado y su motivo",
    totalNov == 1 && novedadesLote[0].FkLoteCarga == lote.IdLoteCarga && novedadesLote[0].Motivo == "correo duplicado");

await AfirmarLanza<ArgumentException>(
    "Cerrar el mismo lote dos veces lanza ArgumentException (400)",
    () => servicioLote.CerrarAsync(lote.IdLoteCarga, new LoteCargaAsistenteCerrar { FilasLeidas = 0, FilasAceptadas = 0, FilasRechazadas = 0 }));

await AfirmarLanza<NoEncontradoExcepcion>(
    "NovedadCarga: Obtener(999) lanza NoEncontradoExcepcion", () => servicioNovedad.ObtenerAsync(999));

Afirmar("IRepositorioNovedadCarga no declara CrearAsync (nace solo dentro del SP)",
    typeof(IRepositorioNovedadCarga).GetMethod("CrearAsync") is null);
Afirmar("IRepositorioLoteCargaAsistente no declara Actualizar ni Eliminar (RF117)",
    typeof(IRepositorioLoteCargaAsistente).GetMethod("ActualizarAsync") is null
    && typeof(IRepositorioLoteCargaAsistente).GetMethod("EliminarAsync") is null);
Afirmar("LoteCargaAsistenteCrear no expone los contadores ni fk_usuario",
    new[] { "FilasLeidas", "FilasAceptadas", "FilasRechazadas", "FkUsuario" }
        .All(p => typeof(LoteCargaAsistenteCrear).GetProperty(p) is null));

// ============================================================================
//  v4 · Prueba de capas: ServicioSesion + repositorio falso
//  El repositorio falso SIMULA EL TRIGGER (ADR D20): si recibe
//  numero_reunion = 0, calcula él mismo el consecutivo de la cátedra. Ese
//  cálculo vive SOLO en el repositorio falso; la prueba comprueba que el
//  SERVICIO le pasó el 0 tal cual (no lo calculó por su cuenta).
// ============================================================================
var repoSesion = new RepositorioSesionFalso();
var servicioSesion = new ServicioSesion(repoSesion);

Console.WriteLine("\n== v4 · Prueba de capas: ServicioSesion + repositorio falso en memoria ==\n");

var (datosSes, totalSes) = await servicioSesion.ListarAsync(null, null);
Afirmar("Listar() devuelve las 3 sesiones semilla", totalSes == 3 && datosSes.Count == 3);

var (datosSes3975, _) = await servicioSesion.ListarAsync(3975, null);
Afirmar("Listar(fkCatedra=3975) trae solo las de esa cátedra, en orden",
    datosSes3975.Count == 2 && datosSes3975.All(s => s.FkCatedra == 3975)
    && datosSes3975[0].NumeroReunion == 1 && datosSes3975[1].NumeroReunion == 2);

await AfirmarLanza<ArgumentException>(
    "Listar(limite 0) lanza ArgumentException", () => servicioSesion.ListarAsync(null, 0));

// Hora de Bogotá (-05:00): 14:00 local = 19:00 UTC.
var inicioBogota = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.FromHours(-5));
SesionCrear NuevaSesion(DateTimeOffset inicio, DateTimeOffset fin, short? numero = null) => new()
{
    FkCatedra = 5498, Inicio = inicio, Fin = fin, NumeroReunion = numero,
    FkModalidad = "PRESENCIAL", FkSede = "SAN_BENITO", FkPeriodo = "2026-2",
};

var (idSes1, numSes1, creadaSes1) = await servicioSesion.CrearAsync(NuevaSesion(inicioBogota, inicioBogota.AddHours(2)));
Afirmar("Crear() sin numero_reunion: el servicio le pasa 0 al repositorio (no lo calcula)",
    repoSesion.UltimoNumeroRecibido == 0);
Afirmar("...y devuelve el número que puso la 'BD' (1, primera de la cátedra)",
    numSes1 == 1 && idSes1 > 0 && creadaSes1 != default);

var (_, numSes2, _) = await servicioSesion.CrearAsync(NuevaSesion(inicioBogota.AddDays(7), inicioBogota.AddDays(7).AddHours(2)));
Afirmar("Crear() otra vez en la misma cátedra -> numero_reunion 2", numSes2 == 2);

var (_, numSes7, _) = await servicioSesion.CrearAsync(NuevaSesion(inicioBogota.AddDays(14), inicioBogota.AddDays(14).AddHours(2), 7));
Afirmar("Crear() con numero_reunion explícito (7) lo respeta tal cual",
    numSes7 == 7 && repoSesion.UltimoNumeroRecibido == 7);

var ses1 = await servicioSesion.ObtenerAsync(idSes1);
Afirmar("Crear() guarda las fechas en UTC (14:00 -05:00 -> 19:00 UTC) y estado PROGRAMADA",
    ses1.Inicio == new DateTime(2026, 10, 20, 19, 0, 0, DateTimeKind.Utc)
    && ses1.Inicio.Kind == DateTimeKind.Utc && ses1.Estado == "PROGRAMADA");

int creacionesAntes = repoSesion.LlamadasCrear;
await AfirmarLanza<ArgumentException>(
    "Crear() con fin anterior a inicio lanza ArgumentException (RF125)",
    () => servicioSesion.CrearAsync(NuevaSesion(inicioBogota, inicioBogota.AddHours(-1))));
await AfirmarLanza<ArgumentException>(
    "Crear() con fin IGUAL a inicio lanza ArgumentException",
    () => servicioSesion.CrearAsync(NuevaSesion(inicioBogota, inicioBogota)));
Afirmar("...y en ninguno de los dos casos se llamó al repositorio", repoSesion.LlamadasCrear == creacionesAntes);

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(99999) lanza NoEncontradoExcepcion", () => servicioSesion.ObtenerAsync(99999));

int filasPutSes = await servicioSesion.ReemplazarAsync(idSes1, new SesionReemplazo
{
    FkCatedra = 5498, NumeroReunion = 1, Inicio = inicioBogota, Fin = inicioBogota.AddHours(3),
    FkModalidad = "VIRTUAL", FkSede = "VIRTUAL", FkPeriodo = "2026-2", Estado = "ABIERTA",
    EnlaceVirtual = "https://ejemplo.edu.co/sala",
});
var trasPutSes = await servicioSesion.ObtenerAsync(idSes1);
Afirmar("PUT reemplaza todo (modalidad, estado, enlace y fin)",
    filasPutSes == 1 && trasPutSes.FkModalidad == "VIRTUAL" && trasPutSes.Estado == "ABIERTA"
    && trasPutSes.Fin == new DateTime(2026, 10, 20, 22, 0, 0, DateTimeKind.Utc));

await AfirmarLanza<ArgumentException>(
    "PUT con fin anterior a inicio lanza ArgumentException",
    () => servicioSesion.ReemplazarAsync(idSes1, new SesionReemplazo
    {
        FkCatedra = 5498, NumeroReunion = 1, Inicio = inicioBogota, Fin = inicioBogota.AddMinutes(-30),
        FkModalidad = "VIRTUAL", FkSede = "VIRTUAL", FkPeriodo = "2026-2", Estado = "ABIERTA",
    }));

int filasPatchSes = await servicioSesion.ActualizarAsync(idSes1, new SesionActualizar { Estado = "CERRADA" });
var trasPatchSes = await servicioSesion.ObtenerAsync(idSes1);
Afirmar("PATCH cambia solo 'estado'",
    filasPatchSes == 1 && trasPatchSes.Estado == "CERRADA" && trasPatchSes.FkModalidad == "VIRTUAL");

await AfirmarLanza<ArgumentException>(
    "PATCH que manda SOLO 'fin' antes del inicio guardado lanza ArgumentException",
    () => servicioSesion.ActualizarAsync(idSes1, new SesionActualizar { Fin = inicioBogota.AddHours(-2) }));

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioSesion.ActualizarAsync(idSes1, new SesionActualizar()));

await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH con fecha a una sesión inexistente lanza NoEncontradoExcepcion",
    () => servicioSesion.ActualizarAsync(99999, new SesionActualizar { Inicio = inicioBogota }));

Afirmar("DELETE elimina 1 fila", await servicioSesion.EliminarAsync(idSes1) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioSesion.EliminarAsync(idSes1));

// RNF10 por REFLEXIÓN: la interfaz del repositorio no ofrece ningún método
// para "calcular el siguiente número"; el número lo decide la BD.
Afirmar("IRepositorioSesion no declara un método para calcular el consecutivo (ADR D20)",
    typeof(IRepositorioSesion).GetMethods().All(m => !m.Name.Contains("Siguiente") && !m.Name.Contains("Max")));

// ============================================================================
//  v4 · Prueba de capas: ServicioPonencia + repositorio falso
//  LLAVE COMPUESTA SIN VIGENCIA (ADR D19): se asigna y se BORRA; no se
//  "cierra". Desde el RETROFIT criterio 3 el PUT cambia el ROL (único
//  cambio con sentido: las 3 columnas son la llave).
// ============================================================================
var servicioPonencia = new ServicioPonencia(new RepositorioPonenciaFalso());

Console.WriteLine("\n== v4 · Prueba de capas: ServicioPonencia + repositorio falso en memoria ==\n");

var ponente = await servicioPonencia.CrearAsync(new PonenciaCrear { FkSesion = 10, FkAsistente = 167 });
Afirmar("Crear() sin rol asigna PONENTE por defecto", ponente.Rol == "PONENTE");

await AfirmarLanza<InvalidOperationException>(
    "Crear() con la MISMA llave exacta falla (PK duplicada simulada; en la API real -> 500)",
    () => servicioPonencia.CrearAsync(new PonenciaCrear { FkSesion = 10, FkAsistente = 167, Rol = "PONENTE" }));

var moderador = await servicioPonencia.CrearAsync(new PonenciaCrear { FkSesion = 10, FkAsistente = 167, Rol = "MODERADOR" });
Afirmar("Crear() con el mismo asistente y OTRO rol es una fila nueva válida", moderador.Rol == "MODERADOR");

var (datosPonS10, _) = await servicioPonencia.ListarAsync(10, null, null);
Afirmar("Listar(fkSesion=10) trae las 2 filas del asistente 167", datosPonS10.Count == 2);

var (datosPonA167, _) = await servicioPonencia.ListarAsync(null, 167, null);
Afirmar("Listar(fkAsistente=167) trae también la semilla (sesión 1)", datosPonA167.Count == 3);

var (datosPonAmbos, _) = await servicioPonencia.ListarAsync(1, 167, null);
Afirmar("Listar(fkSesion=1, fkAsistente=167) combina los dos filtros", datosPonAmbos.Count == 1);

await AfirmarLanza<ArgumentException>(
    "Listar(limite 0) lanza ArgumentException", () => servicioPonencia.ListarAsync(null, null, 0));

var leida = await servicioPonencia.ObtenerAsync(10, 167, "PONENTE");
Afirmar("Obtener(10, 167, PONENTE) encuentra la llave exacta", leida.FkSesion == 10 && leida.Rol == "PONENTE");

Afirmar("DELETE por la llave completa elimina 1 fila", await servicioPonencia.EliminarAsync(10, 167, "PONENTE") == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener la llave borrada lanza NoEncontradoExcepcion (404)",
    () => servicioPonencia.ObtenerAsync(10, 167, "PONENTE"));
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion (404)",
    () => servicioPonencia.EliminarAsync(10, 167, "PONENTE"));

var (datosPonS10b, _) = await servicioPonencia.ListarAsync(10, null, null);
Afirmar("...y el MODERADOR de la misma sesión sigue ahí (otra fila)",
    datosPonS10b.Count == 1 && datosPonS10b[0].Rol == "MODERADOR");

// Por REFLEXIÓN: llave compuesta (sin ObtenerPorIdAsync) y, desde el RETROFIT
// criterio 3, SÍ hay ActualizarAsync: sp_actualizar_ponencia cambia el ROL
// (revierte ADR D19, "sin actualizar"; ver AJUSTES_PATRON_SEDE_LOTE.md §2 · Ponencia).
Afirmar("IRepositorioPonencia no declara ObtenerPorIdAsync, pero SÍ ActualizarAsync (retrofit) y EliminarAsync",
    typeof(IRepositorioPonencia).GetMethod("ObtenerPorIdAsync") is null
    && typeof(IRepositorioPonencia).GetMethod("ActualizarAsync") is not null
    && typeof(IRepositorioPonencia).GetMethod("EliminarAsync") is not null);
Afirmar("El modelo Ponencia no tiene propiedad 'Id' (llave compuesta)",
    typeof(Ponencia).GetProperty("Id") is null && typeof(Ponencia).GetProperty("IdPonencia") is null);
Afirmar("Peticiones: existe PonenciaReemplazo (PUT, solo 'rol') y NO existe PonenciaActualizar (sin PATCH)",
    typeof(PonenciaCrear).Assembly.GetType("ApiCatedras.Peticiones.PonenciaReemplazo") is not null
    && typeof(PonenciaCrear).Assembly.GetType("ApiCatedras.Peticiones.PonenciaActualizar") is null
    && string.Join(",", typeof(PonenciaReemplazo).GetProperties().Select(p => p.Name)) == "Rol");

// PUT /api/Ponencia/{fkSesion}/{fkAsistente}/{rol}: cambia el rol de la fila.
var ponCambiada = await servicioPonencia.ReemplazarAsync(10, 167, "MODERADOR", new PonenciaReemplazo { Rol = "INVITADO" });
Afirmar("PUT (Reemplazar) MODERADOR -> INVITADO devuelve la llave nueva",
    ponCambiada.FkSesion == 10 && ponCambiada.FkAsistente == 167 && ponCambiada.Rol == "INVITADO");
var ponInvitado = await servicioPonencia.ObtenerAsync(10, 167, "INVITADO");
Afirmar("...y la fila existe con el rol nuevo", ponInvitado.Rol == "INVITADO");
await AfirmarLanza<NoEncontradoExcepcion>(
    "...y la llave vieja (MODERADOR) ya no existe (404)",
    () => servicioPonencia.ObtenerAsync(10, 167, "MODERADOR"));
await AfirmarLanza<NoEncontradoExcepcion>(
    "PUT sobre una llave inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioPonencia.ReemplazarAsync(10, 167, "PONENTE", new PonenciaReemplazo { Rol = "MODERADOR" }));
await servicioPonencia.CrearAsync(new PonenciaCrear { FkSesion = 10, FkAsistente = 167, Rol = "PONENTE" });
await AfirmarLanza<InvalidOperationException>(
    "PUT a un rol que el asistente YA tiene en esa sesión falla (PK duplicada simulada; en la API real -> 500)",
    () => servicioPonencia.ReemplazarAsync(10, 167, "PONENTE", new PonenciaReemplazo { Rol = "INVITADO" }));

// ############################################################################
//  VERSIÓN 5 · Usuarios, roles, enlaces, claves y registro de asistencia
//  (criterio de aceptación 8 de la v5)
//  Cada bloque usa SOLO el servicio real + un repositorio FALSO en memoria.
//  Nada de PostgreSQL: las funciones de la BD (fn_emitir_enlace_api,
//  fn_solicitar_clave_api, fn_validar_clave_api) se simulan en los falsos.
// ############################################################################

// ============================================================================
//  v5 · Prueba de capas: ServicioUsuario + repositorios falsos
//  BCrypt REAL (no simulado): se verifica el hash que el servicio calculó.
// ============================================================================
var repoUsr5 = new RepositorioUsuarioFalso();
var repoRolesUsr5 = new RepositorioRolPorUsuarioFalso();
var repoCodRec71 = new RepositorioCodigoRecuperacionFalso();
var servicioUsuario5 = new ServicioUsuario(repoUsr5, repoRolesUsr5, repoCodRec71);

Console.WriteLine("\n== v5 · Prueba de capas: ServicioUsuario + repositorios falsos en memoria ==\n");

var (datosUsr5, totalUsr5) = await servicioUsuario5.ListarAsync(null);
Afirmar("Listar() devuelve los 2 usuarios semilla", totalUsr5 == 2 && datosUsr5.Count == 2);

await AfirmarLanza<ArgumentException>(
    "Listar(0) lanza ArgumentException", () => servicioUsuario5.ListarAsync(0));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioUsuario5.ObtenerAsync(999));

// --- Crear: el hash es BCrypt de verdad (RF134, D23) ---
var nuevoUsr5 = await servicioUsuario5.CrearAsync(new UsuarioCrear
{ FkAsistente = 3, Usuario = "ana.prueba", Clave = "Secreta123" });
var guardadoUsr5 = (await repoUsr5.ObtenerPorIdAsync(nuevoUsr5.IdUsuario))!;
Afirmar("Crear() devuelve id y creado_en de la 'BD'", nuevoUsr5.IdUsuario > 2 && nuevoUsr5.CreadoEn != default);
Afirmar("Crear() NO guarda la clave en claro (el hash empieza por $2 y es distinto)",
    guardadoUsr5.ClaveHash != "Secreta123" && guardadoUsr5.ClaveHash.StartsWith("$2"));
Afirmar("BCrypt.Verify(clave, hashGuardado) es verdadero con la clave correcta",
    BCrypt.Net.BCrypt.Verify("Secreta123", guardadoUsr5.ClaveHash));
Afirmar("BCrypt.Verify con otra clave es falso",
    !BCrypt.Net.BCrypt.Verify("OtraClave99", guardadoUsr5.ClaveHash));
Afirmar("Crear() sin 'activo' deja la cuenta activa", guardadoUsr5.Activo);

await AfirmarLanza<InvalidOperationException>(
    "Crear() con un 'usuario' repetido falla (uq_usuario_login simulado; en la API real -> 500)",
    () => servicioUsuario5.CrearAsync(new UsuarioCrear { FkAsistente = 4, Usuario = "ana.prueba", Clave = "Secreta123" }));

// --- RNF13 / RF138: el hash nunca sale en el JSON ---
string jsonUsr5 = System.Text.Json.JsonSerializer.Serialize(guardadoUsr5);
Afirmar("Serializar un Usuario NO incluye 'clave_hash' ni el texto del hash (RF138)",
    !jsonUsr5.Contains("clave_hash") && !jsonUsr5.Contains(guardadoUsr5.ClaveHash)
    && jsonUsr5.Contains("\"usuario\":\"ana.prueba\""));

// --- PUT: fija una clave nueva ---
string hashAntesPut5 = guardadoUsr5.ClaveHash;
int filasPutUsr5 = await servicioUsuario5.ReemplazarAsync(nuevoUsr5.IdUsuario, new UsuarioReemplazo
{ FkAsistente = 3, Usuario = "ana.nueva", Clave = "ClaveNueva456", Activo = true });
var trasPutUsr5 = (await repoUsr5.ObtenerPorIdAsync(nuevoUsr5.IdUsuario))!;
Afirmar("PUT reemplaza el nombre y re-hashea la clave",
    filasPutUsr5 == 1 && trasPutUsr5.NombreUsuario == "ana.nueva"
    && trasPutUsr5.ClaveHash != hashAntesPut5
    && BCrypt.Net.BCrypt.Verify("ClaveNueva456", trasPutUsr5.ClaveHash)
    && !BCrypt.Net.BCrypt.Verify("Secreta123", trasPutUsr5.ClaveHash));

// --- PATCH: sin 'clave' el hash se conserva (RF136) ---
string hashAntesPatch5 = trasPutUsr5.ClaveHash;
await servicioUsuario5.ActualizarAsync(nuevoUsr5.IdUsuario, new UsuarioActualizar { Activo = false });
var trasPatchUsr5 = (await repoUsr5.ObtenerPorIdAsync(nuevoUsr5.IdUsuario))!;
Afirmar("PATCH sin 'clave' cambia 'activo' y deja el hash INTACTO",
    !trasPatchUsr5.Activo && trasPatchUsr5.ClaveHash == hashAntesPatch5);

await servicioUsuario5.ActualizarAsync(nuevoUsr5.IdUsuario, new UsuarioActualizar { Clave = "TerceraClave789" });
var trasPatchClaveUsr5 = (await repoUsr5.ObtenerPorIdAsync(nuevoUsr5.IdUsuario))!;
Afirmar("PATCH con 'clave' re-hashea y la nueva clave verifica",
    trasPatchClaveUsr5.ClaveHash != hashAntesPatch5
    && BCrypt.Net.BCrypt.Verify("TerceraClave789", trasPatchClaveUsr5.ClaveHash));

await AfirmarLanza<ArgumentException>(
    "PATCH sin campos lanza ArgumentException",
    () => servicioUsuario5.ActualizarAsync(nuevoUsr5.IdUsuario, new UsuarioActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "PATCH a un usuario inexistente lanza NoEncontradoExcepcion",
    () => servicioUsuario5.ActualizarAsync(999, new UsuarioActualizar { Activo = true }));
await AfirmarLanza<NoEncontradoExcepcion>(
    "PUT a un usuario inexistente lanza NoEncontradoExcepcion",
    () => servicioUsuario5.ReemplazarAsync(999, new UsuarioReemplazo
    { FkAsistente = 1, Usuario = "nadie.aqui", Clave = "Clave12345", Activo = true }));

// --- LOGIN (RF139, D27) ---
int accesosAntes5 = repoUsr5.LlamadasUltimoAcceso;
var loginOk5 = await servicioUsuario5.LoginAsync(new UsuarioLogin { Usuario = "admin", Clave = "Admin12345" });
Afirmar("Login correcto devuelve el usuario", loginOk5 is not null && loginOk5.Value.Usuario.NombreUsuario == "admin");
Afirmar("...con SOLO los roles vigentes (ADMIN sí; CONSULTA, ya cerrado, no)",
    loginOk5 is not null && loginOk5.Value.Roles.SequenceEqual(new[] { "ADMIN" }));
Afirmar("...y actualiza ultimo_acceso (1 llamada nueva y valor en el objeto)",
    repoUsr5.LlamadasUltimoAcceso == accesosAntes5 + 1 && loginOk5!.Value.Usuario.UltimoAcceso is not null);

var loginNoExiste5 = await servicioUsuario5.LoginAsync(new UsuarioLogin { Usuario = "no.existe", Clave = "Admin12345" });
var loginClaveMala5 = await servicioUsuario5.LoginAsync(new UsuarioLogin { Usuario = "admin", Clave = "ClaveIncorrecta" });
var loginInactivo5 = await servicioUsuario5.LoginAsync(new UsuarioLogin { Usuario = "inactivo.prueba", Clave = "Clave12345" });
Afirmar("Login con usuario inexistente devuelve null", loginNoExiste5 is null);
Afirmar("Login con clave incorrecta devuelve null", loginClaveMala5 is null);
Afirmar("Login con usuario INACTIVO (aunque la clave sea correcta) devuelve null", loginInactivo5 is null);
Afirmar("Las 3 causas dan el MISMO resultado (D27) y ninguna actualizó ultimo_acceso",
    loginNoExiste5 is null && loginClaveMala5 is null && loginInactivo5 is null
    && repoUsr5.LlamadasUltimoAcceso == accesosAntes5 + 1);

// --- Controlador: null -> 401 con el mismo mensaje; éxito -> 200 ---
var controladorUsr5 = new ApiCatedras.Controllers.UsuarioController(servicioUsuario5);
var respMala5 = await controladorUsr5.Login(new UsuarioLogin { Usuario = "admin", Clave = "ClaveIncorrecta" });
var respInactivo5 = await controladorUsr5.Login(new UsuarioLogin { Usuario = "inactivo.prueba", Clave = "Clave12345" });
var respNoExiste5 = await controladorUsr5.Login(new UsuarioLogin { Usuario = "no.existe", Clave = "Admin12345" });
Afirmar("El controlador responde 401 (UnauthorizedObjectResult) en las 3 causas",
    respMala5 is Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult
    && respInactivo5 is Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult
    && respNoExiste5 is Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult);
string cuerpo401a5 = System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult)respMala5).Value);
string cuerpo401b5 = System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult)respNoExiste5).Value);
Afirmar("...y el cuerpo es IDÉNTICO (no revela cuál fue la causa) y dice 'Usuario o clave incorrectos'",
    cuerpo401a5 == cuerpo401b5 && cuerpo401a5.Contains("Usuario o clave incorrectos"));
var respOk5 = await controladorUsr5.Login(new UsuarioLogin { Usuario = "admin", Clave = "Admin12345" });
Afirmar("El controlador responde 200 (OkObjectResult) con credenciales correctas",
    respOk5 is Microsoft.AspNetCore.Mvc.OkObjectResult);
string cuerpo200_5 = System.Text.Json.JsonSerializer.Serialize(((Microsoft.AspNetCore.Mvc.OkObjectResult)respOk5).Value);
Afirmar("...y la respuesta 200 NO trae el hash",
    !cuerpo200_5.Contains("clave_hash") && !cuerpo200_5.Contains("$2") && cuerpo200_5.Contains("ADMIN"));

// --- DELETE ---
Afirmar("DELETE elimina 1 fila", await servicioUsuario5.EliminarAsync(nuevoUsr5.IdUsuario) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "segundo DELETE lanza NoEncontradoExcepcion", () => servicioUsuario5.EliminarAsync(nuevoUsr5.IdUsuario));

// ============================================================================
//  v5 · Prueba de capas: ServicioRolPorUsuario + repositorio falso
//  Tabla de LLAVE COMPUESTA (ADR D16): sin id, se abre y se cierra.
// ============================================================================
var repoRol5 = new RepositorioRolPorUsuarioFalso();
var servicioRol5 = new ServicioRolPorUsuario(repoRol5);

Console.WriteLine("\n== v5 · Prueba de capas: ServicioRolPorUsuario + repositorio falso en memoria ==\n");

var rolNuevo5 = await servicioRol5.CrearAsync(new RolPorUsuarioCrear { FkUsuario = 2, FkRol = "CONSULTA" });
Afirmar("Crear() abre con fecha_ini = hoy y sin fecha_fin",
    rolNuevo5.FechaIni == DateOnly.FromDateTime(DateTime.Today) && rolNuevo5.FechaFin is null);

var (datosRolU1_5, _) = await servicioRol5.ListarAsync(1, null);
Afirmar("Listar(fkUsuario=1) trae solo las 2 filas semilla de ese usuario", datosRolU1_5.Count == 2);
var (datosRolTodos5, _) = await servicioRol5.ListarAsync(null, null);
Afirmar("Listar() sin filtro trae también el rol recién abierto", datosRolTodos5.Count == 3);
await AfirmarLanza<ArgumentException>(
    "Listar(limite 0) lanza ArgumentException", () => servicioRol5.ListarAsync(null, 0));

await AfirmarLanza<InvalidOperationException>(
    "Crear() con la MISMA llave exacta falla (PK duplicada simulada; en la API real -> 500)",
    () => servicioRol5.CrearAsync(new RolPorUsuarioCrear { FkUsuario = 2, FkRol = "CONSULTA", FechaIni = rolNuevo5.FechaIni }));

var cierreRol5 = new RolPorUsuarioCerrar
{ FkUsuario = 2, FkRol = "CONSULTA", FechaIni = rolNuevo5.FechaIni, FechaFin = rolNuevo5.FechaIni.AddDays(30) };

await AfirmarLanza<ArgumentException>(
    "Cerrar con fecha_fin anterior a fecha_ini lanza ArgumentException",
    () => servicioRol5.CerrarAsync(new RolPorUsuarioCerrar
    { FkUsuario = 2, FkRol = "CONSULTA", FechaIni = rolNuevo5.FechaIni, FechaFin = rolNuevo5.FechaIni.AddDays(-1) }));

var rolCerrado5 = await servicioRol5.CerrarAsync(cierreRol5);
Afirmar("Cerrar() pone fecha_fin", rolCerrado5.FechaFin == rolNuevo5.FechaIni.AddDays(30));

int cierresAntesRol5 = repoRol5.LlamadasCerrar;
await AfirmarLanza<ArgumentException>(
    "Cerrar dos veces lanza ArgumentException (400)", () => servicioRol5.CerrarAsync(cierreRol5));
Afirmar("...y la segunda vez NO se llamó al UPDATE del repositorio", repoRol5.LlamadasCerrar == cierresAntesRol5);

await AfirmarLanza<NoEncontradoExcepcion>(
    "Cerrar una llave inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioRol5.CerrarAsync(new RolPorUsuarioCerrar
    { FkUsuario = 2, FkRol = "ADMIN", FechaIni = new DateOnly(2020, 1, 1) }));

Afirmar("IRepositorioRolPorUsuario no declara ObtenerPorId/Actualizar/Eliminar (ADR D16)",
    new[] { "ObtenerPorIdAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioRolPorUsuario).GetMethod(m) is null));
Afirmar("El modelo RolPorUsuario no tiene propiedad 'Id' (llave compuesta)",
    typeof(RolPorUsuario).GetProperty("Id") is null && typeof(RolPorUsuario).GetProperty("IdRolPorUsuario") is null);

// ============================================================================
//  v5 · Prueba de capas: ServicioEnlaceRegistro + repositorio falso
//  NO es un CRUD (ADR D24): se EMITE (función de la BD, simulada) y se REVOCA.
// ============================================================================
var repoEnl5 = new RepositorioEnlaceRegistroFalso();
var servicioEnl5 = new ServicioEnlaceRegistro(repoEnl5);

Console.WriteLine("\n== v5 · Prueba de capas: ServicioEnlaceRegistro + repositorio falso en memoria ==\n");

var (datosEnlS10_5, _) = await servicioEnl5.ListarAsync(10, null);
Afirmar("Listar(fkSesion=10) trae el enlace semilla vigente", datosEnlS10_5.Count == 1 && datosEnlS10_5[0].RevocadoEn is null);

long idEnlNuevo5 = await servicioEnl5.EmitirAsync(new EnlaceRegistroEmitir { FkSesion = 10, MinutosAntes = 15 });
var enlNuevo5 = await servicioEnl5.ObtenerAsync(idEnlNuevo5);
Afirmar("Emitir() devuelve el id que decidió la 'BD' y el enlace nace vigente",
    idEnlNuevo5 > 1 && enlNuevo5.RevocadoEn is null && enlNuevo5.Token != Guid.Empty);
Afirmar("...con canal, url y ventana calculados por la BD (no por el cliente)",
    enlNuevo5.Canal == "QR" && enlNuevo5.UrlPublica.Contains(enlNuevo5.Token.ToString())
    && enlNuevo5.VentanaHasta > enlNuevo5.VentanaDesde);
Afirmar("...y el servicio PASÓ los minutos opcionales tal cual (15 antes, sin 'después')",
    repoEnl5.UltimosMinutosAntes == 15 && repoEnl5.UltimosMinutosDespues is null);

var enlAnterior5 = await servicioEnl5.ObtenerAsync(1);
Afirmar("RN-14: emitir un segundo enlace para la misma sesión REVOCÓ el primero", enlAnterior5.RevocadoEn is not null);

await AfirmarLanza<InvalidOperationException>(
    "Emitir para una sesión inexistente falla (RAISE de la función simulado; en la API real -> 500, D25)",
    () => servicioEnl5.EmitirAsync(new EnlaceRegistroEmitir { FkSesion = 999 }));

await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioEnl5.ObtenerAsync(999));
await AfirmarLanza<ArgumentException>(
    "Listar(limite 0) lanza ArgumentException", () => servicioEnl5.ListarAsync(null, 0));

Afirmar("Revocar() sobre un enlace vigente afecta 1 fila", await servicioEnl5.RevocarAsync(idEnlNuevo5) == 1);
int revocacionesAntes5 = repoEnl5.LlamadasRevocar;
await AfirmarLanza<ArgumentException>(
    "Revocar dos veces lanza ArgumentException (400)", () => servicioEnl5.RevocarAsync(idEnlNuevo5));
Afirmar("...y la segunda vez NO se llamó al UPDATE del repositorio", repoEnl5.LlamadasRevocar == revocacionesAntes5);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Revocar un enlace inexistente lanza NoEncontradoExcepcion (404)", () => servicioEnl5.RevocarAsync(999));

Afirmar("IRepositorioEnlaceRegistro no declara Crear/Reemplazar/Actualizar/Eliminar (RF147)",
    new[] { "CrearAsync", "ReemplazarAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioEnlaceRegistro).GetMethod(m) is null));
Afirmar("EnlaceRegistroEmitir NO tiene canal, token, ventana ni url_publica (RF148)",
    new[] { "Canal", "Token", "Ventana", "VentanaDesde", "UrlPublica" }
        .All(p => typeof(EnlaceRegistroEmitir).GetProperty(p) is null));

// ============================================================================
//  v7.1 · Prueba de capas: cambiar la clave (RF-S5) y recuperarla (RF-S5/RF-S6)
//  Todo el hash BCrypt sale de ServicioUsuario; el repositorio falso de
//  códigos solo guarda la cadena opaca.
// ============================================================================
Console.WriteLine("\n== v7.1 · Prueba de capas: cambiar y recuperar la clave ==\n");

// Cambiar sabiendo la actual (usuario 1 = admin / Admin12345)
Afirmar("CambiarClave con la clave actual correcta devuelve true y guarda un hash NUEVO",
    await servicioUsuario5.CambiarClaveAsync(1, new UsuarioCambiarClave { ClaveActual = "Admin12345", ClaveNueva = "Nueva-Clave-71" }));
var trasCambio71 = await servicioUsuario5.LoginAsync(new UsuarioLogin { Usuario = "admin", Clave = "Nueva-Clave-71" });
Afirmar("...y el login ya funciona con la clave nueva", trasCambio71 is not null);
Afirmar("...y la clave anterior dejó de servir",
    await servicioUsuario5.LoginAsync(new UsuarioLogin { Usuario = "admin", Clave = "Admin12345" }) is null);
Afirmar("CambiarClave con la clave actual INCORRECTA devuelve false (401, D27)",
    !await servicioUsuario5.CambiarClaveAsync(1, new UsuarioCambiarClave { ClaveActual = "equivocada", ClaveNueva = "Otra-Clave-71" }));
Afirmar("CambiarClave en una cuenta INACTIVA devuelve false (misma respuesta)",
    !await servicioUsuario5.CambiarClaveAsync(2, new UsuarioCambiarClave { ClaveActual = "Clave12345", ClaveNueva = "Otra-Clave-71" }));
await AfirmarLanza<ArgumentException>(
    "CambiarClave con la clave nueva IGUAL a la actual lanza ArgumentException (400)",
    () => servicioUsuario5.CambiarClaveAsync(1, new UsuarioCambiarClave { ClaveActual = "Nueva-Clave-71", ClaveNueva = "Nueva-Clave-71" }));
await AfirmarLanza<NoEncontradoExcepcion>(
    "CambiarClave de un usuario inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioUsuario5.CambiarClaveAsync(999, new UsuarioCambiarClave { ClaveActual = "x", ClaveNueva = "Otra-Clave-71" }));
Afirmar("UsuarioCambiarClave exige clave nueva de al menos 8 caracteres",
    typeof(UsuarioCambiarClave).GetProperty("ClaveNueva")!
        .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MinLengthAttribute), false).Length == 1);
// (la cuenta de prueba vuelve a su clave original para no alterar las pruebas siguientes)
await servicioUsuario5.CambiarClaveAsync(1, new UsuarioCambiarClave { ClaveActual = "Nueva-Clave-71", ClaveNueva = "Admin12345" });

// Recuperar una clave olvidada
await AfirmarLanza<ArgumentException>(
    "SolicitarRecuperacion con una 'ip' inválida lanza ArgumentException (400)",
    () => servicioUsuario5.SolicitarRecuperacionAsync(new RecuperacionSolicitar { Identificador = "admin", Ip = "999.9.9.9" }));
Afirmar("SolicitarRecuperacion de una cuenta que no existe devuelve null (la API responde igual, D27)",
    await servicioUsuario5.SolicitarRecuperacionAsync(new RecuperacionSolicitar { Identificador = "nadie.existe" }) is null);
var rec71 = await servicioUsuario5.SolicitarRecuperacionAsync(new RecuperacionSolicitar { Identificador = "  admin  ", Ip = "10.0.0.7" });
Afirmar("SolicitarRecuperacion devuelve id, código en claro y correo de destino (RNF12)",
    rec71 is not null && rec71.Value.IdCodigo > 0 && rec71.Value.Codigo == RepositorioCodigoRecuperacionFalso.CodigoFijo
    && rec71.Value.EnviadoA.Contains('@'));
Afirmar("...el identificador llegó sin espacios y la IP ya validada",
    repoCodRec71.UltimoIdentificador == "admin" && repoCodRec71.UltimaIp == "10.0.0.7");

await AfirmarLanza<CodigoRecuperacionInvalidoExcepcion>(
    "Restablecer con el código INCORRECTO lanza CodigoRecuperacionInvalidoExcepcion (400)",
    () => servicioUsuario5.RestablecerClaveAsync(new RecuperacionRestablecer { IdCodigo = rec71!.Value.IdCodigo, Codigo = "000000", ClaveNueva = "Recuperada-71" }));
Afirmar("...y el servicio no llegó a guardar ningún hash", repoCodRec71.UltimoHashRecibido is null);
await servicioUsuario5.RestablecerClaveAsync(new RecuperacionRestablecer
{ IdCodigo = rec71!.Value.IdCodigo, Codigo = rec71.Value.Codigo, ClaveNueva = "Recuperada-71" });
Afirmar("Restablecer con el código correcto guarda un hash BCrypt, NUNCA la clave en claro (Regla 3)",
    repoCodRec71.UltimoHashRecibido is not null
    && repoCodRec71.UltimoHashRecibido != "Recuperada-71"
    && BCrypt.Net.BCrypt.Verify("Recuperada-71", repoCodRec71.UltimoHashRecibido));
await AfirmarLanza<CodigoRecuperacionInvalidoExcepcion>(
    "Restablecer OTRA VEZ con el mismo código falla (ya usado; la API responde 400)",
    () => servicioUsuario5.RestablecerClaveAsync(new RecuperacionRestablecer { IdCodigo = rec71!.Value.IdCodigo, Codigo = rec71.Value.Codigo, ClaveNueva = "Otra-71-Clave" }));
Afirmar("IRepositorioCodigoRecuperacion no declara Crear/Reemplazar/Actualizar/Eliminar (no es un CRUD)",
    new[] { "CrearAsync", "ReemplazarAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioCodigoRecuperacion).GetMethod(m) is null));

// ============================================================================
//  v5 · Prueba de capas: ServicioClaveAcceso + repositorio falso
//  NO es un CRUD (ADR D24): SOLICITAR y VALIDAR (funciones de la BD simuladas).
// ============================================================================
var repoCla5 = new RepositorioClaveAccesoFalso();
var servicioCla5 = new ServicioClaveAcceso(repoCla5);
string tokenOk5 = RepositorioClaveAccesoFalso.TokenVigente.ToString();

Console.WriteLine("\n== v5 · Prueba de capas: ServicioClaveAcceso + repositorio falso en memoria ==\n");

// Validaciones de FORMA del servicio: 400 ANTES de tocar el repositorio.
int solicitudesAntes5 = repoCla5.LlamadasSolicitar;
await AfirmarLanza<ArgumentException>(
    "Solicitar con un 'token' que no es uuid lanza ArgumentException (400)",
    () => servicioCla5.SolicitarAsync(new ClaveAccesoSolicitar { Identificador = "1000090", Token = "no-es-un-uuid" }));
await AfirmarLanza<ArgumentException>(
    "Solicitar con una 'ip' inválida lanza ArgumentException (400)",
    () => servicioCla5.SolicitarAsync(new ClaveAccesoSolicitar { Identificador = "1000090", Token = tokenOk5, Ip = "999.1.1.1" }));
Afirmar("...y en ninguno de los dos casos se llamó al repositorio", repoCla5.LlamadasSolicitar == solicitudesAntes5);

var solicitud5 = await servicioCla5.SolicitarAsync(new ClaveAccesoSolicitar
{ Identificador = "1000090", Token = tokenOk5, Ip = "10.0.0.5" });
Afirmar("Solicitar() devuelve id_clave, la clave en texto plano y el correo de destino (RNF12)",
    solicitud5.IdClave > 0 && solicitud5.Clave == RepositorioClaveAccesoFalso.ClaveFija
    && solicitud5.EnviadoA.Contains('@'));
Afirmar("...y la IP llegó al repositorio ya validada", repoCla5.UltimaIp == "10.0.0.5");

await AfirmarLanza<InvalidOperationException>(
    "Solicitar con un token de enlace desconocido falla (RAISE simulado; en la API real -> 500, D25)",
    () => servicioCla5.SolicitarAsync(new ClaveAccesoSolicitar { Identificador = "1000090", Token = Guid.NewGuid().ToString() }));

// Validar
await AfirmarLanza<ArgumentException>(
    "Validar con una 'ip' inválida lanza ArgumentException (400)",
    () => servicioCla5.ValidarAsync(new ClaveAccesoValidar { IdClave = solicitud5.IdClave, Clave = solicitud5.Clave, Ip = "abc", Latitud = 6.2m, Longitud = -75.5m }));

await AfirmarLanza<InvalidOperationException>(
    "Validar con la clave INCORRECTA falla (simulado; en la API real -> 500, D25)",
    () => servicioCla5.ValidarAsync(new ClaveAccesoValidar { IdClave = solicitud5.IdClave, Clave = "000000", Latitud = 6.2m, Longitud = -75.5m }));

long idRegistroVal5 = await servicioCla5.ValidarAsync(new ClaveAccesoValidar
{ IdClave = solicitud5.IdClave, Clave = solicitud5.Clave, Ip = "10.0.0.5", UserAgent = "prueba/1.0",
  Latitud = 6.244203m, Longitud = -75.581211m });
Afirmar("Validar() con la clave correcta devuelve el id_registro creado", idRegistroVal5 > 0);

await AfirmarLanza<InvalidOperationException>(
    "Validar OTRA VEZ la misma clave falla (ya usada, RN-20; en la API real -> 500)",
    () => servicioCla5.ValidarAsync(new ClaveAccesoValidar { IdClave = solicitud5.IdClave, Clave = solicitud5.Clave, Latitud = 6.2m, Longitud = -75.5m }));

// ---------------------------------------------------------------------------
//  v7.1 · RF-S1 · Geolocalización obligatoria al validar la clave
// ---------------------------------------------------------------------------
Afirmar("Validar() guardó la ubicación que llegó (RF-S1)",
    repoCla5.UltimaLatitud == 6.244203m && repoCla5.UltimaLongitud == -75.581211m);

var solicitudGeo71 = await servicioCla5.SolicitarAsync(new ClaveAccesoSolicitar
{ Identificador = "1000091", Token = tokenOk5 });
int validacionesAntesGeo71 = repoCla5.LlamadasValidar;
await AfirmarLanza<ArgumentException>(
    "Validar SIN latitud ni longitud lanza ArgumentException (400, RF-S1)",
    () => servicioCla5.ValidarAsync(new ClaveAccesoValidar { IdClave = solicitudGeo71.IdClave, Clave = solicitudGeo71.Clave }));
await AfirmarLanza<ArgumentException>(
    "Validar con latitud pero SIN longitud lanza ArgumentException (400, RF-S1)",
    () => servicioCla5.ValidarAsync(new ClaveAccesoValidar { IdClave = solicitudGeo71.IdClave, Clave = solicitudGeo71.Clave, Latitud = 6.2m }));
Afirmar("...y en ninguno de los dos casos se llamó al repositorio (la clave no gasta intentos)",
    repoCla5.LlamadasValidar == validacionesAntesGeo71);

static bool Obligatoria(Type tipo, string propiedad) =>
    tipo.GetProperty(propiedad)!
        .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Length == 1;
Afirmar("ClaveAccesoValidar marca latitud y longitud como [Required] (-> 422 antes del servicio)",
    Obligatoria(typeof(ClaveAccesoValidar), "Latitud") && Obligatoria(typeof(ClaveAccesoValidar), "Longitud"));

static bool RangoDe(Type tipo, string propiedad, double min, double max)
{
    var r = (System.ComponentModel.DataAnnotations.RangeAttribute?)tipo.GetProperty(propiedad)!
        .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false).FirstOrDefault();
    return r is not null && Convert.ToDouble(r.Minimum) == min && Convert.ToDouble(r.Maximum) == max;
}
Afirmar("...con rango -90..90 para la latitud y -180..180 para la longitud",
    RangoDe(typeof(ClaveAccesoValidar), "Latitud", -90, 90) && RangoDe(typeof(ClaveAccesoValidar), "Longitud", -180, 180));
Afirmar("El modelo RegistroAsistencia expone latitud y longitud NULABLES (MANUAL / IMPORTADO no las tienen)",
    typeof(RegistroAsistencia).GetProperty("Latitud")!.PropertyType == typeof(decimal?)
    && typeof(RegistroAsistencia).GetProperty("Longitud")!.PropertyType == typeof(decimal?));
string jsonReg71 = System.Text.Json.JsonSerializer.Serialize(
    new RegistroAsistencia { FkSesion = 1, FkAsistente = 1, Latitud = 6.25m, Longitud = -75.56m });
Afirmar("...y se serializan como 'latitud' y 'longitud'",
    jsonReg71.Contains("\"latitud\":6.25") && jsonReg71.Contains("\"longitud\":-75.56"));

// Lecturas de auditoría
var claveLeida5 = await servicioCla5.ObtenerAsync(solicitud5.IdClave);
Afirmar("Obtener() muestra usado_en tras validar", claveLeida5.UsadoEn is not null);
string jsonCla5 = System.Text.Json.JsonSerializer.Serialize(claveLeida5);
Afirmar("Serializar una ClaveAcceso NO incluye 'clave_hash' ni la clave en claro (RF151, RNF12)",
    !jsonCla5.Contains("clave_hash") && !jsonCla5.Contains("\"clave\""));
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioCla5.ObtenerAsync(999));
var (datosClaA5, _) = await servicioCla5.ListarAsync(1000090, null);
Afirmar("Listar(fkAsistente) filtra por asistente", datosClaA5.Count == 1 && datosClaA5[0].FkAsistente == 1000090);
await AfirmarLanza<ArgumentException>(
    "Listar(limite 0) lanza ArgumentException", () => servicioCla5.ListarAsync(null, 0));

Afirmar("IRepositorioClaveAcceso no declara Crear/Reemplazar/Actualizar/Eliminar (RF152)",
    new[] { "CrearAsync", "ReemplazarAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioClaveAcceso).GetMethod(m) is null));
Afirmar("La propiedad ClaveHash del modelo lleva [JsonIgnore]",
    typeof(ClaveAcceso).GetProperty("ClaveHash")!
        .GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), false).Length == 1);

// ============================================================================
//  v5 · Prueba de capas: ServicioRegistroAsistencia + repositorio falso
//  Hecho auditable: solo se lee y se da de alta a mano. 'origen' lo valida el
//  SERVICIO (ADR D26) -> 400, nunca 422.
// ============================================================================
var repoReg5 = new RepositorioRegistroAsistenciaFalso();
var servicioReg5 = new ServicioRegistroAsistencia(repoReg5);

Console.WriteLine("\n== v5 · Prueba de capas: ServicioRegistroAsistencia + repositorio falso en memoria ==\n");

var (datosRegS10_5, _) = await servicioReg5.ListarAsync(10, null, null);
Afirmar("Listar(fkSesion=10) trae la semilla", datosRegS10_5.Count == 1);

var regManual5 = await servicioReg5.CrearAsync(new RegistroAsistenciaCrear { FkSesion = 10, FkAsistente = 2, Origen = "MANUAL" });
Afirmar("Crear() MANUAL devuelve la fila con las 2 instantáneas que puso el 'trigger'",
    regManual5.IdRegistro > 1 && regManual5.FkVinculacionSnapshot == "ESTUDIANTE"
    && regManual5.FkEnlace is null && regManual5.FkClave is null);

var regImportado5 = await servicioReg5.CrearAsync(new RegistroAsistenciaCrear { FkSesion = 11, FkAsistente = 2, Origen = "IMPORTADO" });
Afirmar("Crear() IMPORTADO también es válido", regImportado5.Origen == "IMPORTADO");

// D26: el origen se valida ANTES de llegar al repositorio.
int creacionesAntesReg5 = repoReg5.LlamadasCrear;
await AfirmarLanza<ArgumentException>(
    "Crear con origen \"ENLACE\" lanza ArgumentException (400, D26)",
    () => servicioReg5.CrearAsync(new RegistroAsistenciaCrear { FkSesion = 12, FkAsistente = 2, Origen = "ENLACE" }));
await AfirmarLanza<ArgumentException>(
    "Crear con origen \"QR\" lanza ArgumentException (400, D26)",
    () => servicioReg5.CrearAsync(new RegistroAsistenciaCrear { FkSesion = 12, FkAsistente = 2, Origen = "QR" }));
await AfirmarLanza<ArgumentException>(
    "Crear con origen en minúsculas (\"manual\") lanza ArgumentException (400)",
    () => servicioReg5.CrearAsync(new RegistroAsistenciaCrear { FkSesion = 12, FkAsistente = 2, Origen = "manual" }));
Afirmar("...y el repositorio falso NO fue invocado en ninguno de los 3 casos", repoReg5.LlamadasCrear == creacionesAntesReg5);

var controladorReg5 = new ApiCatedras.Controllers.RegistroAsistenciaController(servicioReg5);
var respRegMal5 = await controladorReg5.Crear(new RegistroAsistenciaCrear { FkSesion = 12, FkAsistente = 2, Origen = "QR" });
Afirmar("El controlador traduce ese origen inválido a HTTP 400",
    respRegMal5 is Microsoft.AspNetCore.Mvc.ObjectResult o400 && o400.StatusCode == 400);

await AfirmarLanza<InvalidOperationException>(
    "Registrar dos veces al mismo asistente en la misma sesión falla (uq_registro_sesion_asis simulado; en la API real -> 500)",
    () => servicioReg5.CrearAsync(new RegistroAsistenciaCrear { FkSesion = 10, FkAsistente = 2, Origen = "MANUAL" }));

var (datosRegA2_5, _) = await servicioReg5.ListarAsync(null, 2, null);
Afirmar("Listar(fkAsistente=2) trae los 2 registros de ese asistente", datosRegA2_5.Count == 2);
var (datosRegAmbos5, _) = await servicioReg5.ListarAsync(10, 2, null);
Afirmar("Listar(fkSesion=10, fkAsistente=2) combina los dos filtros", datosRegAmbos5.Count == 1);
await AfirmarLanza<ArgumentException>(
    "Listar(limite 0) lanza ArgumentException", () => servicioReg5.ListarAsync(null, null, 0));

var regLeido5 = await servicioReg5.ObtenerAsync(regManual5.IdRegistro);
Afirmar("Obtener() encuentra el registro creado", regLeido5.FkAsistente == 2 && regLeido5.FkSesion == 10);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Obtener(999) lanza NoEncontradoExcepcion", () => servicioReg5.ObtenerAsync(999));

Afirmar("IRepositorioRegistroAsistencia no declara Reemplazar/Actualizar/Eliminar (RF157)",
    new[] { "ReemplazarAsync", "ActualizarAsync", "EliminarAsync" }
        .All(m => typeof(IRepositorioRegistroAsistencia).GetMethod(m) is null));
Afirmar("RegistroAsistenciaCrear NO tiene fk_clave, fk_enlace ni las instantáneas (RF156)",
    new[] { "FkClave", "FkEnlace", "FkProgramaSnapshot", "FkVinculacionSnapshot" }
        .All(p => typeof(RegistroAsistenciaCrear).GetProperty(p) is null));
Afirmar("'Origen' lleva [Required] pero NO [RegularExpression] (D26: si no, daría 422 en vez de 400)",
    typeof(RegistroAsistenciaCrear).GetProperty("Origen")!
        .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), false).Length == 1
    && typeof(RegistroAsistenciaCrear).GetProperty("Origen")!
        .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RegularExpressionAttribute), false).Length == 0);

// ============================================================================
//  RETROFIT criterio 3 · Fase 4 (PLAN_RETROFIT_SP.md) — tester: Isaac
//  (a) Regla 1 del retrofit: los contratos de las 7 tablas existentes del
//      lote NO cambiaron (si alguien agrega o quita un método al pasar a
//      procedimientos, esta prueba lo delata).
//  (b) LoteMigracion + DetalleMigracion (v7, construidas en este lote):
//      servicio real + repositorios FALSOS que comparten la lista de
//      detalles (simula que sp_generar_lote_migracion los insertó en la
//      misma BD, igual que el par LoteCarga/NovedadCarga de la v3).
//  Los procedimientos en sí se prueban contra PostgreSQL real en
//  db/postgres/pruebas/prueba_sp_retrofit_fase4.sql.
// ============================================================================
Console.WriteLine("\n== Retrofit Fase 4 · contratos IRepositorio* sin cambios (regla 1) ==\n");

// Nombres de los métodos de una interfaz, ordenados y unidos: "A,B,C".
static string MetodosDe(Type interfaz) =>
    string.Join(",", interfaz.GetMethods().Select(m => m.Name).OrderBy(n => n));

Afirmar("IRepositorioAsistente sigue con sus 5 métodos",
    MetodosDe(typeof(IRepositorioAsistente)) == "ActualizarAsync,CrearAsync,EliminarAsync,ObtenerPorIdAsync,ObtenerTodosAsync");
Afirmar("IRepositorioDocumentoAsistente sigue con sus 5 métodos",
    MetodosDe(typeof(IRepositorioDocumentoAsistente)) == "ActualizarAsync,CrearAsync,EliminarAsync,ObtenerPorIdAsync,ObtenerTodosAsync");
Afirmar("IRepositorioVinculacionAsistente sigue con 3 (sin Obtener/Actualizar/Eliminar, ADR D16)",
    MetodosDe(typeof(IRepositorioVinculacionAsistente)) == "CerrarAsync,CrearAsync,ObtenerTodosAsync");
Afirmar("IRepositorioProgramaAsistente sigue con 3 (sin Obtener/Actualizar/Eliminar, ADR D16)",
    MetodosDe(typeof(IRepositorioProgramaAsistente)) == "CambiarEstadoAsync,CrearAsync,ObtenerTodosAsync");
Afirmar("IRepositorioConsentimientoDatos sigue con 3 (Ley 1581: sin Actualizar/Eliminar)",
    MetodosDe(typeof(IRepositorioConsentimientoDatos)) == "CrearAsync,ObtenerPorIdAsync,ObtenerTodosAsync");
Afirmar("IRepositorioLoteCargaAsistente sigue con 4 (abrir + cerrar, RF117)",
    MetodosDe(typeof(IRepositorioLoteCargaAsistente)) == "CerrarAsync,CrearAsync,ObtenerPorIdAsync,ObtenerTodosAsync");
Afirmar("IRepositorioNovedadCarga sigue de solo lectura (RF118)",
    MetodosDe(typeof(IRepositorioNovedadCarga)) == "ObtenerPorIdAsync,ObtenerTodosAsync");
Afirmar("ConsentimientoDatosController sigue sin PUT/PATCH/DELETE",
    !typeof(ApiCatedras.Controllers.ConsentimientoDatosController).GetMethods()
        .SelectMany(m => m.GetCustomAttributes(true))
        .Any(a => a is Microsoft.AspNetCore.Mvc.HttpPutAttribute
               || a is Microsoft.AspNetCore.Mvc.HttpPatchAttribute
               || a is Microsoft.AspNetCore.Mvc.HttpDeleteAttribute));

Console.WriteLine("\n== Retrofit Fase 4 · ServicioLoteMigracion + ServicioDetalleMigracion ==\n");

var migTablaDetalles = new List<DetalleMigracion>();
var migRepoLote = new RepositorioLoteMigracionFalso(migTablaDetalles);
var migServicioLote = new ServicioLoteMigracion(migRepoLote);
var migServicioDetalle = new ServicioDetalleMigracion(new RepositorioDetalleMigracionFalso(migTablaDetalles));

// ---- LoteMigracion ----------------------------------------------------------
var migLote = await migServicioLote.GenerarAsync(new LoteMigracionGenerar { FkSesion = 5712, FkUsuario = 7 });
Afirmar("Generar() devuelve el lote GENERADO con lo que calculó la BD (nombre, total)",
    migLote.IdLoteMigracion == 1 && migLote.FkSesion == 5712 && migLote.FkUsuario == 7
    && migLote.FkEstadoProceso == "GENERADO" && migLote.TotalFilas == 3 && migLote.NombreArchivo != "");

await AfirmarLanza<InvalidOperationException>(
    "Generar() de una sesión sin migrables deja subir el error de la BD (no lo traga)",
    () => migServicioLote.GenerarAsync(new LoteMigracionGenerar { FkSesion = RepositorioLoteMigracionFalso.SesionSinMigrables }));

await migServicioLote.GenerarAsync(new LoteMigracionGenerar { FkSesion = 9000 });
var (migLotesSesion, migTotalSesion) = await migServicioLote.ListarAsync(5712, null);
Afirmar("Listar(fkSesion) filtra por sesión", migTotalSesion == 1 && migLotesSesion[0].IdLoteMigracion == migLote.IdLoteMigracion);
var (migTodos, _) = await migServicioLote.ListarAsync(null, null);
Afirmar("Listar() sin filtro: el más reciente primero", migTodos.Count == 2 && migTodos[0].FkSesion == 9000);
await AfirmarLanza<ArgumentException>(
    "LoteMigracion: Listar(limite 0) lanza ArgumentException (400)", () => migServicioLote.ListarAsync(null, 0));
await AfirmarLanza<NoEncontradoExcepcion>(
    "LoteMigracion: Obtener(999) lanza NoEncontradoExcepcion (404)", () => migServicioLote.ObtenerAsync(999));

int migFilas = await migServicioLote.ActualizarAsync(migLote.IdLoteMigracion,
    new LoteMigracionActualizar { FkEstadoProceso = "EN_CURSO" });
var migTrasPatch = await migServicioLote.ObtenerAsync(migLote.IdLoteMigracion);
Afirmar("PATCH cambia solo el estado y deja 'observaciones' y 'total_filas' igual",
    migFilas == 1 && migTrasPatch.FkEstadoProceso == "EN_CURSO"
    && migTrasPatch.Observaciones is null && migTrasPatch.TotalFilas == 3);

int migLlamadasAntes = migRepoLote.LlamadasTotales;
await AfirmarLanza<ArgumentException>(
    "LoteMigracion: PATCH sin campos lanza ArgumentException (400)",
    () => migServicioLote.ActualizarAsync(migLote.IdLoteMigracion, new LoteMigracionActualizar()));
Afirmar("...y el repositorio NUNCA se invocó", migRepoLote.LlamadasTotales == migLlamadasAntes);
await AfirmarLanza<NoEncontradoExcepcion>(
    "LoteMigracion: PATCH a un lote inexistente lanza NoEncontradoExcepcion (404)",
    () => migServicioLote.ActualizarAsync(999, new LoteMigracionActualizar { Observaciones = "x" }));

Afirmar("IRepositorioLoteMigracion no declara EliminarAsync (RF185: es evidencia)",
    typeof(IRepositorioLoteMigracion).GetMethod("EliminarAsync") is null);
Afirmar("LoteMigracionGenerar solo expone fk_sesion y fk_usuario (el resto lo calcula la BD)",
    string.Join(",", typeof(LoteMigracionGenerar).GetProperties().Select(p => p.Name).OrderBy(n => n)) == "FkSesion,FkUsuario");

// ---- DetalleMigracion -------------------------------------------------------
var (migDetalles, migTotalDet) = await migServicioDetalle.ListarAsync(migLote.IdLoteMigracion, null);
Afirmar("Los detalles nacieron con el lote: 3, todos PENDIENTE y del lote generado",
    migTotalDet == 3 && migDetalles.All(d => d.FkLoteMigracion == migLote.IdLoteMigracion && d.Resultado == "PENDIENTE"));

long migIdDetalle = migDetalles[0].IdDetalle;
await migServicioDetalle.ActualizarAsync(migIdDetalle, new DetalleMigracionActualizar { Resultado = "RECHAZADO" });
await migServicioDetalle.ActualizarAsync(migIdDetalle, new DetalleMigracionActualizar { Mensaje = "ID no existe en ASIS" });
var migDetalle = await migServicioDetalle.ObtenerAsync(migIdDetalle);
Afirmar("Dos PATCH parciales seguidos: el segundo NO borra lo que puso el primero",
    migDetalle.Resultado == "RECHAZADO" && migDetalle.Mensaje == "ID no existe en ASIS"
    && migDetalle.IdAsisEnviado == "1000001");

await AfirmarLanza<ArgumentException>(
    "DetalleMigracion: PATCH sin campos lanza ArgumentException (400)",
    () => migServicioDetalle.ActualizarAsync(migIdDetalle, new DetalleMigracionActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "DetalleMigracion: PATCH inexistente lanza NoEncontradoExcepcion (404)",
    () => migServicioDetalle.ActualizarAsync(999, new DetalleMigracionActualizar { Resultado = "ACEPTADO" }));
await AfirmarLanza<NoEncontradoExcepcion>(
    "DetalleMigracion: Obtener(999) lanza NoEncontradoExcepcion (404)", () => migServicioDetalle.ObtenerAsync(999));
await AfirmarLanza<ArgumentException>(
    "DetalleMigracion: Listar(limite 0) lanza ArgumentException (400)", () => migServicioDetalle.ListarAsync(null, 0));

// La forma (422) la vigilan las anotaciones de la petición, no el servicio.
var migPeticionMala = new DetalleMigracionActualizar { Resultado = "QUIZAS" };
bool migValida = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
    migPeticionMala, new System.ComponentModel.DataAnnotations.ValidationContext(migPeticionMala),
    new List<System.ComponentModel.DataAnnotations.ValidationResult>(), validateAllProperties: true);
Afirmar("DetalleMigracionActualizar rechaza un 'resultado' fuera de la lista (-> 422)", !migValida);

Afirmar("IRepositorioDetalleMigracion no declara CrearAsync ni EliminarAsync (RF189)",
    typeof(IRepositorioDetalleMigracion).GetMethod("CrearAsync") is null
    && typeof(IRepositorioDetalleMigracion).GetMethod("EliminarAsync") is null);
Afirmar("DetalleMigracionActualizar no expone las columnas *_enviado(a) (copia literal)",
    new[] { "IdAsisEnviado", "ProgramaEnviado", "ReunionEnviada", "FkRegistro", "FkLoteMigracion" }
        .All(p => typeof(DetalleMigracionActualizar).GetProperty(p) is null));

// ============================================================================
//  VERSIÓN 7 · Fase 58 (version7/8_tasks.md) — la prueba de capas llega a las
//  37 entidades. Mismo patrón de siempre: servicio REAL + repositorio FALSO.
//  (a) Parametro: CRUD con llave de texto; 'modificado_en' lo pone el SP
//      (el falso lo simula), nunca el servicio.
//  (b) Bitacora: solo lectura (ADR D36, D37-A), vigilado por reflexión.
//  (c) LoteMigracion/DetalleMigracion: verbos que NO deben existir y el
//      servicio que no toca Sesion (RF184).
//  (d) Las 4 entidades de la v6 que la prueba de capas no cubría aún:
//      Pregunta, OpcionPregunta, RespuestaEncuesta y RespuestaItem.
// ============================================================================

// Valida una petición con sus anotaciones, como lo hace [ApiController]
// antes de llegar al controlador (falso = la API respondería 422).
static bool EsValida(object peticion) =>
    System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
        peticion, new System.ComponentModel.DataAnnotations.ValidationContext(peticion),
        new List<System.ComponentModel.DataAnnotations.ValidationResult>(), validateAllProperties: true);

// Verbos HTTP que declara un controlador (atributos [HttpGet], [HttpPost]...).
static HashSet<string> VerbosDe(Type controlador) =>
    controlador.GetMethods()
        .SelectMany(m => m.GetCustomAttributes(true))
        .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
        .SelectMany(a => a.HttpMethods)
        .ToHashSet();

// ---- (a) Parametro --------------------------------------------------------
Console.WriteLine("\n== v7 · Prueba de capas: ServicioParametro + repositorio falso en memoria ==\n");

var parRepo = new RepositorioParametroFalso();
var parServicio = new ServicioParametro(parRepo);

var (parLista, parTotal) = await parServicio.ListarAsync(null);
Afirmar("Parametro: Listar() devuelve las semillas ordenadas por clave",
    parTotal == 2 && parLista[0].Clave == "CLAVE_LONGITUD" && parLista[1].Clave == "URL_BASE");

await parServicio.CrearAsync(new ParametroCrear
{
    Clave = "PRUEBA_V7", Valor = "1", Descripcion = "Prueba de capas v7",
});
var parCreado = await parServicio.ObtenerAsync("PRUEBA_V7");
Afirmar("Parametro: Crear() sin tipo_dato usa el DEFAULT 'TEXTO'",
    parCreado.TipoDato == "TEXTO" && parCreado.Valor == "1");

await AfirmarLanza<InvalidOperationException>(
    "Parametro: Crear() con clave repetida deja subir el error de la BD (pk_parametro -> 500)",
    () => parServicio.CrearAsync(new ParametroCrear { Clave = "PRUEBA_V7", Valor = "2", Descripcion = "x" }));
await AfirmarLanza<NoEncontradoExcepcion>(
    "Parametro: Obtener('NO_EXISTE') lanza NoEncontradoExcepcion (404)",
    () => parServicio.ObtenerAsync("NO_EXISTE"));

int parLlamadasAntes = parRepo.LlamadasTotales;
await AfirmarLanza<ArgumentException>(
    "Parametro: PATCH sin campos lanza ArgumentException (400)",
    () => parServicio.ActualizarAsync("PRUEBA_V7", new ParametroActualizar()));
Afirmar("...y el repositorio NUNCA se invocó", parRepo.LlamadasTotales == parLlamadasAntes);

DateTime parAntes = parCreado.ModificadoEn;
await parServicio.ActualizarAsync("PRUEBA_V7", new ParametroActualizar { Valor = "2" });
var parTrasPatch = await parServicio.ObtenerAsync("PRUEBA_V7");
Afirmar("Parametro: PATCH cambia 'valor', conserva 'descripcion' y 'modificado_en' avanza (lo pone el SP)",
    parTrasPatch.Valor == "2" && parTrasPatch.Descripcion == "Prueba de capas v7"
    && parTrasPatch.ModificadoEn > parAntes);

await parServicio.ReemplazarAsync("PRUEBA_V7", new ParametroReemplazo
{
    Valor = "3", TipoDato = "ENTERO", Descripcion = "Reemplazada",
});
var parTrasPut = await parServicio.ObtenerAsync("PRUEBA_V7");
Afirmar("Parametro: PUT reemplaza todo; 'fk_usuario_modifica' ausente queda NULL",
    parTrasPut.Valor == "3" && parTrasPut.TipoDato == "ENTERO" && parTrasPut.FkUsuarioModifica is null);

await AfirmarLanza<NoEncontradoExcepcion>(
    "Parametro: PUT a una clave inexistente lanza NoEncontradoExcepcion (404)",
    () => parServicio.ReemplazarAsync("NO_EXISTE", new ParametroReemplazo { Valor = "1", TipoDato = "TEXTO", Descripcion = "x" }));
await AfirmarLanza<NoEncontradoExcepcion>(
    "Parametro: PATCH a una clave inexistente lanza NoEncontradoExcepcion (404)",
    () => parServicio.ActualizarAsync("NO_EXISTE", new ParametroActualizar { Valor = "1" }));
await AfirmarLanza<ArgumentException>(
    "Parametro: Listar(limite 0) lanza ArgumentException (400)", () => parServicio.ListarAsync(0));

int parEliminadas = await parServicio.EliminarAsync("PRUEBA_V7");
Afirmar("Parametro: Eliminar() borra la fila (DELETE físico, sin FK entrantes)", parEliminadas == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Parametro: un segundo Eliminar() lanza NoEncontradoExcepcion (404)",
    () => parServicio.EliminarAsync("PRUEBA_V7"));

// Forma de las peticiones (422), D37-D.
Afirmar("ParametroCrear rechaza una clave en minúsculas ('prueba' -> 422)",
    !EsValida(new ParametroCrear { Clave = "prueba", Valor = "1", Descripcion = "x" }));
Afirmar("ParametroCrear rechaza una clave que empieza por dígito ('1CLAVE' -> 422)",
    !EsValida(new ParametroCrear { Clave = "1CLAVE", Valor = "1", Descripcion = "x" }));
Afirmar("ParametroCrear acepta 'VENTANA_MINUTOS_ANTES' sin tipo_dato (opcional)",
    EsValida(new ParametroCrear { Clave = "VENTANA_MINUTOS_ANTES", Valor = "30", Descripcion = "x" }));
Afirmar("ParametroCrear acepta 'valor' vacío (la columna lo permite)",
    EsValida(new ParametroCrear { Clave = "URL_VACIA", Valor = "", Descripcion = "x" }));
Afirmar("ParametroCrear rechaza un tipo_dato fuera de la lista ('OTRO' -> 422)",
    !EsValida(new ParametroCrear { Clave = "X", Valor = "1", Descripcion = "x", TipoDato = "OTRO" }));
Afirmar("ParametroReemplazo exige tipo_dato (PUT = reemplazo completo -> 422)",
    !EsValida(new ParametroReemplazo { Valor = "1", Descripcion = "x" }));
Afirmar("La clave nunca va en el body de PUT/PATCH (es la llave de la ruta)",
    typeof(ParametroReemplazo).GetProperty("Clave") is null && typeof(ParametroActualizar).GetProperty("Clave") is null);
Afirmar("Ninguna petición de Parametro deja mandar 'modificado_en' (lo pone el SP, RF197)",
    new[] { typeof(ParametroCrear), typeof(ParametroReemplazo), typeof(ParametroActualizar) }
        .All(t => t.GetProperty("ModificadoEn") is null));

// ---- (b) Bitacora -----------------------------------------------------------
Console.WriteLine("\n== v7 · Prueba de capas: ServicioBitacora (solo lectura) + repositorio falso ==\n");

var bitServicio = new ServicioBitacora(new RepositorioBitacoraFalso());

var (bitTodas, bitTotal) = await bitServicio.ListarAsync(null, null);
Afirmar("Bitacora: Listar() devuelve las 3 filas, la más reciente primero",
    bitTotal == 3 && bitTodas[0].IdBitacora == 3 && bitTodas[2].IdBitacora == 1);
var (bitAsistente, bitTotalAsistente) = await bitServicio.ListarAsync("asistente", null);
Afirmar("Bitacora: Listar(nombreTabla 'asistente') filtra (fila del borrado lógico)",
    bitTotalAsistente == 1 && bitAsistente[0].Operacion == "UPDATE");
var (_, bitTotalBlanco) = await bitServicio.ListarAsync("   ", null);
Afirmar("Bitacora: un filtro en blanco se trata como 'sin filtro'", bitTotalBlanco == 3);
var (_, bitTotalLimite) = await bitServicio.ListarAsync(null, 2);
Afirmar("Bitacora: Listar(limite 2) respeta el límite", bitTotalLimite == 2);
await AfirmarLanza<ArgumentException>(
    "Bitacora: Listar(limite 0) lanza ArgumentException (400)", () => bitServicio.ListarAsync(null, 0));
await AfirmarLanza<NoEncontradoExcepcion>(
    "Bitacora: Obtener(999) lanza NoEncontradoExcepcion (404)", () => bitServicio.ObtenerAsync(999));
var bitUna = await bitServicio.ObtenerAsync(1);
Afirmar("Bitacora: Obtener(1) devuelve la fila con su JSON 'datos_despues'",
    bitUna.NombreTabla == "registro_asistencia" && bitUna.DatosDespues?.GetProperty("id_registro").GetInt64() == 1);

// D36 / D37-A: si alguien vuelve a abrir la escritura, estas pruebas fallan.
Afirmar("IRepositorioBitacora solo declara lectura (sin Crear/Actualizar/Eliminar)",
    MetodosDe(typeof(IRepositorioBitacora)) == "ObtenerPorIdAsync,ObtenerTodosAsync");
Afirmar("IServicioBitacora solo declara lectura",
    MetodosDe(typeof(IServicioBitacora)) == "ListarAsync,ObtenerAsync");
Afirmar("BitacoraController solo tiene [HttpGet] (POST/PUT/PATCH/DELETE -> 405)",
    VerbosDe(typeof(ApiCatedras.Controllers.BitacoraController)).SetEquals(new[] { "GET" }));
Afirmar("No quedan peticiones de escritura de Bitacora (BitacoraCrear/Reemplazo/Actualizar)",
    new[] { "BitacoraCrear", "BitacoraReemplazo", "BitacoraActualizar" }
        .All(n => typeof(ParametroCrear).Assembly.GetType("ApiCatedras.Peticiones." + n) is null));

// ---- (c) LoteMigracion / DetalleMigracion: lo que NO debe existir --------------
Console.WriteLine("\n== v7 · Verbos de LoteMigracion/DetalleMigracion y RF184 ==\n");

Afirmar("ServicioLoteMigracion solo recibe IRepositorioLoteMigracion (no puede tocar Sesion, RF184)",
    typeof(ServicioLoteMigracion).GetConstructors().Single().GetParameters()
        .Select(p => p.ParameterType).SequenceEqual(new[] { typeof(IRepositorioLoteMigracion) }));
Afirmar("LoteMigracionController: solo GET, POST (generar) y PATCH (RF183: sin PUT ni DELETE)",
    VerbosDe(typeof(ApiCatedras.Controllers.LoteMigracionController)).SetEquals(new[] { "GET", "POST", "PATCH" }));
Afirmar("DetalleMigracionController: solo GET y PATCH (RF189: sin POST, PUT ni DELETE)",
    VerbosDe(typeof(ApiCatedras.Controllers.DetalleMigracionController)).SetEquals(new[] { "GET", "PATCH" }));
Afirmar("ParametroController: los 5 verbos del CRUD completo",
    VerbosDe(typeof(ApiCatedras.Controllers.ParametroController)).SetEquals(new[] { "GET", "POST", "PUT", "PATCH", "DELETE" }));
Afirmar("LoteMigracionActualizar no expone 'instancia_proceso_asis' (D37-F)",
    typeof(LoteMigracionActualizar).GetProperty("InstanciaProcesoAsis") is null);
Afirmar("LoteMigracionGenerar exige fk_sesion (sin él -> 422)",
    !EsValida(new LoteMigracionGenerar()) && EsValida(new LoteMigracionGenerar { FkSesion = 5712 }));

// ---- (d) v6: Pregunta -------------------------------------------------------
Console.WriteLine("\n== v6 · Prueba de capas: ServicioPregunta + repositorio falso en memoria ==\n");

var preRepo = new RepositorioPreguntaFalso();
var preServicio = new ServicioPregunta(preRepo);

long preId = await preServicio.CrearAsync(new PreguntaCrear
{
    FkEncuesta = 1, Enunciado = "¿Qué tan útil fue la sesión?", Orden = 1, FkTipoPregunta = "ESCALA",
    ValorMin = 1, ValorMax = 5,
    Opciones = [new OpcionInicial { Etiqueta = "Poco", Valor = 1, Orden = 1 },
                new OpcionInicial { Etiqueta = "Mucho", Valor = 5, Orden = 2 }],
});
var preCreada = await preServicio.ObtenerAsync(preId);
Afirmar("Pregunta: Crear() guarda la pregunta con 'obligatoria' false por defecto",
    preCreada.Enunciado.StartsWith("¿Qué") && preCreada.Obligatoria == false && preCreada.ValorMax == 5);
Afirmar("Pregunta: las opciones iniciales viajan en la MISMA llamada (SP maestro-detalle, Regla 1)",
    preRepo.UltimasOpciones.Count == 2 && preRepo.UltimasOpciones.All(o => o.FkPregunta == 0));

int preLlamadas = preRepo.LlamadasTotales;
await AfirmarLanza<ArgumentException>(
    "Pregunta: valor_min sin valor_max lanza ArgumentException (400)",
    () => preServicio.CrearAsync(new PreguntaCrear { FkEncuesta = 1, Enunciado = "x", Orden = 2, FkTipoPregunta = "ESCALA", ValorMin = 1 }));
await AfirmarLanza<ArgumentException>(
    "Pregunta: valor_max <= valor_min lanza ArgumentException (400)",
    () => preServicio.CrearAsync(new PreguntaCrear { FkEncuesta = 1, Enunciado = "x", Orden = 2, FkTipoPregunta = "ESCALA", ValorMin = 5, ValorMax = 5 }));
Afirmar("...y el repositorio NUNCA se invocó", preRepo.LlamadasTotales == preLlamadas);

await AfirmarLanza<ArgumentException>(
    "Pregunta: PATCH solo de valor_max, menor que el valor_min guardado -> 400 (no 500 del CHECK)",
    () => preServicio.ActualizarAsync(preId, new PreguntaActualizar { ValorMax = 1 }));
await preServicio.ActualizarAsync(preId, new PreguntaActualizar { Enunciado = "Nuevo enunciado" });
var prePatch = await preServicio.ObtenerAsync(preId);
Afirmar("Pregunta: PATCH parcial cambia el enunciado y conserva el rango",
    prePatch.Enunciado == "Nuevo enunciado" && prePatch.ValorMin == 1 && prePatch.ValorMax == 5);
await AfirmarLanza<ArgumentException>(
    "Pregunta: PATCH sin campos lanza ArgumentException (400)",
    () => preServicio.ActualizarAsync(preId, new PreguntaActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "Pregunta: PUT a una pregunta inexistente lanza NoEncontradoExcepcion (404)",
    () => preServicio.ReemplazarAsync(999, new PreguntaReemplazo { Enunciado = "x", Orden = 1, FkTipoPregunta = "TEXTO", Obligatoria = false }));
await preServicio.CrearAsync(new PreguntaCrear { FkEncuesta = 2, Enunciado = "Otra encuesta", Orden = 1, FkTipoPregunta = "TEXTO" });
var (_, preTotalEnc1) = await preServicio.ListarAsync(1, null);
Afirmar("Pregunta: Listar(fkEncuesta) filtra por encuesta", preTotalEnc1 == 1);
Afirmar("Pregunta: Eliminar() devuelve 1", await preServicio.EliminarAsync(preId) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Pregunta: Obtener() de la eliminada lanza NoEncontradoExcepcion (404)", () => preServicio.ObtenerAsync(preId));

// ---- (d) v6: OpcionPregunta ---------------------------------------------------
Console.WriteLine("\n== v6 · Prueba de capas: ServicioOpcionPregunta + repositorio falso en memoria ==\n");

var opcServicio = new ServicioOpcionPregunta(new RepositorioOpcionPreguntaFalso());
long opcId = await opcServicio.CrearAsync(new OpcionPreguntaCrear { FkPregunta = 10, Etiqueta = "Sí", Valor = 1, Orden = 1 });
await opcServicio.CrearAsync(new OpcionPreguntaCrear { FkPregunta = 11, Etiqueta = "Otra", Orden = 1 });
var (_, opcTotal10) = await opcServicio.ListarAsync(10, null);
Afirmar("OpcionPregunta: Listar(fkPregunta) filtra por pregunta", opcTotal10 == 1);
await opcServicio.ActualizarAsync(opcId, new OpcionPreguntaActualizar { Orden = 3 });
var opcPatch = await opcServicio.ObtenerAsync(opcId);
Afirmar("OpcionPregunta: PATCH parcial cambia el orden y conserva la etiqueta",
    opcPatch.Orden == 3 && opcPatch.Etiqueta == "Sí" && opcPatch.Valor == 1);
await opcServicio.ReemplazarAsync(opcId, new OpcionPreguntaReemplazo { Etiqueta = "No", Orden = 2 });
var opcPut = await opcServicio.ObtenerAsync(opcId);
Afirmar("OpcionPregunta: PUT reemplaza todo ('valor' ausente queda NULL)",
    opcPut.Etiqueta == "No" && opcPut.Valor is null && opcPut.Orden == 2);
await AfirmarLanza<ArgumentException>(
    "OpcionPregunta: PATCH sin campos lanza ArgumentException (400)",
    () => opcServicio.ActualizarAsync(opcId, new OpcionPreguntaActualizar()));
await AfirmarLanza<NoEncontradoExcepcion>(
    "OpcionPregunta: Obtener(999) lanza NoEncontradoExcepcion (404)", () => opcServicio.ObtenerAsync(999));
Afirmar("OpcionPregunta: Eliminar() devuelve 1", await opcServicio.EliminarAsync(opcId) == 1);
await AfirmarLanza<NoEncontradoExcepcion>(
    "OpcionPregunta: un segundo Eliminar() lanza NoEncontradoExcepcion (404)", () => opcServicio.EliminarAsync(opcId));

// ---- (d) v6: RespuestaEncuesta y RespuestaItem --------------------------------
Console.WriteLine("\n== v6 · Prueba de capas: ServicioRespuestaEncuesta + ServicioRespuestaItem ==\n");

var resTablaItems = new List<RespuestaItem>();
var resServicio = new ServicioRespuestaEncuesta(new RepositorioRespuestaEncuestaFalso(resTablaItems));
var itemServicio = new ServicioRespuestaItem(new RepositorioRespuestaItemFalso(resTablaItems));

long resId = await resServicio.ResponderAsync(new RespuestaEncuestaResponder
{
    FkRegistro = 169,
    Respuestas = [new ItemRespuesta { Pregunta = 1, Numero = 5 },
                  new ItemRespuesta { Pregunta = 2, Texto = "Muy buena" }],
});
var resCreada = await resServicio.ObtenerAsync(resId);
Afirmar("RespuestaEncuesta: Responder() crea la respuesta del registro, completa",
    resCreada.FkRegistro == 169 && resCreada.Completa);
var (resItems, resTotalItems) = await itemServicio.ListarAsync(resId, null);
Afirmar("RespuestaItem: los ítems nacieron DENTRO de responder (2, del mismo maestro)",
    resTotalItems == 2 && resItems.All(i => i.FkRespuesta == resId) && resItems.Any(i => i.ValorTexto == "Muy buena"));
await AfirmarLanza<NoEncontradoExcepcion>(
    "RespuestaEncuesta: Obtener(999) lanza NoEncontradoExcepcion (404)", () => resServicio.ObtenerAsync(999));
await AfirmarLanza<ArgumentException>(
    "RespuestaEncuesta: Listar(limite 0) lanza ArgumentException (400)", () => resServicio.ListarAsync(null, 0));
await AfirmarLanza<NoEncontradoExcepcion>(
    "RespuestaItem: Obtener(999) lanza NoEncontradoExcepcion (404)", () => itemServicio.ObtenerAsync(999));
await AfirmarLanza<ArgumentException>(
    "RespuestaItem: Listar(limite 0) lanza ArgumentException (400)", () => itemServicio.ListarAsync(null, 0));
Afirmar("RespuestaEncuestaResponder rechaza una lista de respuestas vacía (-> 422)",
    !EsValida(new RespuestaEncuestaResponder { FkRegistro = 169, Respuestas = [] }));
Afirmar("IRepositorioRespuestaEncuesta: lectura + responder, sin editar ni borrar (D32)",
    MetodosDe(typeof(IRepositorioRespuestaEncuesta)) == "ObtenerPorIdAsync,ObtenerTodosAsync,ResponderAsync");
Afirmar("IRepositorioRespuestaItem es de solo lectura (nace dentro de responder)",
    MetodosDe(typeof(IRepositorioRespuestaItem)) == "ObtenerPorIdAsync,ObtenerTodosAsync");

// ---- Maestro-detalle en el formulario (Guía IA, Reglas 1 y 4) ------------------
//  El maestro y sus detalles llegan en UNA petición y el servicio los entrega
//  JUNTOS al repositorio (un solo CALL: migraciones 042-044). Lo que se
//  comprueba aquí es la parte del SERVICIO: que arma los detalles, que valida
//  sus reglas ANTES de tocar el repositorio y que no inventa los ids.
//  (El todo-o-nada del motor lo prueba
//   db/postgres/pruebas/prueba_sp_maestro_detalle_formulario.sql.)
Console.WriteLine("\n== Maestro-detalle en el formulario: cátedra, encuesta y sesión ==\n");

var mdRepoCat = new RepositorioCatedraFalso();
var mdServCat = new ServicioCatedra(mdRepoCat);
var mdInicio = new DateTimeOffset(2026, 11, 3, 14, 0, 0, TimeSpan.FromHours(-5));
SesionInicial MdSesion(DateTimeOffset inicio, DateTimeOffset fin) => new()
{ Inicio = inicio, Fin = fin, FkModalidad = "PRESENCIAL", FkSede = "SAN_BENITO", FkPeriodo = "2026-2" };

var (mdIdCat, _) = await mdServCat.CrearAsync(new CatedraCrear
{
    IdEventoAsis = "000999101", Nombre = "Con sesiones", FkTipoEvento = "CAAB",
    Sesiones = [MdSesion(mdInicio, mdInicio.AddHours(2)), MdSesion(mdInicio.AddDays(7), mdInicio.AddDays(7).AddHours(2))],
});
Afirmar("Cátedra: las 2 sesiones viajan en la MISMA llamada que la cátedra (Regla 1)",
    mdIdCat > 0 && mdRepoCat.UltimasSesiones.Count == 2);
Afirmar("Cátedra: cada sesión va con FkCatedra 0 (lo fija el SP) y NumeroReunion 0 (lo fija el trigger)",
    mdRepoCat.UltimasSesiones.All(s => s.FkCatedra == 0 && s.NumeroReunion == 0));
Afirmar("Cátedra: las horas de las sesiones se pasan a UTC (14:00 -05:00 -> 19:00 UTC) y estado PROGRAMADA",
    mdRepoCat.UltimasSesiones[0].Inicio == new DateTime(2026, 11, 3, 19, 0, 0, DateTimeKind.Utc)
    && mdRepoCat.UltimasSesiones.All(s => s.Estado == "PROGRAMADA"));

await mdServCat.CrearAsync(new CatedraCrear { IdEventoAsis = "000999102", Nombre = "Sin sesiones", FkTipoEvento = "CAAB" });
Afirmar("Cátedra: sin 'sesiones' el repositorio recibe una lista vacía (no null)",
    mdRepoCat.UltimasSesiones.Count == 0);

int mdLlamadasCat = mdRepoCat.LlamadasCrear;
await AfirmarLanza<ArgumentException>(
    "Cátedra: una sesión con fin <= inicio -> ArgumentException (400, misma regla RF125 de Sesion)",
    () => mdServCat.CrearAsync(new CatedraCrear
    {
        IdEventoAsis = "000999103", Nombre = "No debe quedar", FkTipoEvento = "CAAB",
        Sesiones = [MdSesion(mdInicio, mdInicio.AddHours(2)), MdSesion(mdInicio, mdInicio.AddHours(-1))],
    }));
Afirmar("...y el repositorio NUNCA se invocó (no queda ni la cátedra)", mdRepoCat.LlamadasCrear == mdLlamadasCat);

var mdRepoEnc = new RepositorioEncuestaFalso();
var mdServEnc = new ServicioEncuesta(mdRepoEnc);
long mdIdEnc = await mdServEnc.CrearAsync(new EncuestaCrear
{
    Nombre = "Con preguntas",
    Preguntas =
    [
        new PreguntaInicial { Enunciado = "¿Útil?", Orden = 1, FkTipoPregunta = "ESCALA_5", ValorMin = 1, ValorMax = 5, Obligatoria = true },
        new PreguntaInicial
        {
            Enunciado = "¿Cómo se enteró?", Orden = 2, FkTipoPregunta = "OPCION_UNICA",
            Opciones = [new OpcionInicial { Etiqueta = "Correo", Orden = 1 }, new OpcionInicial { Etiqueta = "Cartelera", Orden = 2 }],
        },
    ],
});
Afirmar("Encuesta: las 2 preguntas viajan en la MISMA llamada que la encuesta (Regla 1)",
    mdIdEnc > 0 && mdRepoEnc.UltimasPreguntas.Count == 2);
Afirmar("Encuesta: segundo nivel — la pregunta de opción única lleva sus 2 opciones",
    mdRepoEnc.UltimasPreguntas[1].Opciones.Count == 2 && mdRepoEnc.UltimasPreguntas[0].Opciones.Count == 0);
Afirmar("Encuesta: FkEncuesta y FkPregunta van en 0 (los fija el SP); obligatoria por defecto false",
    mdRepoEnc.UltimasPreguntas.All(p => p.Pregunta.FkEncuesta == 0 && p.Opciones.All(o => o.FkPregunta == 0))
    && mdRepoEnc.UltimasPreguntas[0].Pregunta.Obligatoria && !mdRepoEnc.UltimasPreguntas[1].Pregunta.Obligatoria);

int mdLlamadasEnc = mdRepoEnc.LlamadasCrear;
await AfirmarLanza<ArgumentException>(
    "Encuesta: una pregunta con valor_min sin valor_max -> ArgumentException (misma regla de Pregunta)",
    () => mdServEnc.CrearAsync(new EncuestaCrear
    {
        Nombre = "No debe quedar",
        Preguntas = [new PreguntaInicial { Enunciado = "x", Orden = 1, FkTipoPregunta = "ESCALA_5", ValorMin = 1 }],
    }));
Afirmar("...y el repositorio NUNCA se invocó (no queda ni la encuesta)", mdRepoEnc.LlamadasCrear == mdLlamadasEnc);

var mdRepoSes = new RepositorioSesionFalso();
var mdServSes = new ServicioSesion(mdRepoSes);
SesionCrear MdSesionCrear(EnlaceInicial? enlace) => new()
{
    FkCatedra = 5498, Inicio = mdInicio, Fin = mdInicio.AddHours(2),
    FkModalidad = "PRESENCIAL", FkSede = "SAN_BENITO", FkPeriodo = "2026-2", Enlace = enlace,
};
await mdServSes.CrearAsync(MdSesionCrear(new EnlaceInicial { MinutosAntes = 15, MinutosDespues = 45, FkUsuario = 1 }));
Afirmar("Sesión: el enlace pedido viaja en la MISMA llamada que la sesión (Regla 1)",
    mdRepoSes.UltimoEnlace.HasValue && mdRepoSes.UltimoEnlace.Value.MinutosAntes == 15
    && mdRepoSes.UltimoEnlace.Value.MinutosDespues == 45 && mdRepoSes.UltimoEnlace.Value.FkUsuario == 1);
await mdServSes.CrearAsync(MdSesionCrear(null));
Afirmar("Sesión: sin 'enlace' el repositorio recibe null (la sesión nace sin enlace, como antes)",
    !mdRepoSes.UltimoEnlace.HasValue);

Afirmar("Las peticiones de detalle NO declaran la FK del maestro (la fija el SP)",
    typeof(SesionInicial).GetProperty("FkCatedra") is null
    && typeof(PreguntaInicial).GetProperty("FkEncuesta") is null
    && typeof(EnlaceInicial).GetProperty("FkSesion") is null);
Afirmar("SesionInicial con fk_sede vacía no es válida (-> 422 antes de llegar al servicio)",
    !EsValida(new SesionInicial { Inicio = mdInicio, Fin = mdInicio.AddHours(1), FkModalidad = "PRESENCIAL", FkPeriodo = "2026-2" }));
Afirmar("EnlaceInicial con minutos fuera de 0..1440 no es válido (-> 422)",
    !EsValida(new EnlaceInicial { MinutosAntes = 2000 }));

// ---------------------------------------------------------------------------
//  v7.1 · D52 · Disponibilidad de horario (solo lectura)
// ---------------------------------------------------------------------------
Console.WriteLine("\n== v7.1 · Prueba de capas: disponibilidad de horario ==\n");
var repoDisp71 = new RepositorioDisponibilidadFalso();
var servicioDisp71 = new ServicioDisponibilidad(repoDisp71);
var dispIni = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.FromHours(-5));

await AfirmarLanza<ArgumentException>(
    "Conflictos sin 'fin' lanza ArgumentException (400)",
    () => servicioDisp71.ConflictosAsync(dispIni, null, "SAN_BENITO", null, null, null));
await AfirmarLanza<ArgumentException>(
    "Conflictos con fin <= inicio lanza ArgumentException (400)",
    () => servicioDisp71.ConflictosAsync(dispIni, dispIni, "SAN_BENITO", null, null, null));
await AfirmarLanza<ArgumentException>(
    "Conflictos con un rango de más de 24 h lanza ArgumentException (400)",
    () => servicioDisp71.ConflictosAsync(dispIni, dispIni.AddHours(25), "SAN_BENITO", null, null, null));
Afirmar("...y ninguna de esas consultas llegó a la BD", repoDisp71.Consultas == 0);

var hayChoque71 = await servicioDisp71.ConflictosAsync(dispIni, dispIni.AddHours(2), "  SAN_BENITO ", "  ", 7, null);
Afirmar("Conflictos devuelve lo que decide la BD (aquí 1 choque por LUGAR y PONENTE)",
    hayChoque71.Count == 1 && hayChoque71[0].Motivos!.Contains("LUGAR") && hayChoque71[0].Motivos!.Contains("PONENTE"));
Afirmar("...la sede llega sin espacios y un lugar en blanco llega como null (la BD lo trata como «sin lugar»)",
    repoDisp71.UltimaSede == "SAN_BENITO" && repoDisp71.UltimoLugar is null && repoDisp71.UltimoAsistente == 7);
var libre71 = await servicioDisp71.ConflictosAsync(dispIni.AddDays(3), dispIni.AddDays(3).AddHours(2), "SAN_BENITO", null, 7, null);
Afirmar("Un horario sin sesiones que choquen devuelve lista vacía (disponible)", libre71.Count == 0);

await AfirmarLanza<ArgumentException>(
    "Agenda sin 'desde' lanza ArgumentException (400)",
    () => servicioDisp71.AgendaAsync(null, dispIni.AddDays(1), null, null));
await AfirmarLanza<ArgumentException>(
    "Agenda con un rango de más de 31 días lanza ArgumentException (400)",
    () => servicioDisp71.AgendaAsync(dispIni, dispIni.AddDays(40), null, null));
await AfirmarLanza<ArgumentException>(
    "Agenda con limite 0 lanza ArgumentException (400)",
    () => servicioDisp71.AgendaAsync(dispIni, dispIni.AddDays(1), null, 0));
var agenda71 = await servicioDisp71.AgendaAsync(dispIni.Date, dispIni.AddDays(1), null, null);
Afirmar("Agenda de un día devuelve las sesiones de la BD con el límite por defecto (500)",
    agenda71.Count == 1 && repoDisp71.UltimoLimite == 500);

// ---------------------------------------------------------------------------
//  v7.1 · D53 · QR de cierre de una sesión
// ---------------------------------------------------------------------------
Console.WriteLine("\n== v7.1 · Prueba de capas: QR de cierre de sesión ==\n");
var repoQr71 = new RepositorioQrSesionFalso();
var servicioQr71 = new ServicioQrSesion(repoQr71);

var qrNuevo71 = await servicioQr71.EmitirAsync(new QrSesionEmitir { FkSesion = 7, FkUsuario = 3 });
Afirmar("Emitir el QR de una sesión lo crea: ya_existia = false (y ya no exige inscripción)",
    !qrNuevo71.YaExistia && qrNuevo71.Token != Guid.Empty && qrNuevo71.FkSesion == 7);
Afirmar("...la ventana se abre ANTES de que termine la sesión y se cierra cuando termina (fin = 18:00, abre 17:45)",
    qrNuevo71.VentanaHasta == RepositorioQrSesionFalso.FinSesion && qrNuevo71.VentanaDesde == RepositorioQrSesionFalso.FinSesion.AddMinutes(-15));
Afirmar("...y quedó registrado quién lo emitió", repoQr71.UltimoUsuario == 3);
var qrMismo71 = await servicioQr71.EmitirAsync(new QrSesionEmitir { FkSesion = 7 });
Afirmar("Emitir otra vez es idempotente: devuelve el MISMO QR (ya_existia = true)", qrMismo71.YaExistia && qrMismo71.Token == qrNuevo71.Token);
var qrOtro71 = await servicioQr71.EmitirAsync(new QrSesionEmitir { FkSesion = 7, Regenerar = true });
Afirmar("Regenerar revoca el anterior y crea otro token distinto", !qrOtro71.YaExistia && qrOtro71.Token != qrNuevo71.Token && repoQr71.Revocados == 1);
Afirmar("Consultar devuelve el QR vigente (el nuevo)", (await servicioQr71.ConsultarAsync(7))!.Token == qrOtro71.Token);
Afirmar("Consultar una sesión sin QR devuelve null (204)", await servicioQr71.ConsultarAsync(8) is null);
await AfirmarLanza<NoEncontradoExcepcion>(
    "Emitir el QR de una sesión inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioQr71.EmitirAsync(new QrSesionEmitir { FkSesion = 999 }));
await AfirmarLanza<ArgumentException>(
    "Consultar sin 'fkSesion' lanza ArgumentException (400)",
    () => servicioQr71.ConsultarAsync(null));
await AfirmarLanza<ArgumentException>(
    "La asistencia sin 'fkSesion' lanza ArgumentException (400)",
    () => servicioQr71.AsistenciaAsync(0));
var asis71 = await servicioQr71.AsistenciaAsync(7);
Afirmar("La asistencia lista a quien registró (con hora y ubicación), sin lista de inscritos (D55)",
    asis71.Count == 2 && asis71.All(a => a.RegistradoEn is not null) && asis71[0].Latitud is not null);
Afirmar("QrSesionEmitir exige fk_sesion", !EsValida(new QrSesionEmitir()) && EsValida(new QrSesionEmitir { FkSesion = 7 }));

// ---------------------------------------------------------------------------
//  v7.1 · D55 · Solo el ADMINISTRADOR inicia sesión; cambio obligatorio de clave a quien se le marque
// ---------------------------------------------------------------------------
Console.WriteLine("\n== v7.1 · Prueba de capas: solo el administrador inicia sesión ==\n");
var repoUsr55 = new RepositorioUsuarioFalso();
var repoRoles55 = new RepositorioRolPorUsuarioFalso();
var servicioUsr55 = new ServicioUsuario(repoUsr55, repoRoles55, new RepositorioCodigoRecuperacionFalso());

var admin55 = await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "admin", Clave = "Admin12345" });
Afirmar("El administrador (usuario con el rol ADMIN vigente) inicia sesión", admin55 is not null && admin55.Value.Roles.Contains("ADMIN"));

var estu55 = await servicioUsr55.CrearAsync(new UsuarioCrear { FkAsistente = 70, Usuario = "estudiante.uno", Clave = "Clave-Estudiante-1" });
int accesosAntes55 = repoUsr55.LlamadasUltimoAcceso;
Afirmar("Una cuenta con la clave correcta pero SIN ningún rol NO inicia sesión (null, el mismo rechazo de siempre, D27)",
    await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "estudiante.uno", Clave = "Clave-Estudiante-1" }) is null);
await repoRoles55.CrearAsync(new RolPorUsuario { FkUsuario = estu55.IdUsuario, FkRol = "ESTUDIANTE", FechaIni = new DateOnly(2026, 1, 1) });
Afirmar("...con el rol ESTUDIANTE tampoco", await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "estudiante.uno", Clave = "Clave-Estudiante-1" }) is null);
await repoRoles55.CrearAsync(new RolPorUsuario { FkUsuario = estu55.IdUsuario, FkRol = "PONENTE", FechaIni = new DateOnly(2026, 1, 1) });
Afirmar("...ni con ESTUDIANTE y PONENTE juntos", await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "estudiante.uno", Clave = "Clave-Estudiante-1" }) is null);
Afirmar("...y a quien no puede entrar NO se le anota acceso", repoUsr55.LlamadasUltimoAcceso == accesosAntes55);
await repoRoles55.CrearAsync(new RolPorUsuario { FkUsuario = estu55.IdUsuario, FkRol = "ADMIN", FechaIni = new DateOnly(2026, 1, 1) });
var ahoraAdmin55 = await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "estudiante.uno", Clave = "Clave-Estudiante-1" });
Afirmar("Cuando se le da el rol ADMIN, entra (el rol es lo que manda, no la persona)", ahoraAdmin55 is not null && ahoraAdmin55.Value.Roles.Contains("ADMIN"));
Afirmar("Con el rol ADMIN pero la clave mal escrita, no entra",
    await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "estudiante.uno", Clave = "otra-clave" }) is null);
Afirmar("Un correo institucional ya no es un nombre de usuario válido para entrar",
    await servicioUsr55.LoginAsync(new UsuarioLogin { Usuario = "estudiante.prueba@usbmed.edu.co", Clave = "3000999111" }) is null);

// cambio obligatorio de clave (sirve para cualquier cuenta, p. ej. un administrador nuevo con clave temporal)
var conFlag55 = await servicioUsr55.CrearAsync(new UsuarioCrear { FkAsistente = 71, Usuario = "admin.nuevo", Clave = "Temporal-1234", DebeCambiarClave = true });
Afirmar("Crear una cuenta con debe_cambiar_clave = true la deja marcada",
    (await servicioUsr55.ObtenerAsync(conFlag55.IdUsuario)).DebeCambiarClave);
var sinFlag55 = await servicioUsr55.CrearAsync(new UsuarioCrear { FkAsistente = 72, Usuario = "admin.otro", Clave = "Temporal-1234" });
Afirmar("...y sin pedirlo no se marca", !(await servicioUsr55.ObtenerAsync(sinFlag55.IdUsuario)).DebeCambiarClave);
await servicioUsr55.ExigirCambioClaveAsync(sinFlag55.IdUsuario);
Afirmar("El administrador puede exigir el cambio a una cuenta existente", (await servicioUsr55.ObtenerAsync(sinFlag55.IdUsuario)).DebeCambiarClave);
await AfirmarLanza<NoEncontradoExcepcion>("Exigir el cambio a un usuario inexistente lanza NoEncontradoExcepcion (404)",
    () => servicioUsr55.ExigirCambioClaveAsync(99999));
Afirmar("Cambiar la clave apaga la exigencia",
    await servicioUsr55.CambiarClaveAsync(sinFlag55.IdUsuario, new UsuarioCambiarClave { ClaveActual = "Temporal-1234", ClaveNueva = "Definitiva-5678" })
    && !(await servicioUsr55.ObtenerAsync(sinFlag55.IdUsuario)).DebeCambiarClave);

// ---- Cuenta de control --------------------------------------------------------
Console.WriteLine("\n== Cuenta de control: 37 tablas, 37 recursos ==\n");
// v7.1: D52 agrega Disponibilidad (consultas de solo lectura) y D53 agrega QrSesion (el QR de cierre: enlace_registro +
// asistencia). La inscripción (D50) se quitó en la D55. 37 del script + 2.
Afirmar("La API declara 39 controladores (los 37 de bdcatedras + Disponibilidad + QrSesion de la v7.1)",
    typeof(ApiCatedras.Controllers.BitacoraController).Assembly.GetTypes()
        .Count(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)) == 39);

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

    public Task<List<Sede>> ObtenerTodosAsync(int limite, bool incluirInactivos = false) =>
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

    // Maestro-detalle (Regla 1): lo que le entregó el servicio, para la prueba.
    public int LlamadasCrear { get; private set; }
    public IReadOnlyList<(Pregunta Pregunta, IReadOnlyList<OpcionPregunta> Opciones)> UltimasPreguntas { get; private set; } = [];

    public Task<long> CrearAsync(Encuesta encuesta,
        IReadOnlyList<(Pregunta Pregunta, IReadOnlyList<OpcionPregunta> Opciones)> preguntasIniciales)
    {
        LlamadasCrear++;
        UltimasPreguntas = preguntasIniciales;
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

    public Task<List<Facultad>> ObtenerTodosAsync(int limite, bool incluirInactivos = false) =>
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

    public Task<List<Dependencia>> ObtenerTodosAsync(int limite, bool incluirInactivos = false) =>
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

    public Task<List<ProgramaAcademico>> ObtenerTodosAsync(int limite, bool incluirInactivos = false) =>
        Task.FromResult(_tabla.Values.OrderBy(p => p.Codigo).Take(limite).ToList());

    public Task<ProgramaAcademico?> ObtenerPorIdAsync(string codigo) =>
        Task.FromResult(_tabla.TryGetValue(codigo, out var programa) ? programa : null);

    // Retrofit V2: guarda también los alias que le entregó el servicio, para
    // que la prueba de capas compruebe CÓMO los armó (fk y origen).
    public List<AliasPrograma> UltimosAlias { get; private set; } = new();

    public Task CrearAsync(ProgramaAcademico programa, IReadOnlyList<AliasPrograma> aliasIniciales)
    {
        if (_tabla.ContainsKey(programa.Codigo))
            throw new InvalidOperationException("codigo duplicado (simulado).");
        _tabla[programa.Codigo] = programa;
        UltimosAlias = aliasIniciales.ToList();
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

    public Task<int> ActualizarAsync(long idAlias, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idAlias, out var alias))
            return Task.FromResult(0);
        if (datos.TryGetValue("texto_normalizado", out var t)) alias.TextoNormalizado = (string)t!;
        if (datos.TryGetValue("fk_programa", out var f)) alias.FkPrograma = (string)f!;
        if (datos.TryGetValue("origen", out var o)) alias.Origen = (string)o!;
        return Task.FromResult(1);
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

    public Task<List<Catedra>> ObtenerTodosAsync(int? limite, bool incluirInactivos = false) =>
        Task.FromResult(_tabla.Values.OrderBy(c => c.IdCatedra).Take(limite ?? int.MaxValue).ToList());

    public Task<int> ContarAsync(bool incluirInactivos = false) => Task.FromResult(_tabla.Count);

    public Task<Catedra?> ObtenerPorIdAsync(long idCatedra) =>
        Task.FromResult(_tabla.TryGetValue(idCatedra, out var catedra) ? catedra : null);

    // Maestro-detalle (Regla 1): guarda lo que le entregó el servicio para que
    // la prueba compruebe que las sesiones llegaron JUNTO con la cátedra.
    public int LlamadasCrear { get; private set; }
    public IReadOnlyList<Sesion> UltimasSesiones { get; private set; } = [];

    public Task<(long IdCatedra, string NombreAsis)> CrearAsync(Catedra catedra, IReadOnlyList<Sesion> sesionesIniciales)
    {
        LlamadasCrear++;
        UltimasSesiones = sesionesIniciales;
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

// ==========================================================================
//  v3 · Repositorio FALSO de 'Asistente'. Simula el CALL al SP:
//   · IDENTITY -> contador '_siguienteId' (el id que "devuelve" el SP);
//   · guarda las 4 listas de detalle que le entregó el servicio, para que
//     la prueba compruebe que llegaron JUNTAS (Guía IA, Regla 1);
//   · el filtro 'q' sin distinguir mayúsculas, como el ILIKE de Postgres.
// ==========================================================================
class RepositorioAsistenteFalso : IRepositorioAsistente
{
    private readonly Dictionary<long, Asistente> _tabla = new();
    private long _siguienteId = 14812; // en el script real el último id es 14811

    public int LlamadasCrear { get; private set; }
    public long UltimoId { get; private set; }
    public List<DocumentoAsistente> UltimosDocumentos { get; private set; } = new();
    public List<VinculacionAsistente> UltimasVinculaciones { get; private set; } = new();
    public List<ProgramaAsistente> UltimosProgramas { get; private set; } = new();
    public ConsentimientoDatos? UltimoConsentimiento { get; private set; }

    public RepositorioAsistenteFalso()
    {
        _tabla[1] = new Asistente { IdAsistente = 1, IdAsis = "1000090", Nombres = "Asistente", Apellidos = "ASIS 1000090", CorreoInstitucional = "asis.1000090@usbmed.edu.co" };
        _tabla[2] = new Asistente { IdAsistente = 2, IdAsis = "1000445", Nombres = "Ana", Apellidos = "Garcia", CorreoInstitucional = "ana.garcia@usbmed.edu.co" };
        _tabla[3] = new Asistente { IdAsistente = 3, IdAsis = "1000513C", Nombres = "Luis", Apellidos = "Perez", CorreoPersonal = "luis@x.co" };
    }

    public Task<List<Asistente>> ObtenerTodosAsync(string? q, int limite, int desplazamiento)
    {
        // 'OrdinalIgnoreCase' = comparar sin distinguir mayúsculas (como ILIKE).
        var filas = _tabla.Values
            .Where(a => string.IsNullOrWhiteSpace(q)
                || $"{a.Nombres} {a.Apellidos}".Contains(q, StringComparison.OrdinalIgnoreCase)
                || (a.IdAsis ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.IdAsistente)
            .Skip(desplazamiento)   // OFFSET
            .Take(limite)           // LIMIT
            .ToList();
        return Task.FromResult(filas);
    }

    public Task<Asistente?> ObtenerPorIdAsync(long idAsistente) =>
        Task.FromResult(_tabla.TryGetValue(idAsistente, out var a) ? a : null);

    public Task<long> CrearAsync(Asistente asistente, IReadOnlyList<DocumentoAsistente> documentos,
        IReadOnlyList<VinculacionAsistente> vinculaciones, IReadOnlyList<ProgramaAsistente> programas,
        ConsentimientoDatos? consentimiento)
    {
        LlamadasCrear++;
        if (asistente.IdAsis is not null && _tabla.Values.Any(a => a.IdAsis == asistente.IdAsis))
            throw new InvalidOperationException("id_asis duplicado (simulado).");

        long id = _siguienteId++;     // lo que el SP devuelve en p_id_asistente
        asistente.CreadoEn = DateTime.UtcNow;
        _tabla[id] = asistente;

        UltimoId = id;
        // Se guardan las MISMAS instancias: si el servicio les pone el
        // fk_asistente después del CALL, la prueba lo ve aquí.
        UltimosDocumentos = documentos.ToList();
        UltimasVinculaciones = vinculaciones.ToList();
        UltimosProgramas = programas.ToList();
        UltimoConsentimiento = consentimiento;
        return Task.FromResult(id);
    }

    public Task<int> ActualizarAsync(long idAsistente, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idAsistente, out var a))
            return Task.FromResult(0);

        if (datos.TryGetValue("id_asis", out var ia)) a.IdAsis = (string?)ia;
        if (datos.TryGetValue("nombres", out var n)) a.Nombres = (string)n!;
        if (datos.TryGetValue("apellidos", out var ap)) a.Apellidos = (string)ap!;
        if (datos.TryGetValue("nombre_completo_asis", out var nc)) a.NombreCompletoAsis = (string?)nc;
        if (datos.TryGetValue("correo_institucional", out var ci)) a.CorreoInstitucional = (string?)ci;
        if (datos.TryGetValue("correo_personal", out var cp)) a.CorreoPersonal = (string?)cp;
        if (datos.TryGetValue("celular", out var ce)) a.Celular = (string?)ce;
        if (datos.TryGetValue("es_externo", out var ee)) a.EsExterno = (bool)ee!;
        if (datos.TryGetValue("activo", out var ac)) a.Activo = (bool)ac!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idAsistente) =>
        Task.FromResult(_tabla.Remove(idAsistente) ? 1 : 0);
}

// ==========================================================================
//  v3 · Repositorio FALSO de 'DocumentoAsistente'. Simula IDENTITY y now().
// ==========================================================================
class RepositorioDocumentoAsistenteFalso : IRepositorioDocumentoAsistente
{
    private readonly Dictionary<long, DocumentoAsistente> _tabla = new();
    private long _siguienteId = 3;

    public RepositorioDocumentoAsistenteFalso()
    {
        _tabla[1] = new DocumentoAsistente { IdDocumento = 1, FkAsistente = 1, FkTipoDocumento = "CC", Numero = "32182100" };
        _tabla[2] = new DocumentoAsistente { IdDocumento = 2, FkAsistente = 2, FkTipoDocumento = "CC", Numero = "43000111" };
    }

    public Task<List<DocumentoAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite) =>
        Task.FromResult(_tabla.Values
            .Where(d => fkAsistente is null || d.FkAsistente == fkAsistente)
            .OrderBy(d => d.IdDocumento).Take(limite).ToList());

    public Task<DocumentoAsistente?> ObtenerPorIdAsync(long idDocumento) =>
        Task.FromResult(_tabla.TryGetValue(idDocumento, out var d) ? d : null);

    public Task<(long IdDocumento, DateTime RegistradoEn)> CrearAsync(DocumentoAsistente documento)
    {
        long id = _siguienteId++;          // simula IDENTITY
        var ahora = DateTime.UtcNow;       // simula DEFAULT now()
        _tabla[id] = new DocumentoAsistente
        {
            IdDocumento = id, FkAsistente = documento.FkAsistente, FkTipoDocumento = documento.FkTipoDocumento,
            Numero = documento.Numero, Vigente = documento.Vigente, RegistradoEn = ahora,
        };
        return Task.FromResult((id, ahora));
    }

    public Task<int> ActualizarAsync(long idDocumento, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idDocumento, out var d))
            return Task.FromResult(0);

        if (datos.TryGetValue("fk_tipo_documento", out var t)) d.FkTipoDocumento = (string)t!;
        if (datos.TryGetValue("numero", out var n)) d.Numero = (string)n!;
        if (datos.TryGetValue("vigente", out var v)) d.Vigente = (bool)v!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idDocumento) =>
        Task.FromResult(_tabla.Remove(idDocumento) ? 1 : 0);
}

// ==========================================================================
//  v3 · Repositorio FALSO de 'VinculacionAsistente' (llave compuesta).
//  Solo los 3 métodos de la interfaz. Cuenta las llamadas a CerrarAsync.
// ==========================================================================
class RepositorioVinculacionAsistenteFalso : IRepositorioVinculacionAsistente
{
    private readonly List<VinculacionAsistente> _tabla = new()
    {
        new VinculacionAsistente { FkAsistente = 1, FkTipoVinculacion = "ESTUDIANTE", FechaIni = new DateOnly(2025, 1, 20) },
    };

    public int LlamadasCerrar { get; private set; }

    // Devuelve COPIAS: así el servicio no puede cambiar la "tabla" sin
    // pasar por CerrarAsync (como pasa con una BD de verdad).
    public Task<List<VinculacionAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite) =>
        Task.FromResult(_tabla
            .Where(v => fkAsistente is null || v.FkAsistente == fkAsistente)
            .Take(limite)
            .Select(v => new VinculacionAsistente
            { FkAsistente = v.FkAsistente, FkTipoVinculacion = v.FkTipoVinculacion, FechaIni = v.FechaIni, FechaFin = v.FechaFin })
            .ToList());

    public Task CrearAsync(VinculacionAsistente vinculacion)
    {
        if (_tabla.Any(v => v.FkAsistente == vinculacion.FkAsistente
                && v.FkTipoVinculacion == vinculacion.FkTipoVinculacion && v.FechaIni == vinculacion.FechaIni))
            throw new InvalidOperationException("llave duplicada (simulado).");
        _tabla.Add(vinculacion);
        return Task.CompletedTask;
    }

    public Task<int> CerrarAsync(long fkAsistente, string fkTipoVinculacion, DateOnly fechaIni, DateOnly fechaFin)
    {
        LlamadasCerrar++;
        var fila = _tabla.FirstOrDefault(v => v.FkAsistente == fkAsistente
            && v.FkTipoVinculacion == fkTipoVinculacion && v.FechaIni == fechaIni && v.FechaFin is null);
        if (fila is null) return Task.FromResult(0);
        fila.FechaFin = fechaFin;
        return Task.FromResult(1);
    }
}

// ==========================================================================
//  v3 · Repositorio FALSO de 'ProgramaAsistente' (llave compuesta).
// ==========================================================================
class RepositorioProgramaAsistenteFalso : IRepositorioProgramaAsistente
{
    private readonly List<ProgramaAsistente> _tabla = new()
    {
        new ProgramaAsistente { FkAsistente = 2, FkPrograma = "M0221", FkPeriodo = "2026-1" },
    };

    public int LlamadasCambiar { get; private set; }

    public Task<List<ProgramaAsistente>> ObtenerTodosAsync(long? fkAsistente, int limite) =>
        Task.FromResult(_tabla
            .Where(p => fkAsistente is null || p.FkAsistente == fkAsistente)
            .Take(limite)
            .Select(p => new ProgramaAsistente
            { FkAsistente = p.FkAsistente, FkPrograma = p.FkPrograma, FkPeriodo = p.FkPeriodo, Estado = p.Estado })
            .ToList());

    public Task CrearAsync(ProgramaAsistente programa)
    {
        _tabla.Add(new ProgramaAsistente
        { FkAsistente = programa.FkAsistente, FkPrograma = programa.FkPrograma, FkPeriodo = programa.FkPeriodo, Estado = programa.Estado });
        return Task.CompletedTask;
    }

    public Task<int> CambiarEstadoAsync(long fkAsistente, string fkPrograma, string fkPeriodo, string estado)
    {
        LlamadasCambiar++;
        var fila = _tabla.FirstOrDefault(p => p.FkAsistente == fkAsistente && p.FkPrograma == fkPrograma && p.FkPeriodo == fkPeriodo);
        if (fila is null) return Task.FromResult(0);
        fila.Estado = estado;
        return Task.FromResult(1);
    }
}

// ==========================================================================
//  v3 · Repositorio FALSO de 'ConsentimientoDatos'. Sin actualizar ni borrar.
// ==========================================================================
class RepositorioConsentimientoDatosFalso : IRepositorioConsentimientoDatos
{
    private readonly Dictionary<long, ConsentimientoDatos> _tabla = new();
    private long _siguienteId = 2;

    public RepositorioConsentimientoDatosFalso()
    {
        _tabla[1] = new ConsentimientoDatos { IdConsentimiento = 1, FkAsistente = 1, VersionPolitica = "v1.0" };
    }

    public Task<List<ConsentimientoDatos>> ObtenerTodosAsync(long? fkAsistente, int limite) =>
        Task.FromResult(_tabla.Values
            .Where(c => fkAsistente is null || c.FkAsistente == fkAsistente)
            .OrderBy(c => c.IdConsentimiento).Take(limite).ToList());

    public Task<ConsentimientoDatos?> ObtenerPorIdAsync(long idConsentimiento) =>
        Task.FromResult(_tabla.TryGetValue(idConsentimiento, out var c) ? c : null);

    public Task<(long IdConsentimiento, DateTime AceptadoEn)> CrearAsync(ConsentimientoDatos consentimiento)
    {
        long id = _siguienteId++;
        var ahora = DateTime.UtcNow;
        _tabla[id] = new ConsentimientoDatos
        {
            IdConsentimiento = id, FkAsistente = consentimiento.FkAsistente,
            VersionPolitica = consentimiento.VersionPolitica, Canal = consentimiento.Canal,
            Ip = consentimiento.Ip, AceptadoEn = ahora,
        };
        return Task.FromResult((id, ahora));
    }
}

// ==========================================================================
//  v3 · Repositorio FALSO de 'LoteCargaAsistente'. 'LlamadasTotales' cuenta
//  CUALQUIER uso del repositorio: la prueba lo usa para demostrar que las
//  reglas de los contadores las corta el servicio. Al cerrar, guarda las
//  novedades en la lista compartida con el falso de NovedadCarga (simula
//  que el SP las insertó en la misma BD).
// ==========================================================================
class RepositorioLoteCargaAsistenteFalso : IRepositorioLoteCargaAsistente
{
    private readonly Dictionary<long, LoteCargaAsistente> _tabla = new();
    private readonly List<NovedadCarga> _novedades;
    private long _siguienteId = 2;

    public int LlamadasTotales { get; private set; }

    public RepositorioLoteCargaAsistenteFalso(List<NovedadCarga> novedades)
    {
        _novedades = novedades;
        _tabla[1] = new LoteCargaAsistente { IdLoteCarga = 1, NombreArchivo = "USBME_EMPLID_CATED_V1.xlsx", FilasLeidas = 16093, FilasAceptadas = 14811, FilasRechazadas = 1282 };
    }

    public Task<List<LoteCargaAsistente>> ObtenerTodosAsync(int limite)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.Values.OrderByDescending(l => l.IdLoteCarga).Take(limite).ToList());
    }

    public Task<LoteCargaAsistente?> ObtenerPorIdAsync(long idLoteCarga)
    {
        LlamadasTotales++;
        // COPIA: el "estado guardado" solo cambia a través de CerrarAsync.
        return Task.FromResult(_tabla.TryGetValue(idLoteCarga, out var l)
            ? new LoteCargaAsistente
            {
                IdLoteCarga = l.IdLoteCarga, NombreArchivo = l.NombreArchivo, CargadoEn = l.CargadoEn,
                FkUsuario = l.FkUsuario, FilasLeidas = l.FilasLeidas, FilasAceptadas = l.FilasAceptadas,
                FilasRechazadas = l.FilasRechazadas, Observaciones = l.Observaciones,
            }
            : null);
    }

    public Task<(long IdLoteCarga, DateTime CargadoEn)> CrearAsync(LoteCargaAsistente lote)
    {
        LlamadasTotales++;
        long id = _siguienteId++;
        var ahora = DateTime.UtcNow;
        _tabla[id] = new LoteCargaAsistente { IdLoteCarga = id, NombreArchivo = lote.NombreArchivo, CargadoEn = ahora };
        return Task.FromResult((id, ahora));
    }

    public Task CerrarAsync(LoteCargaAsistente lote, IReadOnlyList<NovedadCarga> novedades)
    {
        LlamadasTotales++;
        var guardado = _tabla[lote.IdLoteCarga];
        guardado.FilasLeidas = lote.FilasLeidas;
        guardado.FilasAceptadas = lote.FilasAceptadas;
        guardado.FilasRechazadas = lote.FilasRechazadas;
        guardado.Observaciones = lote.Observaciones;

        long siguienteNovedad = _novedades.Count + 1;
        foreach (var n in novedades)
        {
            n.IdNovedad = siguienteNovedad++;   // simula IDENTITY de novedad_carga
            _novedades.Add(n);
        }
        return Task.CompletedTask;
    }
}

// ==========================================================================
//  v3 · Repositorio FALSO de 'NovedadCarga'. Solo lectura, sobre la lista
//  que llena el falso de LoteCargaAsistente al cerrar.
// ==========================================================================
class RepositorioNovedadCargaFalso : IRepositorioNovedadCarga
{
    private readonly List<NovedadCarga> _tabla;

    public RepositorioNovedadCargaFalso(List<NovedadCarga> tabla)
    {
        _tabla = tabla;
    }

    public Task<List<NovedadCarga>> ObtenerTodosAsync(long? fkLoteCarga, int limite) =>
        Task.FromResult(_tabla
            .Where(n => fkLoteCarga is null || n.FkLoteCarga == fkLoteCarga)
            .OrderBy(n => n.NumeroFila).Take(limite).ToList());

    public Task<NovedadCarga?> ObtenerPorIdAsync(long idNovedad) =>
        Task.FromResult(_tabla.FirstOrDefault(n => n.IdNovedad == idNovedad));
}


// ==========================================================================
//  v4 · Repositorio FALSO de 'Sesion'. Simula lo que hace la BD:
//   · IDENTITY -> contador '_siguienteId';
//   · DEFAULT now() -> 'CreadoEn = DateTime.UtcNow';
//   · EL TRIGGER (ADR D20): numero_reunion 0 -> MAX + 1 de esa cátedra.
//     Solo AQUÍ, dentro del falso: el servicio no sabe nada de este cálculo;
//   · uq_sesion_reunion: un número explícito repetido en la cátedra -> error.
//  Cuenta las llamadas a CrearAsync y guarda el último número RECIBIDO, para
//  que la prueba compruebe qué le entregó el servicio.
// ==========================================================================
class RepositorioSesionFalso : IRepositorioSesion
{
    private readonly Dictionary<long, Sesion> _tabla = new();
    private long _siguienteId = 5715; // en el script real el último id es 5714

    public int LlamadasCrear { get; private set; }
    public short UltimoNumeroRecibido { get; private set; } = -1;

    public RepositorioSesionFalso()
    {
        var inicio = new DateTime(2026, 8, 31, 13, 0, 0, DateTimeKind.Utc);
        Guardar(new Sesion { IdSesion = 1, FkCatedra = 1, NumeroReunion = 33, Inicio = inicio, Fin = inicio.AddHours(2),
            FkModalidad = "PRESENCIAL", FkSede = "SAN_BENITO", FkPeriodo = "2026-1", Estado = "CERRADA" });
        Guardar(new Sesion { IdSesion = 100, FkCatedra = 3975, NumeroReunion = 2, Inicio = inicio.AddDays(7), Fin = inicio.AddDays(7).AddHours(2),
            FkModalidad = "PRESENCIAL", FkSede = "SAN_BENITO", FkPeriodo = "2026-1", Estado = "CERRADA" });
        Guardar(new Sesion { IdSesion = 99, FkCatedra = 3975, NumeroReunion = 1, Inicio = inicio, Fin = inicio.AddHours(2),
            FkModalidad = "PRESENCIAL", FkSede = "SAN_BENITO", FkPeriodo = "2026-1", Estado = "CERRADA" });
    }

    private void Guardar(Sesion s) => _tabla[s.IdSesion] = s;

    public Task<List<Sesion>> ObtenerTodosAsync(long? fkCatedra, int limite) =>
        Task.FromResult(_tabla.Values
            .Where(s => fkCatedra is null || s.FkCatedra == fkCatedra)
            .OrderBy(s => s.FkCatedra).ThenBy(s => s.NumeroReunion)
            .Take(limite)
            .ToList());

    public Task<Sesion?> ObtenerPorIdAsync(long idSesion) =>
        Task.FromResult(_tabla.TryGetValue(idSesion, out var sesion) ? sesion : null);

    // Maestro-detalle (Regla 1): el enlace que pidió el servicio (null = ninguno).
    public (int? MinutosAntes, int? MinutosDespues, long? FkUsuario)? UltimoEnlace { get; private set; }

    public Task<(long IdSesion, short NumeroReunion, DateTime CreadoEn)> CrearAsync(
        Sesion sesion, (int? MinutosAntes, int? MinutosDespues, long? FkUsuario)? enlaceInicial)
    {
        LlamadasCrear++;
        UltimoEnlace = enlaceInicial;
        UltimoNumeroRecibido = sesion.NumeroReunion;

        if (sesion.NumeroReunion == 0)
        {
            // Lo que hace 'fn_trg_sesion_consecutivo': coalesce(max, 0) + 1.
            var deLaCatedra = _tabla.Values.Where(s => s.FkCatedra == sesion.FkCatedra).ToList();
            sesion.NumeroReunion = (short)((deLaCatedra.Count == 0 ? 0 : deLaCatedra.Max(s => s.NumeroReunion)) + 1);
        }
        else if (_tabla.Values.Any(s => s.FkCatedra == sesion.FkCatedra && s.NumeroReunion == sesion.NumeroReunion))
        {
            throw new InvalidOperationException("uq_sesion_reunion (simulado).");
        }

        sesion.IdSesion = _siguienteId++;
        sesion.CreadoEn = DateTime.UtcNow;
        Guardar(sesion);
        return Task.FromResult((sesion.IdSesion, sesion.NumeroReunion, sesion.CreadoEn));
    }

    public Task<int> ActualizarAsync(long idSesion, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idSesion, out var s))
            return Task.FromResult(0);

        if (datos.TryGetValue("fk_catedra", out var c)) s.FkCatedra = (long)c!;
        if (datos.TryGetValue("numero_reunion", out var n)) s.NumeroReunion = (short)n!;
        if (datos.TryGetValue("numero_reunion_asis", out var na)) s.NumeroReunionAsis = (short?)na;
        if (datos.TryGetValue("titulo", out var t)) s.Titulo = (string?)t;
        if (datos.TryGetValue("inicio", out var i)) s.Inicio = (DateTime)i!;
        if (datos.TryGetValue("fin", out var f)) s.Fin = (DateTime)f!;
        if (datos.TryGetValue("fk_modalidad", out var m)) s.FkModalidad = (string)m!;
        if (datos.TryGetValue("fk_sede", out var se)) s.FkSede = (string)se!;
        if (datos.TryGetValue("fk_periodo", out var p)) s.FkPeriodo = (string)p!;
        if (datos.TryGetValue("fk_encuesta", out var e)) s.FkEncuesta = (long?)e;
        if (datos.TryGetValue("lugar", out var l)) s.Lugar = (string?)l;
        if (datos.TryGetValue("enlace_virtual", out var ev)) s.EnlaceVirtual = (string?)ev;
        if (datos.TryGetValue("estado", out var es)) s.Estado = (string)es!;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idSesion) =>
        Task.FromResult(_tabla.Remove(idSesion) ? 1 : 0);
}

// ==========================================================================
//  v4 · Repositorio FALSO de 'Ponencia' (llave compuesta SIN vigencia).
//  ActualizarAsync (RETROFIT criterio 3) cambia el ROL de la fila. La PK
//  duplicada se simula con una excepción, como hace la BD.
// ==========================================================================
class RepositorioPonenciaFalso : IRepositorioPonencia
{
    private readonly List<Ponencia> _tabla = new()
    {
        new Ponencia { FkSesion = 1, FkAsistente = 167, Rol = "INVITADO" },
    };

    // ¿Esta fila tiene EXACTAMENTE esta llave de 3 columnas?
    private static bool MismaLlave(Ponencia p, long fkSesion, long fkAsistente, string rol) =>
        p.FkSesion == fkSesion && p.FkAsistente == fkAsistente && p.Rol == rol;

    public Task<List<Ponencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite) =>
        Task.FromResult(_tabla
            .Where(p => (fkSesion is null || p.FkSesion == fkSesion)
                     && (fkAsistente is null || p.FkAsistente == fkAsistente))
            .Take(limite)
            .ToList());

    public Task<Ponencia?> ObtenerUnoAsync(long fkSesion, long fkAsistente, string rol) =>
        Task.FromResult(_tabla.FirstOrDefault(p => MismaLlave(p, fkSesion, fkAsistente, rol)));

    public Task CrearAsync(Ponencia ponencia)
    {
        if (_tabla.Any(p => MismaLlave(p, ponencia.FkSesion, ponencia.FkAsistente, ponencia.Rol)))
            throw new InvalidOperationException("pk_ponencia duplicada (simulado).");
        _tabla.Add(ponencia);
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(long fkSesion, long fkAsistente, string rol, string rolNuevo)
    {
        var fila = _tabla.FirstOrDefault(p => MismaLlave(p, fkSesion, fkAsistente, rol));
        if (fila is null)
            return Task.FromResult(0);
        if (_tabla.Any(p => MismaLlave(p, fkSesion, fkAsistente, rolNuevo)))
            throw new InvalidOperationException("pk_ponencia duplicada (simulado).");
        fila.Rol = rolNuevo;
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol) =>
        // 'RemoveAll' devuelve cuántas quitó: 1 si existía, 0 si no.
        Task.FromResult(_tabla.RemoveAll(p => MismaLlave(p, fkSesion, fkAsistente, rol)));
}

// ==========================================================================
//  v5 · Repositorio FALSO de 'Usuario'. Guarda 'ClaveHash' tal cual lo
//  calculó ServicioUsuario (BCrypt real): así la prueba puede verificarlo.
//  Semilla: 'admin' (activo, clave Admin12345) e 'inactivo.prueba' (inactivo,
//  clave Clave12345). Cuenta las llamadas a ActualizarUltimoAccesoAsync.
// ==========================================================================
class RepositorioUsuarioFalso : IRepositorioUsuario
{
    private readonly Dictionary<long, Usuario> _tabla = new();
    private long _siguienteId = 3;

    public int LlamadasUltimoAcceso { get; private set; }

    public RepositorioUsuarioFalso()
    {
        _tabla[1] = new Usuario
        {
            IdUsuario = 1, FkAsistente = 1, NombreUsuario = "admin",
            ClaveHash = BCrypt.Net.BCrypt.HashPassword("Admin12345"),
            Activo = true, CreadoEn = DateTime.UtcNow,
        };
        _tabla[2] = new Usuario
        {
            IdUsuario = 2, FkAsistente = 2, NombreUsuario = "inactivo.prueba",
            ClaveHash = BCrypt.Net.BCrypt.HashPassword("Clave12345"),
            Activo = false, CreadoEn = DateTime.UtcNow,
        };
    }

    public Task<List<Usuario>> ObtenerTodosAsync(int limite) =>
        Task.FromResult(_tabla.Values.OrderBy(u => u.IdUsuario).Take(limite).ToList());

    public Task<Usuario?> ObtenerPorIdAsync(long idUsuario) =>
        Task.FromResult(_tabla.TryGetValue(idUsuario, out var u) ? u : null);

    public Task<Usuario?> ObtenerPorLoginAsync(string usuario) =>
        Task.FromResult(_tabla.Values.FirstOrDefault(u => u.NombreUsuario == usuario));

    public Task<(long IdUsuario, DateTime CreadoEn)> CrearAsync(Usuario usuario)
    {
        if (_tabla.Values.Any(u => u.NombreUsuario == usuario.NombreUsuario))
            throw new InvalidOperationException("uq_usuario_login (simulado).");
        if (_tabla.Values.Any(u => u.FkAsistente == usuario.FkAsistente))
            throw new InvalidOperationException("uq_usuario_persona (simulado).");

        usuario.IdUsuario = _siguienteId++;
        usuario.CreadoEn = DateTime.UtcNow;
        _tabla[usuario.IdUsuario] = usuario;
        return Task.FromResult((usuario.IdUsuario, usuario.CreadoEn));
    }

    public Task<int> ActualizarAsync(long idUsuario, Dictionary<string, object?> datos)
    {
        if (!_tabla.TryGetValue(idUsuario, out var u))
            return Task.FromResult(0);

        if (datos.TryGetValue("fk_asistente", out var f)) u.FkAsistente = (long)f!;
        if (datos.TryGetValue("usuario", out var n)) u.NombreUsuario = (string)n!;
        // Como el disparador trg_usuario_clave_cambiada: cambiar la clave apaga debe_cambiar_clave.
        if (datos.TryGetValue("clave_hash", out var h)) { u.ClaveHash = (string)h!; u.DebeCambiarClave = false; }
        if (datos.TryGetValue("activo", out var a)) u.Activo = (bool)a!;
        return Task.FromResult(1);
    }

    public Task ActualizarUltimoAccesoAsync(long idUsuario)
    {
        LlamadasUltimoAcceso++;
        if (_tabla.TryGetValue(idUsuario, out var u))
            u.UltimoAcceso = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<int> EliminarAsync(long idUsuario) =>
        Task.FromResult(_tabla.Remove(idUsuario) ? 1 : 0);

    public Task FijarDebeCambiarClaveAsync(long idUsuario, bool valor)
    {
        if (_tabla.TryGetValue(idUsuario, out var u)) u.DebeCambiarClave = valor;
        return Task.CompletedTask;
    }
}

// ==========================================================================
//  v5 · Repositorio FALSO de 'RolPorUsuario' (llave compuesta, ADR D16).
//  Semilla: usuario 1 con ADMIN vigente y CONSULTA ya cerrado. Cuenta las
//  llamadas a CerrarAsync.
// ==========================================================================
class RepositorioRolPorUsuarioFalso : IRepositorioRolPorUsuario
{
    private readonly List<RolPorUsuario> _tabla = new()
    {
        new RolPorUsuario { FkUsuario = 1, FkRol = "ADMIN", FechaIni = new DateOnly(2026, 1, 1) },
        new RolPorUsuario { FkUsuario = 1, FkRol = "CONSULTA", FechaIni = new DateOnly(2025, 1, 1), FechaFin = new DateOnly(2025, 12, 31) },
    };

    public int LlamadasCerrar { get; private set; }

    // Devuelve COPIAS: el servicio no puede cambiar la "tabla" sin pasar por CerrarAsync.
    public Task<List<RolPorUsuario>> ObtenerTodosAsync(long? fkUsuario, int limite) =>
        Task.FromResult(_tabla
            .Where(r => fkUsuario is null || r.FkUsuario == fkUsuario)
            .Take(limite)
            .Select(r => new RolPorUsuario
            { FkUsuario = r.FkUsuario, FkRol = r.FkRol, FechaIni = r.FechaIni, FechaFin = r.FechaFin })
            .ToList());

    public Task<List<string>> ObtenerVigentesAsync(long fkUsuario) =>
        Task.FromResult(_tabla
            .Where(r => r.FkUsuario == fkUsuario && r.FechaFin is null)
            .Select(r => r.FkRol)
            .OrderBy(x => x)
            .ToList());

    public Task CrearAsync(RolPorUsuario rol)
    {
        if (_tabla.Any(r => r.FkUsuario == rol.FkUsuario && r.FkRol == rol.FkRol && r.FechaIni == rol.FechaIni))
            throw new InvalidOperationException("llave duplicada (simulado).");
        _tabla.Add(rol);
        return Task.CompletedTask;
    }

    public Task<int> CerrarAsync(long fkUsuario, string fkRol, DateOnly fechaIni, DateOnly fechaFin)
    {
        LlamadasCerrar++;
        var fila = _tabla.FirstOrDefault(r => r.FkUsuario == fkUsuario
            && r.FkRol == fkRol && r.FechaIni == fechaIni && r.FechaFin is null);
        if (fila is null) return Task.FromResult(0);
        fila.FechaFin = fechaFin;
        return Task.FromResult(1);
    }
}

// ==========================================================================
//  v5 · Repositorio FALSO de 'EnlaceRegistro'. Simula fn_emitir_enlace_api:
//   · sesión 999 inexistente -> excepción (el RAISE de la BD);
//   · RN-14: revoca el enlace vigente anterior de la MISMA sesión;
//   · la BD decide token, url, canal y ventana.
//  Guarda los últimos minutos recibidos y cuenta las llamadas a RevocarAsync.
// ==========================================================================
class RepositorioEnlaceRegistroFalso : IRepositorioEnlaceRegistro
{
    private readonly List<EnlaceRegistro> _tabla = new();
    private long _siguienteId = 2;

    public int LlamadasRevocar { get; private set; }
    public int? UltimosMinutosAntes { get; private set; }
    public int? UltimosMinutosDespues { get; private set; }

    public RepositorioEnlaceRegistroFalso()
    {
        var token = Guid.NewGuid();
        var inicio = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);
        _tabla.Add(new EnlaceRegistro
        {
            IdEnlace = 1, FkSesion = 10, Token = token, UrlPublica = "https://catedras.example/r/" + token,
            Canal = "QR", VentanaDesde = inicio.AddMinutes(-30), VentanaHasta = inicio.AddHours(2).AddMinutes(30),
            CreadoEn = DateTime.UtcNow,
        });
    }

    public Task<List<EnlaceRegistro>> ObtenerTodosAsync(long? fkSesion, int limite) =>
        Task.FromResult(_tabla
            .Where(e => fkSesion is null || e.FkSesion == fkSesion)
            .OrderBy(e => e.IdEnlace).Take(limite).ToList());

    public Task<EnlaceRegistro?> ObtenerPorIdAsync(long idEnlace) =>
        Task.FromResult(_tabla.FirstOrDefault(e => e.IdEnlace == idEnlace));

    public Task<long> EmitirAsync(long fkSesion, int? minutosAntes, int? minutosDespues, long? fkUsuario)
    {
        UltimosMinutosAntes = minutosAntes;
        UltimosMinutosDespues = minutosDespues;

        if (fkSesion == 999)
            throw new InvalidOperationException("No existe la sesion 999 (simulado).");

        // RN-14: se revoca el vigente anterior de esa sesión.
        foreach (var vigente in _tabla.Where(e => e.FkSesion == fkSesion && e.RevocadoEn is null))
            vigente.RevocadoEn = DateTime.UtcNow;

        var token = Guid.NewGuid();
        var inicio = new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc);
        var enlace = new EnlaceRegistro
        {
            IdEnlace = _siguienteId++, FkSesion = fkSesion, Token = token,
            UrlPublica = "https://catedras.example/r/" + token,
            Canal = "QR",
            VentanaDesde = inicio.AddMinutes(-(minutosAntes ?? 30)),
            VentanaHasta = inicio.AddHours(2).AddMinutes(minutosDespues ?? 30),
            FkUsuarioCrea = fkUsuario, CreadoEn = DateTime.UtcNow,
        };
        _tabla.Add(enlace);
        return Task.FromResult(enlace.IdEnlace);
    }

    public Task<int> RevocarAsync(long idEnlace)
    {
        LlamadasRevocar++;
        var fila = _tabla.FirstOrDefault(e => e.IdEnlace == idEnlace && e.RevocadoEn is null);
        if (fila is null) return Task.FromResult(0);
        fila.RevocadoEn = DateTime.UtcNow;
        return Task.FromResult(1);
    }
}

// ==========================================================================
//  v5 · Repositorio FALSO de 'ClaveAcceso'. Simula las dos funciones de la BD:
//   · fn_solicitar_clave_api: token desconocido -> excepción; si no, crea una
//     clave y devuelve (id, "483920", correo);
//   · fn_validar_clave_api: clave incorrecta o ya usada -> excepción (D25); si
//     no, la marca usada y devuelve un id_registro.
//  Cuenta las solicitudes y guarda la última IP recibida.
// ==========================================================================
class RepositorioClaveAccesoFalso : IRepositorioClaveAcceso
{
    public static readonly Guid TokenVigente = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public const string ClaveFija = "483920";

    private readonly List<ClaveAcceso> _tabla = new();
    private long _siguienteId = 501;
    private long _siguienteRegistro = 900;

    public int LlamadasSolicitar { get; private set; }
    public int LlamadasValidar { get; private set; }
    public string? UltimaIp { get; private set; }
    public decimal? UltimaLatitud { get; private set; }
    public decimal? UltimaLongitud { get; private set; }

    public Task<List<ClaveAcceso>> ObtenerTodosAsync(long? fkAsistente, int limite) =>
        Task.FromResult(_tabla
            .Where(c => fkAsistente is null || c.FkAsistente == fkAsistente)
            .OrderBy(c => c.IdClave).Take(limite).ToList());

    public Task<ClaveAcceso?> ObtenerPorIdAsync(long idClave) =>
        Task.FromResult(_tabla.FirstOrDefault(c => c.IdClave == idClave));

    public Task<(long IdClave, string Clave, string EnviadoA)> SolicitarAsync(
        string identificador, Guid token, string? ip)
    {
        LlamadasSolicitar++;
        UltimaIp = ip;

        if (token != TokenVigente)
            throw new InvalidOperationException("Enlace inexistente, revocado o fuera de ventana (simulado).");

        var ahora = DateTime.UtcNow;
        long fkAsistente = long.Parse(identificador);
        var clave = new ClaveAcceso
        {
            IdClave = _siguienteId++, FkAsistente = fkAsistente, FkEnlace = 1,
            EnviadoA = $"asis.{identificador}@usbmed.edu.co", EsCorreoInstitucional = true,
            GeneradoEn = ahora, ExpiraEn = ahora.AddMinutes(10), IpSolicitud = ip,
        };
        _tabla.Add(clave);
        return Task.FromResult((clave.IdClave, ClaveFija, clave.EnviadoA));
    }

    public Task<long> ValidarAsync(long idClave, string clave, decimal latitud, decimal longitud,
        string? ip, string? userAgent)
    {
        LlamadasValidar++;
        var fila = _tabla.FirstOrDefault(c => c.IdClave == idClave)
            ?? throw new InvalidOperationException("La clave no existe (simulado).");

        if (fila.UsadoEn is not null)
            throw new InvalidOperationException("La clave ya fue usada (simulado, RN-20).");
        if (clave != ClaveFija)
            throw new InvalidOperationException("Clave incorrecta (simulado).");

        fila.UsadoEn = DateTime.UtcNow;
        UltimaLatitud = latitud;      // el SP de la BD las guarda en registro_asistencia
        UltimaLongitud = longitud;
        return Task.FromResult(_siguienteRegistro++);
    }
}

// ==========================================================================
//  v7.1 · Repositorio FALSO de 'CodigoRecuperacion'. Simula los dos
//  procedimientos de la migración 047: una cuenta "admin" existe, el código
//  correcto es siempre CodigoFijo y se puede usar UNA vez. Guarda el último
//  hash que recibió para comprobar que es un hash y no la clave.
// ==========================================================================
class RepositorioCodigoRecuperacionFalso : IRepositorioCodigoRecuperacion
{
    public const string CodigoFijo = "719304";

    private readonly Dictionary<long, bool> _usado = new();   // id_codigo -> ¿ya se usó?
    private long _siguienteId = 70;

    public string? UltimoIdentificador { get; private set; }
    public string? UltimaIp { get; private set; }
    public string? UltimoHashRecibido { get; private set; }

    public Task<(long IdCodigo, string Codigo, string EnviadoA, string Usuario)?> SolicitarAsync(
        string identificador, string? ip)
    {
        UltimoIdentificador = identificador;
        UltimaIp = ip;

        if (identificador != "admin")
            return Task.FromResult<(long, string, string, string)?>(null);   // sin cuenta activa

        long id = _siguienteId++;
        _usado[id] = false;
        return Task.FromResult<(long, string, string, string)?>((id, CodigoFijo, "admin.catedras@usbmed.edu.co", "admin"));
    }

    public Task<string> RestablecerAsync(long idCodigo, string codigo, string claveHash)
    {
        // Como sp_restablecer_clave: devuelve un ESTADO, no lanza.
        if (!_usado.TryGetValue(idCodigo, out var usado))
            return Task.FromResult("NO_EXISTE");
        if (usado)
            return Task.FromResult("USADO");
        if (codigo != CodigoFijo)
            return Task.FromResult("INCORRECTO");

        _usado[idCodigo] = true;
        UltimoHashRecibido = claveHash;
        return Task.FromResult("OK");
    }
}

// ==========================================================================
//  v5 · Repositorio FALSO de 'RegistroAsistencia'. Simula en CrearAsync lo
//  que hacen el trigger y las restricciones de la BD:
//   · la instantánea de vinculación ("ESTUDIANTE");
//   · uq_registro_sesion_asis: un asistente no se registra dos veces en la
//     misma sesión -> excepción.
//  Cuenta las llamadas a CrearAsync (para probar que D26 corta ANTES).
// ==========================================================================
class RepositorioRegistroAsistenciaFalso : IRepositorioRegistroAsistencia
{
    private readonly List<RegistroAsistencia> _tabla = new()
    {
        new RegistroAsistencia
        {
            IdRegistro = 1, FkSesion = 10, FkAsistente = 1, FkEnlace = 1, FkClave = 500,
            RegistradoEn = DateTime.UtcNow, FkVinculacionSnapshot = "ESTUDIANTE", Origen = "QR",
        },
    };
    private long _siguienteId = 2;

    public int LlamadasCrear { get; private set; }

    public Task<List<RegistroAsistencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite) =>
        Task.FromResult(_tabla
            .Where(r => (fkSesion is null || r.FkSesion == fkSesion)
                     && (fkAsistente is null || r.FkAsistente == fkAsistente))
            .OrderBy(r => r.IdRegistro).Take(limite).ToList());

    public Task<RegistroAsistencia?> ObtenerPorIdAsync(long idRegistro) =>
        Task.FromResult(_tabla.FirstOrDefault(r => r.IdRegistro == idRegistro));

    public Task<RegistroAsistencia> CrearAsync(long fkSesion, long fkAsistente, string origen)
    {
        LlamadasCrear++;

        if (_tabla.Any(r => r.FkSesion == fkSesion && r.FkAsistente == fkAsistente))
            throw new InvalidOperationException("uq_registro_sesion_asis (simulado).");

        var registro = new RegistroAsistencia
        {
            IdRegistro = _siguienteId++, FkSesion = fkSesion, FkAsistente = fkAsistente,
            RegistradoEn = DateTime.UtcNow,
            FkVinculacionSnapshot = "ESTUDIANTE",   // lo pone el trigger (RN-22)
            Origen = origen,
        };
        _tabla.Add(registro);
        return Task.FromResult(registro);
    }
}

// ==========================================================================
//  RETROFIT Fase 4 · Repositorio FALSO de 'LoteMigracion'. Simula
//  sp_generar_lote_migracion: al "generar" calcula el nombre del archivo y
//  el total, y deja 3 filas de detalle en la lista COMPARTIDA con el falso
//  de DetalleMigracion. Para la sesión 'SesionSinMigrables' lanza una
//  excepción, como el RAISE EXCEPTION de la BD. Cuenta las llamadas para
//  demostrar que el 400 del PATCH vacío no llega al repositorio.
// ==========================================================================
class RepositorioLoteMigracionFalso : IRepositorioLoteMigracion
{
    public const long SesionSinMigrables = 404;

    private readonly List<LoteMigracion> _lotes = new();
    private readonly List<DetalleMigracion> _detalles;
    private long _siguienteLote = 1;
    private long _siguienteDetalle = 1;

    public int LlamadasTotales { get; private set; }

    public RepositorioLoteMigracionFalso(List<DetalleMigracion> detalles)
    {
        _detalles = detalles;
    }

    public Task<List<LoteMigracion>> ObtenerTodosAsync(long? fkSesion, int limite)
    {
        LlamadasTotales++;
        var filas = _lotes
            .Where(l => fkSesion is null || l.FkSesion == fkSesion)
            .OrderByDescending(l => l.IdLoteMigracion)
            .Take(limite)
            .ToList();
        return Task.FromResult(filas);
    }

    public Task<LoteMigracion?> ObtenerPorIdAsync(long idLoteMigracion)
    {
        LlamadasTotales++;
        return Task.FromResult(_lotes.FirstOrDefault(l => l.IdLoteMigracion == idLoteMigracion));
    }

    public Task<LoteMigracion> CrearAsync(long fkSesion, long? fkUsuario)
    {
        LlamadasTotales++;
        if (fkSesion == SesionSinMigrables)
            throw new InvalidOperationException(
                $"No hay registros migrables en la sesion {fkSesion} (simula el RAISE de la BD).");

        var lote = new LoteMigracion
        {
            IdLoteMigracion = _siguienteLote++,
            FkSesion = fkSesion,
            FkUsuario = fkUsuario,
            NombreArchivo = $"ME_CATEDRA_FALSO_S{fkSesion}.xlsx",
            GeneradoEn = DateTimeOffset.UtcNow,
            TotalFilas = 3,
        };
        _lotes.Add(lote);

        for (int i = 1; i <= lote.TotalFilas; i++)
        {
            _detalles.Add(new DetalleMigracion
            {
                IdDetalle = _siguienteDetalle++,
                FkLoteMigracion = lote.IdLoteMigracion,
                FkRegistro = i,
                IdAsisEnviado = $"100000{i}",
                ProgramaEnviado = "M0200",
                ReunionEnviada = 1,
            });
        }

        return Task.FromResult(lote);
    }

    public Task<int> ActualizarAsync(long idLoteMigracion, Dictionary<string, object?> datos)
    {
        LlamadasTotales++;
        var lote = _lotes.FirstOrDefault(l => l.IdLoteMigracion == idLoteMigracion);
        if (lote is null)
            return Task.FromResult(0);

        // Misma mezcla que el repositorio real: lo que no llegó, se queda.
        if (datos.TryGetValue("instancia_proceso_asis", out var vInstancia)) lote.InstanciaProcesoAsis = (string?)vInstancia;
        if (datos.TryGetValue("fk_estado_proceso", out var vEstado)) lote.FkEstadoProceso = (string)vEstado!;
        if (datos.TryGetValue("observaciones", out var vObs)) lote.Observaciones = (string?)vObs;

        return Task.FromResult(1);
    }
}

// ==========================================================================
//  RETROFIT Fase 4 · Repositorio FALSO de 'DetalleMigracion'. Lee y edita la
//  lista que llenó el falso de lotes al "generar".
// ==========================================================================
class RepositorioDetalleMigracionFalso : IRepositorioDetalleMigracion
{
    private readonly List<DetalleMigracion> _detalles;

    public RepositorioDetalleMigracionFalso(List<DetalleMigracion> detalles)
    {
        _detalles = detalles;
    }

    public Task<List<DetalleMigracion>> ObtenerTodosAsync(long? fkLoteMigracion, int limite)
    {
        var filas = _detalles
            .Where(d => fkLoteMigracion is null || d.FkLoteMigracion == fkLoteMigracion)
            .OrderBy(d => d.FkLoteMigracion).ThenBy(d => d.IdDetalle)
            .Take(limite)
            .ToList();
        return Task.FromResult(filas);
    }

    public Task<DetalleMigracion?> ObtenerPorIdAsync(long idDetalle)
    {
        return Task.FromResult(_detalles.FirstOrDefault(d => d.IdDetalle == idDetalle));
    }

    public Task<int> ActualizarAsync(long idDetalle, Dictionary<string, object?> datos)
    {
        var detalle = _detalles.FirstOrDefault(d => d.IdDetalle == idDetalle);
        if (detalle is null)
            return Task.FromResult(0);

        if (datos.TryGetValue("resultado", out var vResultado)) detalle.Resultado = (string)vResultado!;
        if (datos.TryGetValue("mensaje", out var vMensaje)) detalle.Mensaje = (string?)vMensaje;

        return Task.FromResult(1);
    }
}


// ==========================================================================
//  VERSIÓN 7 · Fase 58 — repositorios FALSOS de Parametro y Bitacora, y de
//  las 4 entidades de la v6 que faltaban (Pregunta, OpcionPregunta,
//  RespuestaEncuesta, RespuestaItem). Mismo contrato que el repositorio
//  real, en memoria; ninguna línea de PostgreSQL.
// ==========================================================================

/// <summary>Falso de 'parametro'. Simula lo que hace la BD: PK de texto
/// (clave repetida -> error) y 'modificado_en' que fija el SP.</summary>
class RepositorioParametroFalso : IRepositorioParametro
{
    private readonly SortedDictionary<string, Parametro> _tabla = new(StringComparer.Ordinal);

    // Reloj del "motor": avanza un segundo por operación para que la
    // prueba no dependa de la resolución de DateTime.UtcNow.
    private DateTime _reloj = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private DateTime Ahora() => _reloj = _reloj.AddSeconds(1);

    public int LlamadasTotales { get; private set; }

    public RepositorioParametroFalso()
    {
        _tabla["URL_BASE"] = new Parametro { Clave = "URL_BASE", Valor = "https://catedras.usbmed.edu.co/r/", Descripcion = "Base de los enlaces", ModificadoEn = Ahora() };
        _tabla["CLAVE_LONGITUD"] = new Parametro { Clave = "CLAVE_LONGITUD", Valor = "6", TipoDato = "ENTERO", Descripcion = "Dígitos de la clave", ModificadoEn = Ahora() };
    }

    public Task<List<Parametro>> ObtenerTodosAsync(int limite)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.Values.Take(limite).ToList());   // ORDER BY clave
    }

    public Task<Parametro?> ObtenerPorIdAsync(string clave)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.TryGetValue(clave, out var p) ? p : null);
    }

    public Task CrearAsync(Parametro parametro)
    {
        LlamadasTotales++;
        if (_tabla.ContainsKey(parametro.Clave))
            throw new InvalidOperationException(
                $"duplicate key value violates unique constraint \"pk_parametro\" (simula la BD: '{parametro.Clave}').");
        parametro.ModificadoEn = Ahora();   // DEFAULT now() de la columna
        _tabla[parametro.Clave] = parametro;
        return Task.CompletedTask;
    }

    public Task<int> ActualizarAsync(string clave, Dictionary<string, object?> datos)
    {
        LlamadasTotales++;
        if (!_tabla.TryGetValue(clave, out var actual))
            return Task.FromResult(0);

        // Igual que el repositorio real: mezcla lo enviado sobre la fila
        // guardada y "llama al SP" con el conjunto completo.
        if (datos.TryGetValue("valor", out var v)) actual.Valor = (string)v!;
        if (datos.TryGetValue("tipo_dato", out var t)) actual.TipoDato = (string)t!;
        if (datos.TryGetValue("descripcion", out var d)) actual.Descripcion = (string)d!;
        if (datos.TryGetValue("fk_usuario_modifica", out var u)) actual.FkUsuarioModifica = u is null ? null : Convert.ToInt64(u);
        actual.ModificadoEn = Ahora();      // sp_actualizar_parametro: modificado_en = now()
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(string clave)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.Remove(clave) ? 1 : 0);
    }
}

/// <summary>Falso de 'bitacora': solo lectura, como el contrato real. Las
/// filas "las pusieron los triggers" antes de empezar la prueba.</summary>
class RepositorioBitacoraFalso : IRepositorioBitacora
{
    private readonly List<Bitacora> _tabla =
    [
        new Bitacora { IdBitacora = 1, OcurridoEn = new DateTime(2026, 8, 13, 18, 0, 0, DateTimeKind.Utc), UsuarioBd = "sa",
            NombreTabla = "registro_asistencia", Operacion = "INSERT", Llave = "1",
            DatosDespues = System.Text.Json.JsonDocument.Parse("{\"id_registro\": 1}").RootElement },
        new Bitacora { IdBitacora = 2, OcurridoEn = new DateTime(2026, 8, 13, 18, 1, 0, DateTimeKind.Utc), UsuarioBd = "sa",
            NombreTabla = "registro_asistencia", Operacion = "INSERT", Llave = "2" },
        new Bitacora { IdBitacora = 3, OcurridoEn = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc), UsuarioBd = "sa",
            NombreTabla = "asistente", Operacion = "UPDATE", Llave = "2" },
    ];

    public Task<List<Bitacora>> ObtenerTodosAsync(string? nombreTabla, int limite) =>
        Task.FromResult(_tabla
            .Where(b => nombreTabla is null || b.NombreTabla == nombreTabla)
            .OrderByDescending(b => b.OcurridoEn).ThenByDescending(b => b.IdBitacora)
            .Take(limite)
            .ToList());

    public Task<Bitacora?> ObtenerPorIdAsync(long idBitacora) =>
        Task.FromResult(_tabla.FirstOrDefault(b => b.IdBitacora == idBitacora));
}

/// <summary>Falso de 'pregunta' (v6). Guarda las opciones iniciales que le
/// llegan en la misma llamada para comprobar la Regla 1.</summary>
class RepositorioPreguntaFalso : IRepositorioPregunta
{
    private readonly List<Pregunta> _tabla = new();
    private long _siguiente = 1;
    public int LlamadasTotales { get; private set; }
    public IReadOnlyList<OpcionPregunta> UltimasOpciones { get; private set; } = [];

    public Task<List<Pregunta>> ObtenerTodosAsync(long? fkEncuesta, int limite)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.Where(p => fkEncuesta is null || p.FkEncuesta == fkEncuesta).Take(limite).ToList());
    }

    public Task<Pregunta?> ObtenerPorIdAsync(long idPregunta)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.FirstOrDefault(p => p.IdPregunta == idPregunta));
    }

    public Task<long> CrearAsync(Pregunta pregunta, IReadOnlyList<OpcionPregunta> opcionesIniciales)
    {
        LlamadasTotales++;
        pregunta.IdPregunta = _siguiente++;
        _tabla.Add(pregunta);
        UltimasOpciones = opcionesIniciales;
        return Task.FromResult(pregunta.IdPregunta);
    }

    public Task<int> ActualizarAsync(long idPregunta, Dictionary<string, object?> datos)
    {
        LlamadasTotales++;
        var p = _tabla.FirstOrDefault(x => x.IdPregunta == idPregunta);
        if (p is null) return Task.FromResult(0);
        if (datos.TryGetValue("enunciado", out var e)) p.Enunciado = (string)e!;
        if (datos.TryGetValue("orden", out var o)) p.Orden = Convert.ToInt16(o);
        if (datos.TryGetValue("fk_tipo_pregunta", out var t)) p.FkTipoPregunta = (string)t!;
        if (datos.TryGetValue("obligatoria", out var ob)) p.Obligatoria = (bool)ob!;
        if (datos.TryGetValue("valor_min", out var mi)) p.ValorMin = mi is null ? null : Convert.ToInt16(mi);
        if (datos.TryGetValue("valor_max", out var ma)) p.ValorMax = ma is null ? null : Convert.ToInt16(ma);
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idPregunta)
    {
        LlamadasTotales++;
        return Task.FromResult(_tabla.RemoveAll(p => p.IdPregunta == idPregunta));
    }
}

/// <summary>Falso de 'opcion_pregunta' (v6).</summary>
class RepositorioOpcionPreguntaFalso : IRepositorioOpcionPregunta
{
    private readonly List<OpcionPregunta> _tabla = new();
    private long _siguiente = 1;

    public Task<List<OpcionPregunta>> ObtenerTodosAsync(long? fkPregunta, int limite) =>
        Task.FromResult(_tabla.Where(o => fkPregunta is null || o.FkPregunta == fkPregunta).Take(limite).ToList());

    public Task<OpcionPregunta?> ObtenerPorIdAsync(long idOpcion) =>
        Task.FromResult(_tabla.FirstOrDefault(o => o.IdOpcion == idOpcion));

    public Task<long> CrearAsync(OpcionPregunta opcion)
    {
        opcion.IdOpcion = _siguiente++;
        _tabla.Add(opcion);
        return Task.FromResult(opcion.IdOpcion);
    }

    public Task<int> ActualizarAsync(long idOpcion, Dictionary<string, object?> datos)
    {
        var o = _tabla.FirstOrDefault(x => x.IdOpcion == idOpcion);
        if (o is null) return Task.FromResult(0);
        if (datos.TryGetValue("etiqueta", out var e)) o.Etiqueta = (string)e!;
        if (datos.TryGetValue("valor", out var v)) o.Valor = v is null ? null : Convert.ToInt16(v);
        if (datos.TryGetValue("orden", out var or)) o.Orden = Convert.ToInt16(or);
        return Task.FromResult(1);
    }

    public Task<int> EliminarAsync(long idOpcion) =>
        Task.FromResult(_tabla.RemoveAll(o => o.IdOpcion == idOpcion));
}

/// <summary>Falso de 'respuesta_encuesta' (v6): "responder" crea el maestro
/// y sus ítems juntos, en la lista que comparte con el falso de ítems.</summary>
class RepositorioRespuestaEncuestaFalso : IRepositorioRespuestaEncuesta
{
    private readonly List<RespuestaEncuesta> _tabla = new();
    private readonly List<RespuestaItem> _items;
    private long _siguiente = 1;
    private long _siguienteItem = 1;

    public RepositorioRespuestaEncuestaFalso(List<RespuestaItem> items) { _items = items; }

    public Task<List<RespuestaEncuesta>> ObtenerTodosAsync(long? fkEncuesta, int limite) =>
        Task.FromResult(_tabla.Where(r => fkEncuesta is null || r.FkEncuesta == fkEncuesta).Take(limite).ToList());

    public Task<RespuestaEncuesta?> ObtenerPorIdAsync(long idRespuesta) =>
        Task.FromResult(_tabla.FirstOrDefault(r => r.IdRespuesta == idRespuesta));

    public Task<long> ResponderAsync(long fkRegistro, IReadOnlyList<ItemRespuesta> respuestas)
    {
        var r = new RespuestaEncuesta
        {
            IdRespuesta = _siguiente++, FkRegistro = fkRegistro, FkEncuesta = 1,
            RespondidaEn = DateTimeOffset.UtcNow, Completa = true,
        };
        _tabla.Add(r);
        foreach (var it in respuestas)
            _items.Add(new RespuestaItem
            {
                IdItem = _siguienteItem++, FkRespuesta = r.IdRespuesta, FkPregunta = it.Pregunta!.Value,
                ValorNumerico = it.Numero, ValorTexto = it.Texto, FkOpcion = it.Opcion,
            });
        return Task.FromResult(r.IdRespuesta);
    }
}

/// <summary>Falso de 'respuesta_item' (v6): solo lectura.</summary>
class RepositorioRespuestaItemFalso : IRepositorioRespuestaItem
{
    private readonly List<RespuestaItem> _items;
    public RepositorioRespuestaItemFalso(List<RespuestaItem> items) { _items = items; }

    public Task<List<RespuestaItem>> ObtenerTodosAsync(long? fkRespuesta, int limite) =>
        Task.FromResult(_items.Where(i => fkRespuesta is null || i.FkRespuesta == fkRespuesta).Take(limite).ToList());

    public Task<RespuestaItem?> ObtenerPorIdAsync(long idItem) =>
        Task.FromResult(_items.FirstOrDefault(i => i.IdItem == idItem));
}

// ==========================================================================
//  v7.1 · D52 · Repositorio FALSO de 'Disponibilidad'. Guarda los últimos
//  parámetros recibidos y simula una sola sesión ocupada: la de las 14:00 a
//  las 16:00 del 20-oct-2026 (hora de Colombia) en SAN_BENITO, donde participa el ponente 7.
// ==========================================================================
class RepositorioDisponibilidadFalso : IRepositorioDisponibilidad
{
    private static readonly DateTimeOffset Ocupada = new(2026, 10, 20, 14, 0, 0, TimeSpan.FromHours(-5));

    public int Consultas { get; private set; }
    public string? UltimaSede { get; private set; }
    public string? UltimoLugar { get; private set; }
    public long? UltimoAsistente { get; private set; }
    public int UltimoLimite { get; private set; }

    private static SesionOcupada Sesion(List<string>? motivos) => new()
    {
        IdSesion = 1, FkCatedra = 10, NombreCatedra = "Cátedra ocupada", NumeroReunion = 1,
        Inicio = Ocupada.UtcDateTime, Fin = Ocupada.AddHours(2).UtcDateTime,
        FkSede = "SAN_BENITO", Lugar = "Aula 101", FkModalidad = "PRESENCIAL", Estado = "PROGRAMADA",
        Motivos = motivos,
    };

    public Task<List<SesionOcupada>> ConflictosAsync(
        DateTimeOffset inicio, DateTimeOffset fin, string? fkSede, string? lugar, long? fkAsistente, long? excluirSesion)
    {
        Consultas++;
        UltimaSede = fkSede;
        UltimoLugar = lugar;
        UltimoAsistente = fkAsistente;
        bool traslapa = inicio < Ocupada.AddHours(2) && fin > Ocupada;
        return Task.FromResult(traslapa ? new List<SesionOcupada> { Sesion(["LUGAR", "PONENTE"]) } : new List<SesionOcupada>());
    }

    public Task<List<SesionOcupada>> AgendaAsync(DateTimeOffset desde, DateTimeOffset hasta, string? fkSede, int limite)
    {
        Consultas++;
        UltimoLimite = limite;
        bool toca = desde < Ocupada.AddHours(2) && hasta > Ocupada;
        return Task.FromResult(toca ? new List<SesionOcupada> { Sesion(null) } : new List<SesionOcupada>());
    }
}

// ==========================================================================
//  v7.1 · D53 · Repositorio FALSO de 'QrSesion'. La sesión 7 existe y termina a las
//  18:00 UTC del 20-oct-2026; emitir crea un QR con ventana [17:45, 18:00); regenerar
//  revoca el vigente. La asistencia simulada: 2 personas que registraron.
// ==========================================================================
class RepositorioQrSesionFalso : IRepositorioQrSesion
{
    public static readonly DateTime FinSesion = new(2026, 10, 20, 18, 0, 0, DateTimeKind.Utc);

    private QrSesion? _vigente;
    private long _siguiente = 1;

    public long? UltimoUsuario { get; private set; }
    public int Revocados { get; private set; }

    public Task<QrSesion?> EmitirAsync(long fkSesion, long? fkUsuario, bool regenerar)
    {
        if (fkSesion != 7) return Task.FromResult<QrSesion?>(null);
        UltimoUsuario = fkUsuario ?? UltimoUsuario;

        if (_vigente is not null && !regenerar)
        {
            _vigente.YaExistia = true;
            return Task.FromResult<QrSesion?>(_vigente);
        }
        if (_vigente is not null) Revocados++;

        _vigente = new QrSesion
        {
            IdEnlace = _siguiente++, FkSesion = 7, Token = Guid.NewGuid(),
            VentanaDesde = FinSesion.AddMinutes(-15), VentanaHasta = FinSesion,
            YaExistia = false,
        };
        return Task.FromResult<QrSesion?>(_vigente);
    }

    public Task<QrSesion?> ConsultarAsync(long fkSesion) =>
        Task.FromResult(fkSesion == 7 ? _vigente : null);

    public Task<List<AsistenciaSesion>> AsistenciaAsync(long fkSesion) => Task.FromResult(new List<AsistenciaSesion>
    {
        new() { FkAsistente = 1, Nombres = "Ana", Apellidos = "A", RegistradoEn = FinSesion.AddMinutes(-5), Latitud = 6.24m, Longitud = -75.58m, Origen = "QR" },
        new() { FkAsistente = 3, Nombres = "Cami", Apellidos = "C", RegistradoEn = FinSesion.AddMinutes(-3), Origen = "QR" },
    });
}
