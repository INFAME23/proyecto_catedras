# Plan técnico — Versión 1: Sede, Encuesta, EstadoProceso, Modalidad + PostgreSQL (C#/ASP.NET Core)

> **Versión 1** · CÓMO construir lo especificado en [2_spec.md](2_spec.md).
> El porqué de cada decisión: [4_research.md](4_research.md) · contratos
> de integración: [6_contracts.md](6_contracts.md) · orden:
> [8_tasks.md](8_tasks.md).

---

## 1. Stack

| Pieza | Elección | Por qué |
|---|---|---|
| Lenguaje / framework | **C# sobre ASP.NET Core (.NET 10)** | El stack del curso; controladores con atributos, DI integrada, async nativo |
| Acceso a datos | **Dapper** sobre `Npgsql`, con SQL parametrizado a mano | SQL visible — Dapper mapea fila→objeto pero NO genera SQL (Art. 2, D1) |
| Validación | **Una petición por verbo** con anotaciones (`[Required]`, `[StringLength]`…) | El framework valida el body contra la petición y responde 422 — la petición ES la frontera |
| Motor (v1) | **PostgreSQL 16-alpine** (contenedor oficial) | El motor natural del ecosistema .NET; liviano y amigo de Docker (D6) |
| Contenedor de la API | `mcr.microsoft.com/dotnet/sdk:10.0` + `dotnet watch` | Guardar un `.cs` recompila y reinicia solo (ciclo de desarrollo del curso) |

## 2. Estructura de carpetas

```
(raíz del proyecto)
├── docker-compose.yml                # UN comando: postgres + api (crece por versiones)
├── db/
│   └── postgres/
│       └── script_bdcatedras.sql     # la BD completa, PROVISTA (se copia, no se genera)
└── api_catedras/
    ├── ApiCatedras.csproj             # el proyecto .NET (paquetes: Npgsql, Dapper, Swashbuckle)
    ├── Program.cs                     # punto de entrada: ENSAMBLADOR (DI) + 422 + rutas
    ├── appsettings.json                # cadena de conexión (default localhost:15542)
    ├── Dockerfile                      # sdk:10.0 + dotnet watch (puerto interno 8042)
    ├── Modelos/
    │   ├── Sede.cs                    # el MODELO = la ENTIDAD: 5 propiedades tipadas
    │   ├── Encuesta.cs                # 6 propiedades; IdEncuesta lo pone la BD
    │   ├── EstadoProceso.cs           # 4 propiedades
    │   └── Modalidad.cs               # 4 propiedades
    ├── Peticiones/
    │   ├── SedeCrear.cs               # petición del POST (todo obligatorio)
    │   ├── SedeReemplazo.cs           # petición del PUT (todo obligatorio, sin código)
    │   ├── SedeActualizar.cs          # petición del PATCH (todo opcional)
    │   ├── EncuestaCrear.cs / EncuestaReemplazo.cs / EncuestaActualizar.cs
    │   ├── EstadoProcesoCrear.cs / EstadoProcesoReemplazo.cs / EstadoProcesoActualizar.cs
    │   └── ModalidadCrear.cs / ModalidadReemplazo.cs / ModalidadActualizar.cs
    ├── Controllers/
    │   ├── SedeController.cs          # HTTP: atributos de verbo, try/catch → códigos
    │   ├── EncuestaController.cs
    │   ├── EstadoProcesoController.cs
    │   └── ModalidadController.cs
    ├── Servicios/
    │   ├── IServicioSede.cs / ServicioSede.cs
    │   ├── IServicioEncuesta.cs / ServicioEncuesta.cs
    │   ├── IServicioEstadoProceso.cs / ServicioEstadoProceso.cs
    │   └── IServicioModalidad.cs / ServicioModalidad.cs
    ├── Repositorios/
    │   ├── IRepositorioSede.cs / RepositorioSedePostgres.cs
    │   ├── IRepositorioEncuesta.cs / RepositorioEncuestaPostgres.cs
    │   ├── IRepositorioEstadoProceso.cs / RepositorioEstadoProcesoPostgres.cs
    │   ├── IRepositorioModalidad.cs / RepositorioModalidadPostgres.cs
    │   └── ManejadorTipoDateOnly.cs   # puente Dapper <-> DateOnly (§4.7)
    ├── Excepciones/
    │   └── NoEncontradoExcepcion.cs   # la excepción de negocio que el controller vuelve 404 (compartida por las 4 entidades)
    └── pruebas/
        ├── PruebaCapas.csproj         # proyecto de consola aparte (criterio 6)
        └── Programa.cs                # los 4 servicios con repositorios falsos, sin BD
```

**⏳ Pendiente (próximo commit):** el mismo patrón de 5 archivos por
entidad (Modelo, 3 Peticiones, Repositorio×2, Servicio×2, Controller) se
repite para `PeriodoAcademico`, `Rol`, `TipoDocumento`, `TipoEvento`,
`TipoPregunta` y `TipoVinculacion` (RF26-RF61 de
[2_spec.md](2_spec.md), columnas en §2.5-2.10 de
[5_data_model.md](5_data_model.md)) — ninguno de esos archivos existe
todavía. `PeriodoAcademico` reutiliza `ManejadorTipoDateOnly.cs` (ya
existe, no hay que tocarlo).

**Frontend (`front_catedras/`):** proyecto Flask **aparte**, en la raíz
del repo junto a `api_catedras/` (no dentro de él). Consume `ApiCatedras`
por HTTP (`servicios/api_catedras.py`) para el CRUD de las 4 entidades
construidas; usa la identidad visual de la Universidad San Buenaventura
Medellín (Bootstrap 5 + CSS institucional + escudo). Vive fuera de
`docker-compose.yml` por ahora (se arranca con `python run.py`) y fuera
del alcance de este documento, que planea `ApiCatedras`.

## 3. Arquitectura en capas (flujo de una petición)

```
HTTP → ASP.NET routing        (los atributos [HttpGet]/[HttpPost]… deciden el método)
     → validación de la PETICIÓN (anotaciones de la petición del verbo → 422 automático)
     → SedeController     (try/catch: traduce excepciones a códigos HTTP)
     → IServicioSede      (interfaz — reglas de negocio)
     → IRepositorioSede   (interfaz — el servicio no sabe qué motor hay detrás)
     → RepositorioSedePostgres (Dapper + parámetros @)
     → PostgreSQL
```

**Regla de dependencias:** controller → servicio → interfaz de repositorio.
Solo el ENSAMBLADOR (la sección de DI de `Program.cs`) conoce clases
concretas.

## 4. Decisiones de diseño clave

### 4.1 Interfaces de C# desde v1
```csharp
public interface IRepositorioSede
{
    Task<List<Sede>> ObtenerTodosAsync(int limite);              // lista de objetos Sede
    Task<Sede?> ObtenerPorIdAsync(string idSede);                // el modelo, o null
    Task CrearAsync(Sede sede);                                  // recibe el modelo
    Task<int> ActualizarAsync(string idSede, Dictionary<string, object?> datos); // PUT y PATCH
    Task<int> EliminarAsync(string idSede);
}
```
El servicio recibe **la interfaz** por constructor (la inyecta el
ensamblador). Esto es lo que compra el futuro: un segundo motor será otra
clase con `: IRepositorioSede`. Las lecturas devuelven **objetos del
modelo**; `ActualizarAsync` va con diccionario porque un PATCH puede traer
solo algunos campos.

### 4.2 La validación vive en las PETICIONES (una por verbo)
ASP.NET valida el body contra la petición del verbo ANTES de ejecutar el
método del controlador — el 422 sale solo (personalizado en `Program.cs`
para responder `{estado, mensaje, errores:[…]}`):

- `SedeCrear`      → POST: todos obligatorios (con `id_sede`)
- `SedeReemplazo`  → PUT: todos obligatorios (el `id_sede` va en la URL)
- `SedeActualizar` → PATCH: todos opcionales (se valida lo que llegue)

Reglas: `id_sede` 1–15 caracteres · `nombre` no vacío, hasta 80 ·
`direccion` opcional, hasta 200 · `es_virtual`/`activo` booleanos.
**El tipo también es regla**: `es_virtual`/`activo` son `bool?` — un
`"texto"` no encaja y cae en 422. (El body vacío en PATCH es 400 y lo
decide el **servicio**: no es un problema de forma sino de regla de
negocio.)

### 4.3 El ensamblador: la sección de DI de Program.cs
```csharp
builder.Services.AddScoped<IRepositorioSede>(
    _ => new RepositorioSedePostgres(cadenaConexion));
builder.Services.AddScoped<IServicioSede, ServicioSede>();
```
Sin fábrica multi-motor ni selección: v1 tiene UN motor y el código lo
dice. Cuando exista un segundo motor, **solo esta sección** se convierte
en la fábrica real — controllers y servicios no se tocan.

### 4.4 SQL del repositorio (Dapper, siempre parametrizado)
Ejemplo real (`RepositorioSedePostgres`):
```sql
SELECT id_sede, nombre, direccion, es_virtual, activo
FROM catedras.sede ORDER BY id_sede LIMIT @limite;

SELECT id_sede, nombre, direccion, es_virtual, activo
FROM catedras.sede WHERE id_sede = @idSede;

INSERT INTO catedras.sede (id_sede, nombre, direccion, es_virtual, activo)
VALUES (@IdSede, @Nombre, @Direccion, @EsVirtual, @Activo);

UPDATE catedras.sede SET <col> = @<col>, ...
WHERE id_sede = @id_clave;   -- los campos que lleguen (PUT: los 4; PATCH: los enviados)

DELETE FROM catedras.sede WHERE id_sede = @idSede;
```
- `LIMIT @limite` es el Top-N del dialecto PostgreSQL (va al FINAL y
  acepta parámetro).
- Dapper ejecuta ese SQL tal cual: `QueryAsync<Sede>` para lecturas
  (mapea columna→propiedad por nombre, gracias a
  `DefaultTypeMap.MatchNamesWithUnderscores = true` en `Program.cs`) y
  `ExecuteAsync` para escrituras (devuelve filas afectadas). Conexión por
  operación con `await using`; todo `async`.
- El SET del UPDATE se arma solo con columnas de una lista blanca
  (`ColumnasActualizables`), nunca con claves crudas del cliente.
- Detalle amable del motor: en PostgreSQL, las filas afectadas de un UPDATE
  cuentan las que CUMPLIERON el WHERE (aunque el valor nuevo sea igual al
  viejo) — un PATCH con el mismo valor reporta 1 fila, sin trucos.

### 4.5 Traducción de excepciones a HTTP (en el controller)
| Situación | HTTP |
|---|---|
| (Body con errores de forma — lo responde el framework con la lista) | 422 |
| `ArgumentException` (regla de negocio: límite ≤ 0, body vacío en PATCH) | 400 |
| `NoEncontradoExcepcion` (código inexistente) | 404 |
| `NpgsqlException` y cualquier otra | 500 (mensaje del motor en `detalle`) |

Cada método del controller lleva su propio `try/catch` plano, de arriba a
abajo — sin indirecciones.

### 4.6 PostgreSQL se siembra SOLO (sin inicializador)
PostgreSQL ejecuta automáticamente los scripts montados en
`/docker-entrypoint-initdb.d/` la PRIMERA vez (cuando su volumen está
vacío): el compose monta `db/postgres/script_bdcatedras.sql` ahí y no
necesita ningún contenedor extra. La API arranca con `depends_on:
condition: service_healthy` — cuando la BD ya RESPONDE (y por tanto ya se
sembró).

### 4.7 `Encuesta`: PK autogenerada por la BD (`RETURNING`)

`id_encuesta` es `GENERATED ALWAYS AS IDENTITY`: el cliente nunca lo
manda. Dos consecuencias sobre el patrón de `Sede`:

- `EncuestaCrear` (la petición del POST) **no tiene** campo `IdEncuesta`.
- `IRepositorioEncuesta.CrearAsync` **devuelve el id**, a diferencia de
  `IRepositorioSede.CrearAsync` (que devuelve `Task` sin valor):
  ```csharp
  Task<long> CrearAsync(Encuesta encuesta);
  ```
  El SQL usa `RETURNING id_encuesta` y Dapper lo lee con
  `QuerySingleAsync<long>` (no `ExecuteAsync`, que solo cuenta filas):
  ```sql
  INSERT INTO catedras.encuesta (nombre, version, vigente_desde, vigente_hasta, activa)
  VALUES (@Nombre, @Version, @VigenteDesde, @VigenteHasta, @Activa)
  RETURNING id_encuesta;
  ```
  El servicio asigna ese valor al modelo antes de devolverlo al
  controlador, que lo agrega a la respuesta: `{estado, mensaje,
  id_encuesta}`.
- El parámetro de ruta de los demás verbos es `long idEncuesta` (no
  `string`, como en `Sede`/`EstadoProceso`/`Modalidad`).
- Los tipos `DATE` de PostgreSQL (`vigente_desde`, `vigente_hasta`)
  mapean a `DateOnly`/`DateOnly?` de C#. **Npgsql sí entiende `DATE` de
  forma nativa, pero Dapper NO trae un manejador para `DateOnly`
  (es un tipo de C# 10, posterior a Dapper)**: sin ayuda, un parámetro
  `@VigenteDesde` falla al enviarse y una columna `DATE` falla al leerse.
  Se resuelve con un `SqlMapper.TypeHandler<DateOnly>` propio
  (`Repositorios/ManejadorTipoDateOnly.cs`), registrado una sola vez en
  `Program.cs` junto a `DefaultTypeMap.MatchNamesWithUnderscores`, para
  `DateOnly` y `DateOnly?`.

### 4.8 Reglas de coherencia entre dos campos: viven en el SERVICIO

`[Required]`/`[StringLength]`/`[Range]` validan **un campo a la vez**;
no pueden expresar "si `es_exitoso` es `true`, `es_final` también debe
serlo" ni "`vigente_hasta` no puede ser anterior a `vigente_desde`". Esas
reglas viven en el **servicio** (`ArgumentException` → 400), mismo lugar
donde ya vive "el PATCH sin campos es un 400" para `Sede` — es la
decisión ya tomada en D5 ([4_research.md](4_research.md)), aplicada de
nuevo:

```csharp
// ServicioEncuesta.CrearAsync / ReemplazarAsync (fechas siempre presentes)
if (vigenteHasta is not null && vigenteHasta < vigenteDesde)
    throw new ArgumentException("'vigente_hasta' no puede ser anterior a 'vigente_desde'.");

// ServicioEstadoProceso.CrearAsync / ReemplazarAsync
if (esExitoso && !esFinal)
    throw new ArgumentException("Un estado 'exitoso' debe ser también 'final'.");
```

En **PATCH**, el servicio solo puede aplicar esta comprobación cuando el
cliente manda las DOS mitades de la regla en la misma petición (si manda
una sola, no tiene con qué comparar sin leer antes la fila — y la v1 no
lee-antes-de-escribir). Si el PATCH deja una combinación inválida con un
solo campo, la última muralla (`chk_encuesta_vig` / `chk_estado_coherente`
en la BD) la detiene con `NpgsqlException` → 500. Es un costo aceptado y
documentado, no un descuido.

### 4.9 Listas cerradas de valores: se validan en la PETICIÓN, no en el servicio

`Modalidad.canal_difusion` solo admite `QR`/`ENLACE`/`AMBOS`
(`chk_modalidad_canal`). A diferencia de 4.8, esto **no** es una relación
entre dos campos: es la forma de un solo campo (pertenece o no a un
conjunto fijo), así que se valida igual que un `[StringLength]` — en la
PETICIÓN, con `[RegularExpression]`:

```csharp
[Required(ErrorMessage = "El campo 'canal_difusion' es obligatorio.")]
[RegularExpression("^(QR|ENLACE|AMBOS)$",
    ErrorMessage = "El campo 'canal_difusion' debe ser 'QR', 'ENLACE' o 'AMBOS'.")]
public string? CanalDifusion { get; set; }
```

Un valor fuera de la lista cae en 422 (frontera de forma), no en 400
(frontera de negocio) — mismo criterio que "el tipo también es regla" de
`Sede`.

## 5. Docker: un solo comando desde v1

La constitución (Artículo 4) manda: `docker compose up -d --build` deja TODO
funcionando. En v1 eso son **dos servicios**: `postgres` (`15542` al
host → `5432` interno, se siembra solo) y `api` (`8142` al host →
`8042` interno, código montado + `dotnet watch`, `bin/` y `obj/` en
volúmenes anónimos para no mezclar compilados de Linux con los de
Windows). El detalle línea por línea está en el `docker-compose.yml` de
la raíz, comentado.
