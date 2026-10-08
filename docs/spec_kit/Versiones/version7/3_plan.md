# Plan técnico — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md).

## 1. Stack

Sin cambios: C#/ASP.NET Core (.NET 10) + Dapper, sin ORM, **sin
paquetes nuevos y sin ningún SP/migración nueva** (ADR D33) — segunda
versión del proyecto, junto con v4, sin ningún `CREATE PROCEDURE`
propio.

## 2. Estructura de carpetas

```
api_catedras/
├── Modelos/
│   ├── LoteMigracion.cs       # 10 propiedades
│   ├── DetalleMigracion.cs    # 8 propiedades
│   ├── Bitacora.cs            # 9 propiedades
│   └── Parametro.cs           # 6 propiedades, PK = Clave (string)
├── Peticiones/
│   ├── LoteMigracionGenerar.cs / LoteMigracionActualizar.cs   # ⚠️ sin Crear/Reemplazo genéricos
│   ├── DetalleMigracionActualizar.cs                          # ⚠️ ÚNICA — sin Crear/Reemplazo
│   └── Parametro{Crear,Reemplazo,Actualizar}.cs
├── Controllers/
│   ├── LoteMigracionController.cs      # GET lista, GET/{id}, POST (=generar), PATCH/{id} — 4
│   ├── DetalleMigracionController.cs   # GET lista, GET/{id}, PATCH/{id} — 3
│   ├── BitacoraController.cs           # GET lista, GET/{id} — 2, SIN ningún verbo de escritura
│   └── ParametroController.cs          # 6 endpoints, molde completo con llave de texto
├── Servicios/
│   ├── IServicioLoteMigracion.cs / ServicioLoteMigracion.cs
│   ├── IServicioDetalleMigracion.cs / ServicioDetalleMigracion.cs
│   ├── IServicioBitacora.cs / ServicioBitacora.cs          # solo lectura
│   └── IServicioParametro.cs / ServicioParametro.cs
└── Repositorios/
    ├── IRepositorioLoteMigracion.cs / RepositorioLoteMigracionPostgres.cs       # GenerarAsync = SELECT * FROM fn_generar_lote_api(...)
    ├── IRepositorioDetalleMigracion.cs / RepositorioDetalleMigracionPostgres.cs # sin CrearAsync
    ├── IRepositorioBitacora.cs / RepositorioBitacoraPostgres.cs                 # solo ObtenerTodosAsync/ObtenerPorIdAsync
    └── IRepositorioParametro.cs / RepositorioParametroPostgres.cs               # CRUD estándar, PK string
```

## 3. Arquitectura en capas

Sin cambios de forma (Artículo 3). Reaparece, por tercera vez (tras v5
y v6), el patrón `SELECT * FROM fn_..._api(...)` para
`LoteMigracion.GenerarAsync` — una función que ya existe desde v1 y
que, a diferencia de `fn_emitir_enlace_api`/`fn_responder_encuesta_api`,
también modifica una fila de OTRA tabla de otra versión
(`sesion.estado`) dentro de la misma transacción (ver §4.2).

## 4. Decisiones de diseño clave

### 4.1 Por qué `LoteMigracion`/`DetalleMigracion` no necesitan ningún SP nuevo (ADR D33)

Ver desarrollo completo en [4_research.md](4_research.md). Resumen:
`sp_generar_lote_migracion` (y su envoltorio `fn_generar_lote_api`) ya
inserta el maestro (`lote_migracion`) y todos los detalles
(`detalle_migracion`) en una sola transacción, a partir de lo que
`fn_exportar_asis` calcula — exactamente la operación que la Guía IA,
Regla 1 exigiría resolver con un SP, solo que **ya está resuelta**.

```csharp
// RepositorioLoteMigracionPostgres.cs
public async Task<long> GenerarAsync(long fkSesion, long? fkUsuario)
{
    const string sql = @"SELECT * FROM catedras.fn_generar_lote_api(@FkSesion, @FkUsuario);";
    return await conexion.ExecuteScalarAsync<long>(sql,
        new { FkSesion = fkSesion, FkUsuario = fkUsuario });
}
```

### 4.2 El efecto colateral sobre `Sesion.estado` no se oculta ni se revierte desde la API

`sp_generar_lote_migracion` hace, como parte de la misma transacción,
`UPDATE sesion SET estado = 'MIGRADA' WHERE id_sesion = p_id_sesion`.
`ServicioLoteMigracion.GenerarAsync` **no** intenta deshacer, verificar
ni duplicar ese cambio desde C# — simplemente deja que ocurra (D9,
llevado a un caso donde el efecto colateral cruza hacia una tabla de
otra versión, no solo hacia otra fila de la misma tabla como en
v2-v4). El repositorio de `Sesion` (v4) no necesita ningún cambio: el
`UPDATE` lo hace la BD, no la API.

### 4.3 `modificado_en` sin trigger: el repositorio lo fija a mano (RF197)

```csharp
// RepositorioParametroPostgres.cs
public async Task<int> ActualizarAsync(string clave, ParametroActualizar peticion)
{
    const string sql = @"
        UPDATE parametro
           SET valor = coalesce(@Valor, valor),
               tipo_dato = coalesce(@TipoDato, tipo_dato),
               descripcion = coalesce(@Descripcion, descripcion),
               fk_usuario_modifica = coalesce(@FkUsuarioModifica, fk_usuario_modifica),
               modificado_en = now()                          -- NO hay trigger: se fija aqui
         WHERE clave = @Clave;";
    return await conexion.ExecuteAsync(sql, new { Clave = clave, ...peticion });
}
```
Es la primera vez en el proyecto que una columna de auditoría temporal
(`modificado_en`) depende enteramente del código C# para mantenerse
correcta — todas las `creado_en` anteriores usan `DEFAULT now()`, que
basta porque nunca se actualizan después del `INSERT`.

### 4.4 FK hacia tablas que ya existen: no se revalida (D9, sin cambios)

`lote_migracion.fk_sesion`/`fk_usuario`/`fk_estado_proceso`,
`detalle_migracion.fk_lote_migracion`/`fk_registro`,
`bitacora.fk_usuario` — igual que siempre, un id inexistente lo
rechaza el motor (500), la API no revalida.

## 5. Frontend: exportar a ASIS, bitácora y parámetros

```python
# routes/migracion.py (forma, no codigo final)
# POST /sesiones/<id>/migrar -> POST /api/LoteMigracion {fkSesion: id}
# GET  /migracion/<idLote>   -> detalle del lote + sus filas (GET /api/DetalleMigracion?fkLoteMigracion=idLote)
# PATCH sobre cada fila/lote, desde el mismo panel de administracion

# routes/bitacora.py -> solo lectura, con filtros por tabla/usuario/fecha

# routes/parametros.py -> CRUD simple, igual molde que Facultad/Sede
```

## 6. Docker: sin cambios de infraestructura, sin migraciones nuevas

Sigue siendo `postgres` + `api`, **sin ningún archivo nuevo en**
`db/postgres/migraciones/` — las 4 tablas y las 2 funciones que v7 usa
ya estaban en `script_bdcatedras.sql` desde el montaje de la v1.
