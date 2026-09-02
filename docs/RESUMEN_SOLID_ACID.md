# Resumen — SOLID, ACID y el flujo de una petición (Proyecto Cátedras · v1)

Este documento resume los comentarios `[SOLID · x]`, `[ACID · x]` y `[FLUJO n]`
que están repartidos por el código de `api_catedras/`. Sirve como mapa: cada
sección dice **qué principio es**, **dónde vive en el código** y **cómo se
comprueba**.

---

## 1. El flujo de una petición (paso a paso)

Ruta de ejemplo: `PATCH /api/Sede/BELLO` con body `{ "activo": true }`.

| Paso | Dónde | Qué pasa |
|---|---|---|
| **0** | `Program.cs` (arranque) | El contenedor de Inyección de Dependencias queda cargado: `IServicioSede → ServicioSede`, `IRepositorioSede → RepositorioSedePostgres` (con la cadena de conexión). Es la **única** parte que nombra clases concretas (*Composition Root*). |
| **1** | `Program.cs` → `app.MapControllers()` + atributos en `SedeController` | Llega el HTTP. El enrutador elige el método por `[HttpPatch("{idSede}")]`. |
| **2** | `[ApiController]` + `Peticiones/SedeActualizar.cs` + fábrica 422 de `Program.cs` | El framework deserializa el body a `SedeActualizar` y corre sus anotaciones (`[StringLength]`, tipo `bool?`…). **Si falla → 422** con `{ estado, mensaje, errores:[...] }` y el controlador **nunca se ejecuta**. La base de datos no se toca. |
| **3** | `SedeController.Actualizar(...)` | El método recibe la petición ya validada y llama a `_servicio.ActualizarAsync(idSede, peticion)` **por la interfaz**. |
| **4** | `ServicioSede.ActualizarAsync(...)` | Reglas de **negocio**: `peticion.ACamposEnviados()` arma el diccionario `columna → valor` solo con lo que llegó; si queda vacío → `ArgumentException` (→ 400). Transforma la petición en lo que el repositorio entiende. |
| **5** | `ServicioSede` → `_repositorio.ActualizarAsync(...)` | Baja a la capa de datos **por la interfaz** `IRepositorioSede`. |
| **6** | `RepositorioSedePostgres.ActualizarAsync(...)` | Arma el `UPDATE ... SET activo = @activo WHERE id_sede = @id_clave` **solo con columnas de la lista blanca**, abre `NpgsqlConnection` (`await using`), ejecuta con Dapper de forma **parametrizada** y obtiene el nº de filas afectadas. |
| **7** | Vuelta hacia arriba | El repositorio devuelve `filas` (p. ej. `1`). Si el motor rechazó algo, sube `NpgsqlException` **sin capturarla**. `ServicioSede` convierte `filas == 0` en `NoEncontradoExcepcion` (→ 404). |
| **8** | `SedeController` (try/catch de cada método) | Traduce el resultado o la excepción a **código HTTP + sobre JSON**: `200 { filasAfectadas }`, `400`, `404`, `500 { estado, mensaje, detalle }`. Fin. |

**Regla de oro de las capas:** cada flecha del flujo cruza una **interfaz**, no
una clase concreta. Por eso una capa se puede reemplazar sin tocar las demás.

```
HTTP → SedeController → IServicioSede → ServicioSede → IRepositorioSede → RepositorioSedePostgres → PostgreSQL
        (traduce HTTP)   (contrato)     (reglas)        (contrato)         (SQL a mano + Dapper)
```

---

## 2. SOLID — dónde se aplica cada principio

### S — Responsabilidad Única (SRP)
Cada tipo tiene **una sola razón para cambiar**:

| Archivo | Su única responsabilidad |
|---|---|
| `Controllers/SedeController.cs` | Traducir HTTP ↔ negocio. No tiene SQL ni reglas. |
| `Servicios/ServicioSede.cs` | Reglas de negocio (`límite > 0`, body de PATCH no vacío, "no existe" → 404). |
| `Repositorios/RepositorioSedePostgres.cs` | Acceso a datos: método → SQL → objeto. |
| `Peticiones/SedeCrear|SedeReemplazo|SedeActualizar` | Describir el body **de un verbo** y su validación de forma. |
| `Modelos/Sede.cs` | Representar una fila de `catedras.sede`. Nada más. |
| `Excepciones/NoEncontradoExcepcion.cs` | Señalar "el recurso no existe". |
| `Program.cs` | Ensamblar y arrancar. |

*Comprobación:* un cambio en el formato del sobre JSON solo toca el controlador;
un cambio en el límite por defecto solo toca el servicio; un cambio de SQL solo
toca el repositorio.

### O — Abierto/Cerrado (OCP)
El sistema está **abierto a extensión, cerrado a modificación**:

- **Segundo motor (v3):** crear `RepositorioSedeMysql : IRepositorioSede` y
  cambiar **solo** la línea de registro en `Program.cs`. Ni el controlador ni el
  servicio se modifican.
- **Nuevo verbo:** añadir un método a `IServicioSede` / `ServicioSede`; los
  existentes no se tocan.
- **Lista blanca `ColumnasActualizables`** en el repositorio: el `UPDATE` acepta
  columnas nuevas agregándolas al `HashSet`, sin reescribir la lógica.

### L — Sustitución de Liskov (LSP)
Cualquier `IRepositorioSede` es **intercambiable** sin que `ServicioSede` lo
note, porque todas las implementaciones respetan el mismo **comportamiento
observable** definido en la interfaz:
- "devuelve `null` si no existe" (`ObtenerPorIdAsync`)
- "devuelve filas afectadas; `0` = no existe" (`ActualizarAsync`, `EliminarAsync`)

*Comprobación:* `api_catedras/pruebas/Programa.cs` le pasa a `ServicioSede` un
`RepositorioSedeFalso` (diccionario en memoria, cero líneas de PostgreSQL) y
**las 9 pruebas pasan igual**. Ejecutar: `dotnet run --project pruebas`.

### I — Segregación de Interfaces (ISP)
Interfaces **pequeñas y cohesionadas**, hechas a la medida del cliente:
- `IServicioSede`: 6 métodos, todos los que un cliente de negocio de `Sede` usa.
- `IRepositorioSede`: 5 operaciones de datos, todas necesarias.
- El controlador **solo** depende de `IServicioSede`; no conoce `IRepositorioSede`.
- PUT y PATCH tienen clases de petición **separadas** (`SedeReemplazo` vs
  `SedeActualizar`): así el mismo body sin `nombre` da 422 en PUT y 200 en PATCH
  sin un solo `if`.

### D — Inversión de Dependencias (DIP)
Los módulos de **alto nivel no dependen de los de bajo nivel**; ambos dependen de
**abstracciones**:

- `SedeController` depende de `IServicioSede` (no de `ServicioSede`).
- `ServicioSede` depende de `IRepositorioSede` (no de `RepositorioSedePostgres`).
- Las clases concretas se "cablean" **solo en `Program.cs`** y entran por
  **constructor** (inyección de dependencias).

*Comprobación:* fuera de `Program.cs`, busca `new ServicioSede` o
`new RepositorioSedePostgres` en el proyecto `ApiCatedras` — no aparecen.

---

## 3. ACID — dónde vive cada garantía

> **Alcance de la v1:** cada operación ejecuta **una sola sentencia SQL**. Por eso
> `ServicioSede` y `RepositorioSedePostgres` **no abren transacciones explícitas**
> (`BEGIN/COMMIT`): PostgreSQL en modo *autocommit* ya envuelve cada sentencia en
> su propia transacción. Si una regla futura tocara 2+ tablas, el `BEGIN/COMMIT`
> se pondría en **`ServicioSede`** (la capa que conoce la "unidad de trabajo").

### A — Atomicidad
Cada `conexion.ExecuteAsync(...)` manda **un** comando; PostgreSQL lo aplica
**entero o nada**:
- `CrearAsync`: el `INSERT` de las 5 columnas se realiza completo o no se realiza.
- `ActualizarAsync`: el `UPDATE ... SET col1 = @c1, col2 = @c2 ...` cambia **todas
  las columnas juntas** (es un solo comando).
- Si el motor lanza `NpgsqlException`, no queda nada "a medias".

### C — Consistencia
La base de datos **nunca queda en un estado que viole sus reglas**. Se defiende en
tres niveles:

| Nivel | Dónde | Qué filtra |
|---|---|---|
| Forma | `Peticiones/*.cs` (anotaciones) → 422 | Tipos y longitudes mal → ni llega al motor. |
| Aplicación | `ColumnasActualizables` (lista blanca) + parámetros `@` (RNF3) | El `UPDATE` no puede tocar columnas desconocidas; el valor del cliente nunca se concatena en el texto SQL (anti-inyección). |
| Motor | Restricciones de `catedras.sede`: `pk_sede`, `uq_sede_nombre`, `NOT NULL`, FK `fk_sesion_sede` `ON DELETE RESTRICT` | `id_sede`/`nombre` duplicado → `NpgsqlException` → 500. Borrar una sede con sesiones → rechazado (no deja filas huérfanas). |

Además: `filasAfectadas == 0` → `NoEncontradoExcepcion` (404). No se inventa una
fila que no existe.

### I — Aislamiento
- Cada operación abre **su propia** `NpgsqlConnection` con `await using` (se abre,
  se usa, se cierra sola).
- Registro `AddScoped`: una instancia de servicio/repositorio **por petición
  HTTP**; dos peticiones simultáneas no comparten estado en memoria.
- PostgreSQL usa el nivel **READ COMMITTED** por defecto: una petición no ve los
  cambios sin confirmar de otra.

### D — Durabilidad
Cuando `ExecuteAsync` **regresa sin excepción**, el `COMMIT` implícito ya está
escrito en el WAL (Write-Ahead Log) en disco: el cambio **sobrevive a un reinicio**
del motor o del contenedor.

---

## 4. Mapa rápido de comentarios en el código

| Marca | Significado | Archivos principales |
|---|---|---|
| `[FLUJO 0..8]` | Paso del recorrido de una petición | `Program.cs`, `SedeController.cs`, `ServicioSede.cs`, `RepositorioSedePostgres.cs` |
| `[SOLID · S]` | Responsabilidad única | todos |
| `[SOLID · O]` | Abierto/Cerrado | `Program.cs`, `IRepositorioSede.cs`, `RepositorioSedePostgres.cs` |
| `[SOLID · L]` | Sustitución de Liskov | `ServicioSede.cs`, `IRepositorioSede.cs`, `pruebas/Programa.cs` |
| `[SOLID · I]` | Segregación de interfaces | `IServicioSede.cs`, `IRepositorioSede.cs`, `Peticiones/*` |
| `[SOLID · D]` | Inversión de dependencias | `Program.cs`, `SedeController.cs`, `ServicioSede.cs` |
| `[ACID · A]` | Atomicidad | `RepositorioSedePostgres.cs`, `ServicioSede.cs` |
| `[ACID · C]` | Consistencia | `SedeController.cs`, `RepositorioSedePostgres.cs`, `Peticiones/*`, `Modelos/Sede.cs` |
| `[ACID · I]` | Aislamiento | `Program.cs`, `RepositorioSedePostgres.cs` |
| `[ACID · D]` | Durabilidad | `RepositorioSedePostgres.cs` |

---

## 5. En una frase

- **SOLID** organiza el **código en capas** unidas por interfaces: cada pieza
  tiene un trabajo y se puede cambiar sola.
- **ACID** lo garantiza **PostgreSQL** en cada sentencia; la aplicación colabora
  validando la forma antes y traduciendo los errores del motor después.
