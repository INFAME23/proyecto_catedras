# Plan técnico — Versión 1: Sede + PostgreSQL (C#/ASP.NET Core)

> **Versión 1** · CÓMO construir lo especificado en [2_spec.md](2_spec.md).
> El porqué de cada decisión: [4_research.md](4_research.md) · contratos
> exactos: [6_contracts.md](6_contracts.md) · orden: [8_tasks.md](8_tasks.md).

---

## 1. Stack

| Pieza | Elección | Por qué |
|---|---|---|
| Lenguaje / framework | **C# sobre ASP.NET Core (.NET 10)** | El stack del curso; controladores con atributos, DI integrada, async nativo |
| Acceso a datos | **Dapper** sobre `Npgsql`, con SQL parametrizado a mano | SQL visible — Dapper mapea fila→objeto pero NO genera SQL (Art. 2, D1) |
| Validación | **Una petición por verbo** con anotaciones (`[Required]`, `[Range]`…) | El framework valida el body contra la petición y responde 422 — la petición ES la frontera |
| Motor (v1) | **PostgreSQL 2022** (contenedor oficial) | El motor natural del ecosistema .NET; los otros llegan en v3/v4 |
| Contenedor de la API | `mcr.microsoft.com/dotnet/sdk:10.0` + `dotnet watch` | Guardar un `.cs` recompila y reinicia solo (ciclo de desarrollo del curso) |

## 2. Estructura de carpetas

```
(raíz del proyecto)
├── docker-compose.yml                # UN comando: postgres + api (crece por versiones)
├── db/
│   └── bdfacturas_postgres.sql       # la BD completa, PROVISTA (se copia, no se genera)
└── api_facturas/
    ├── ApiFacturas.csproj            # el proyecto .NET (paquetes: Npgsql y Swashbuckle)
    ├── Program.cs                    # punto de entrada: ENSAMBLADOR (DI) + 422 + rutas
    ├── appsettings.json              # cadena de conexión (default localhost:15442)
    ├── Dockerfile                    # sdk:10.0 + dotnet watch (puerto 8042)
    ├── Modelos/
    │   └── Sede.cs               # el MODELO = la ENTIDAD: 4 propiedades tipadas
    ├── Peticiones/
    │   ├── SedeCrear.cs          # petición del POST (todo obligatorio)
    │   ├── SedeReemplazo.cs      # petición del PUT (todo obligatorio, sin código)
    │   └── SedeActualizar.cs     # petición del PATCH (todo opcional)
    ├── Controllers/
    │   └── SedeController.cs     # HTTP: atributos de verbo, try/catch → códigos
    ├── Servicios/
    │   ├── IServicioSede.cs      # interface del servicio
    │   └── ServicioSede.cs       # reglas de negocio; recibe IRepositorioSede
    ├── Repositorios/
    │   ├── IRepositorioSede.cs   # interface: 5 métodos de datos (async)
    │   └── RepositorioSedePostgres.cs   # Dapper + SQL a mano parametrizado
    ├── Excepciones/
    │   └── NoEncontradoExcepcion.cs  # la excepción de negocio que el controller vuelve 404
    └── pruebas/
        ├── PruebaCapas.csproj        # proyecto de consola aparte (criterio 6)
        └── Programa.cs               # el servicio con un repositorio falso, sin BD
```

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
    Task<List<Sede>> ObtenerTodosAsync(int limite);   // lista de objetos Sede
    Task<Sede?> ObtenerPorCodigoAsync(string codigo); // el modelo, o null
    Task CrearAsync(Sede Sede);                   // recibe el modelo
    Task<int> ActualizarAsync(string codigo, Dictionary<string, object> datos); // PUT y PATCH
    Task<int> EliminarAsync(string codigo);
}
```
El servicio recibe **la interfaz** por constructor (la inyecta el
ensamblador). Esto es lo que compra la v3: un segundo motor será otra clase
con `: IRepositorioSede`. Las lecturas devuelven **objetos del modelo**;
`ActualizarAsync` va con diccionario porque un PATCH puede traer solo
algunos campos.

### 4.2 La validación vive en las PETICIONES (una por verbo)
ASP.NET valida el body contra la petición del verbo ANTES de ejecutar el
método del controlador — el 422 sale solo (personalizado en `Program.cs`
para responder `{estado, mensaje, errores:[…]}`):

- `SedeCrear`      → POST: todos obligatorios (con código)
- `SedeReemplazo`  → PUT: todos obligatorios (el código va en la URL)
- `SedeActualizar` → PATCH: todos opcionales (se valida lo que llegue)

Reglas: `codigo` 1–10 caracteres · `nombre` no vacío · `stock` entero ≥ 0 ·
`valorunitario` numérico ≥ 0. **El tipo también es regla**: `stock` es
`int?` — un `7.5` o un `"texto"` no encajan y caen en 422. (El body vacío
en PATCH es 400 y lo decide el **servicio**: no es un problema de forma
sino de regla de negocio.)

### 4.3 El ensamblador: la sección de DI de Program.cs
```csharp
builder.Services.AddScoped<IRepositorioSede>(
    _ => new RepositorioSedePostgres(cadenaConexion));
builder.Services.AddScoped<IServicioSede, ServicioSede>();
```
Sin fábrica multi-motor ni selección: v1 tiene UN motor y el código lo dice.
Cuando v3 agregue PostgreSQL, **solo esta sección** se convierte en la
fábrica real — controllers y servicios no se tocan (ese es el examen de la
v3).

### 4.4 SQL del repositorio (Dapper, siempre parametrizado)
Ejemplo
```sql
SELECT codigo, nombre, stock, valorunitario FROM Sede ORDER BY codigo LIMIT @limite
SELECT … WHERE codigo = @codigo
INSERT INTO Sede (codigo, nombre, stock, valorunitario) VALUES (@codigo, @nombre, @stock, @valorunitario)
UPDATE Sede SET … WHERE codigo = @codigo_clave   -- los campos que lleguen (PUT: los 3; PATCH: los enviados)
DELETE FROM Sede WHERE codigo = @codigo
```
- `LIMIT @limite` es el Top-N del dialecto PostgreSQL (va al FINAL y
  acepta parámetro).
- Dapper ejecuta ese SQL tal cual: `QueryAsync<Sede>` para lecturas
  (mapea columna→propiedad por nombre) y `ExecuteAsync` para escrituras
  (devuelve filas afectadas). Conexión por operación con `await using`;
  todo `async`.
- El SET del UPDATE se arma solo con columnas que salen de las PETICIONES
  (lista blanca), nunca con claves del cliente.
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
vacío): el compose monta `db/bdfacturas_postgres.sql` ahí y no necesita
ningún contenedor extra. La API arranca con `depends_on: condition:
service_healthy` — cuando la BD ya RESPONDE (y por tanto ya se sembró).
(Otros motores, como SQL Server, NO tienen este mecanismo y exigen un
contenedor inicializador: esa lección llegará con el segundo motor.)

## 5. Docker: un solo comando desde v1

La constitución (Artículo 4) manda: `docker compose up -d --build` deja TODO
funcionando. En v1 eso son **dos servicios**: `postgres` (15442 al host,
se siembra solo) y `api-facturas` (8042, código montado +
`dotnet watch`, `bin/` y `obj/` en volúmenes anónimos para no mezclar
compilados de Linux con los de Windows). El detalle línea por línea está en
el `docker-compose.yml` de la raíz, comentado.