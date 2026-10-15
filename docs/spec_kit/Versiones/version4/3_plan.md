# Plan técnico — Versión 4: sesiones y ponencias

> ✅ **Revisada y construida (2026-10-08)** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

Sin cambios: C#/ASP.NET Core (.NET 10) + Dapper, sin ORM, sin paquetes
nuevos. **Sin SP nuevo** (a diferencia de v2 y v3): ninguna operación
de v4 es maestro-detalle, así que no hay ninguna migración en
`db/postgres/migraciones/` esta vez.

## 2. Estructura de carpetas

```
api_catedras/
├── Modelos/
│   ├── Sesion.cs      # 16 propiedades; Inicio/Fin/CreadoEn como DateTime en UTC (ADR D21)
│   └── Ponencia.cs    # 3 propiedades, SIN id (§4.2, ADR D19)
├── Peticiones/
│   ├── SesionCrear.cs / SesionReemplazo.cs / SesionActualizar.cs
│   └── PonenciaCrear.cs                       # ⚠️ ÚNICA, como AliasProgramaCrear/ConsentimientoDatosCrear
├── Controllers/
│   ├── SesionController.cs      # 6 endpoints (molde completo)
│   └── PonenciaController.cs    # 4: GET lista, GET/{fkSesion}/{fkAsistente}/{rol}, POST, DELETE/{fkSesion}/{fkAsistente}/{rol}
├── Servicios/
│   ├── IServicioSesion.cs / ServicioSesion.cs       # CrearAsync valida fin > inicio
│   └── IServicioPonencia.cs / ServicioPonencia.cs
└── Repositorios/
    ├── IRepositorioSesion.cs / RepositorioSesionPostgres.cs             # CrearAsync: INSERT ... RETURNING *, deja que la BD calcule numero_reunion
    └── IRepositorioPonencia.cs / RepositorioPonenciaPostgres.cs         # sin ActualizarAsync (§4.2)
```

## 3. Arquitectura en capas

Sin cambios de forma (Artículo 3): `Controller → IServicio → IRepositorio
→ RepositorioXPostgres → BD`. Es la primera versión desde v1 donde
**ningún** repositorio nuevo hace un `CALL` a un SP — los dos son
`INSERT`/`UPDATE`/`DELETE` sencillos, igual que `Facultad`/`Dependencia`
en v2.

## 4. Decisiones de diseño clave

### 4.1 `numero_reunion`: la API no calcula, solo deja pasar (ADR D20)

Ver desarrollo completo en [4_research.md](4_research.md) ADR D20.
`RepositorioSesionPostgres.CrearAsync` manda `numero_reunion` tal cual
venga en la petición (incluido `null`/`0` si el cliente no lo fijó) y
usa `RETURNING numero_reunion` para devolver al cliente el valor que la
BD terminó asignando — igual mecánica que `RepositorioCatedraPostgres`
en v2 con `nombre_asis` (un valor que la BD calcula y el C# solo
refleja de vuelta):

```csharp
const string sql = @"
    INSERT INTO sesion (fk_catedra, numero_reunion, numero_reunion_asis,
                         titulo, inicio, fin, fk_modalidad, fk_sede,
                         fk_periodo, fk_encuesta, lugar, enlace_virtual,
                         cupo, estado)
    VALUES (@FkCatedra, @NumeroReunion, @NumeroReunionAsis, @Titulo,
            @Inicio, @Fin, @FkModalidad, @FkSede, @FkPeriodo,
            @FkEncuesta, @Lugar, @EnlaceVirtual, @Cupo, @Estado)
    RETURNING id_sesion, numero_reunion, creado_en;";
```

Si `numero_reunion` llega ausente desde `SesionCrear`, el servicio pone
`0` (no un número calculado por su cuenta) — es el trigger
`fn_trg_sesion_consecutivo` quien lo reemplaza ANTES del `INSERT` real.
El C# nunca ve el valor intermedio: solo ve lo que `RETURNING` le
devuelve, que ya es el definitivo. Por eso `CrearAsync` devuelve la
tupla `(long IdSesion, short NumeroReunion, DateTime CreadoEn)`.

Consecuencia que no se ve a simple vista: el trigger es `BEFORE INSERT`,
así que en un `UPDATE` **no corre**. `SesionReemplazo` y
`SesionActualizar` exigen `numero_reunion >= 1` (`[Range(1, ...)]` →
422) en vez de dejar que el `CHECK chk_sesion_reunion` responda 500.

### 4.2 `Ponencia`: llave compuesta SIN el patrón de vigencia de v3 (ADR D19)

Ver desarrollo completo en [4_research.md](4_research.md) ADR D19.
Interfaz (3 métodos, igual cantidad que `IRepositorioVinculacionAsistente`
de v3, pero con `DELETE` en vez de `CERRAR`):

```csharp
public interface IRepositorioPonencia
{
    Task<List<Ponencia>> ObtenerTodosAsync(long? fkSesion, long? fkAsistente, int limite);
    Task<Ponencia?> ObtenerUnoAsync(long fkSesion, long fkAsistente, string rol);  // para el GET puntual, RF128
    Task CrearAsync(Ponencia ponencia);                                           // INSERT; PK duplicada -> 500
    Task<int> EliminarAsync(long fkSesion, long fkAsistente, string rol);         // DELETE; 0 filas -> NoEncontradoExcepcion
}
```

`PonenciaController` expone la llave compuesta como **3 segmentos de
ruta** (`[HttpGet("{fkSesion}/{fkAsistente}/{rol}")]`,
`[HttpDelete("{fkSesion}/{fkAsistente}/{rol}")]`), no como querystring
ni como body — a diferencia de `VinculacionAsistenteCerrar` de v3 (que
sí manda la llave en el body de un `PATCH`), aquí el verbo HTTP que
corresponde (`DELETE`, `GET` puntual) ya acepta identificar el recurso
por la URL, así que no hace falta inventar una ruta de acción como
`/cerrar`.

### 4.3 Fechas con hora: zona en la entrada, UTC adentro (ADR D21)

Ver [4_research.md](4_research.md) ADR D21. Las peticiones declaran
`inicio`/`fin` como `DateTimeOffset?` (aceptan `-05:00`); el servicio
hace `.UtcDateTime` y el modelo `Sesion` guarda `DateTime` con
`Kind = Utc` — igual que `CreadoEn` en todas las tablas desde la v2.
Así Dapper (que no fija `DbType` para `DateTime`) y Npgsql (que infiere
`timestamptz` para un `DateTime` UTC) escriben sin conversiones. El
PATCH hace la misma conversión dentro de `ACamposEnviados()`.

## 5. Frontend: calendario de cátedra, detalle de sesión y ponente

| Pantalla | Archivo(s) | Llamadas a la API |
|---|---|---|
| Calendario dentro del detalle de la cátedra | `routes/catedras.py` · `templates/detalle_catedra.html` | `GET /api/Sesion?fkCatedra={id}` |
| Detalle de sesión + ponentes (asignar / quitar) | `routes/sesiones.py` · `templates/detalle_sesion.html` | `GET /api/Sesion/{id}`, `GET /api/Ponencia?fkSesion=`, `GET /api/Asistente?q=` (búsqueda), `POST /api/Ponencia`, `DELETE /api/Ponencia/{s}/{a}/{rol}` |
| Calendario del ponente (sin login hasta v5) | `routes/ponente.py` · `templates/ponente_calendario.html` | `GET /api/Ponencia?fkAsistente=`, `GET /api/Sesion/{id}`, `GET /api/Catedra/{id}` |
| Admin: Sesión (CRUD) y Ponencia (asignar/quitar) | `routes/admin.py` · `templates/admin/sesion_*.html`, `ponencia_lista.html` | las anteriores + `PUT`/`DELETE /api/Sesion/{id}` |

Piezas compartidas: `servicios/fechas.py` (UTC ↔ hora de Colombia, filtros
Jinja `|fecha_hora` y `|para_input`), `servicios/api_catedras.llave()`
(arma `10/167/PONENTE` para la ruta de llave compuesta) y
`templates/partials/macros_sesion.html` (badge de estado y bloque de
ponentes, usado por la página pública y por el admin).

**Regla 2 (FK = select):** `fk_catedra` de una sesión lo fija la URL
(`/admin/sesiones/nueva?fk_catedra=`), igual que `fk_asistente` en v3;
`fk_asistente` de una ponencia sale de un `<select>` llenado con una
búsqueda (`?q=`); modalidad, sede, periodo y encuesta, de `<select>` con
el catálogo completo.

## 6. Docker: sin cambios de infraestructura

Sigue siendo `postgres` + `api`, **sin ninguna migración nueva** — a
diferencia de v2 y v3, que agregaron SP (RNF de esta versión: ningún
`CREATE PROCEDURE` en `db/postgres/migraciones/` esta vez).
