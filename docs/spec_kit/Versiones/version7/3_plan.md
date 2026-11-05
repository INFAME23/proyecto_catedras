# Plan técnico — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Alineado con el
> código real en la Fase 54 (D34 enmendado, D37).

## 1. Stack

Sin cambios: C#/ASP.NET Core (.NET 10) + Dapper, sin ORM y **sin
paquetes nuevos**. Molde vigente del proyecto (retrofit del criterio 3,
[PLAN_RETROFIT_SP.md](../PLAN_RETROFIT_SP.md)): cada verbo de un
repositorio es un `CALL catedras.sp_<verbo>_<tabla>(..., NULL)` que
devuelve `json` en un `INOUT p_resultado`; `NULL` = no encontrado. Los
procedimientos de la v7 ya existen (`013`, `014`, `031`, `032`, D34) y
**ninguno reimplementa** la generación del lote.

## 2. Estructura de carpetas

```
api_catedras/
├── Modelos/
│   ├── LoteMigracion.cs       # 10 propiedades
│   ├── DetalleMigracion.cs    # 8 propiedades
│   ├── Bitacora.cs            # 9 propiedades
│   └── Parametro.cs           # 6 propiedades, PK = Clave (string)
├── Peticiones/
│   ├── LoteMigracionGenerar.cs / LoteMigracionActualizar.cs   # sin Crear/Reemplazo genéricos
│   ├── DetalleMigracionActualizar.cs                          # ÚNICA — sin Crear/Reemplazo
│   └── Parametro{Crear,Reemplazo,Actualizar}.cs
│       (sin ninguna petición de Bitacora: D36/D37-A)
├── Controllers/
│   ├── LoteMigracionController.cs      # GET lista, GET/{id}, POST (=generar), PATCH/{id} — 4
│   ├── DetalleMigracionController.cs   # GET lista, GET/{id}, PATCH/{id} — 3
│   ├── BitacoraController.cs           # GET lista, GET/{id} — 2, SIN ningún verbo de escritura
│   └── ParametroController.cs          # 6 endpoints, molde completo con llave de texto
├── Servicios/
│   ├── IServicioLoteMigracion.cs / ServicioLoteMigracion.cs     # no recibe IRepositorioSesion
│   ├── IServicioDetalleMigracion.cs / ServicioDetalleMigracion.cs
│   ├── IServicioBitacora.cs / ServicioBitacora.cs               # solo lectura
│   └── IServicioParametro.cs / ServicioParametro.cs
└── Repositorios/
    ├── IRepositorioLoteMigracion.cs / RepositorioLoteMigracionPostgres.cs       # CrearAsync = CALL sp_crear_lote_migracion
    ├── IRepositorioDetalleMigracion.cs / RepositorioDetalleMigracionPostgres.cs # sin CrearAsync ni EliminarAsync
    ├── IRepositorioBitacora.cs / RepositorioBitacoraPostgres.cs                 # solo ObtenerTodosAsync/ObtenerPorIdAsync
    └── IRepositorioParametro.cs / RepositorioParametroPostgres.cs               # CRUD estándar, PK string

db/postgres/migraciones/        (ya existen, montadas como zNNN_ en initdb)
├── 013_sp_parametro.sql        # 5 SP; sp_actualizar_parametro fija modificado_en = now()
├── 014_sp_bitacora.sql         # 5 SP; la API solo llama listar y consultar
├── 031_sp_lote_migracion.sql   # 5 SP; sp_crear_ llama a sp_generar_lote_migracion
└── 032_sp_detalle_migracion.sql# 5 SP; la API solo llama listar, consultar y actualizar
```

Nombres de los métodos del repositorio de `LoteMigracion`: el de generar
se llama `CrearAsync` (sigue el nombre del SP `sp_crear_lote_migracion`);
en el servicio y en el controlador el verbo de negocio se llama
`GenerarAsync`/`Generar`.

## 3. Arquitectura en capas

Sin cambios de forma (Artículo 3). Lo particular de la v7 es que
`LoteMigracion` reutiliza un procedimiento provisto que, además, modifica
una fila de OTRA tabla de otra versión (`sesion.estado`) dentro del
mismo `CALL` (ver §4.2).

```
POST /api/LoteMigracion {"fk_sesion": 5712}
  → LoteMigracionController.Generar         (422 si falta fk_sesion)
  → ServicioLoteMigracion.GenerarAsync      (no toca Sesion)
  → RepositorioLoteMigracionPostgres.CrearAsync
  → CALL catedras.sp_crear_lote_migracion(@fkSesion, @fkUsuario, NULL)
      → CALL catedras.sp_generar_lote_migracion(...)        (provisto)
          · count(*) FROM fn_exportar_asis(sesion)  → 0 ⇒ RAISE (500)
          · INSERT lote_migracion (GENERADO, total_filas, nombre_archivo)
          · INSERT … SELECT detalle_migracion (PENDIENTE)
          · UPDATE sesion SET estado = 'MIGRADA'
      → devuelve la fila del lote en p_resultado
  → 200 {estado, mensaje, id_lote_migracion, lote}
```

## 4. Decisiones de diseño clave

### 4.1 Generar un lote: se llama al SP provisto, no se rediseña (ADR D34)

Ver desarrollo completo en [4_research.md](4_research.md).
`sp_generar_lote_migracion` ya inserta el maestro (`lote_migracion`) y
todos los detalles (`detalle_migracion`) a partir de lo que
`fn_exportar_asis` calcula — exactamente la operación que la Guía IA,
Regla 1 exigiría resolver con un SP, solo que **ya está resuelta**.
`sp_crear_lote_migracion` (`031`) es un envoltorio fino con la firma del
molde (`INOUT p_resultado json`):

```csharp
// RepositorioLoteMigracionPostgres.cs (resumen del código real)
public async Task<LoteMigracion> CrearAsync(long fkSesion, long? fkUsuario)
{
    const string sql = "CALL catedras.sp_crear_lote_migracion(@fkSesion, @fkUsuario, NULL);";
    // El RAISE de sp_generar_lote_migracion sube como NpgsqlException (→ 500).
    string? json = await conexion.QuerySingleAsync<string?>(sql, new { fkSesion, fkUsuario });
    return JsonSerializer.Deserialize<LoteMigracion>(json!)!;
}
```

`fn_generar_lote_api` existe en la BD pero la API no la usa: devuelve
solo el id, y la respuesta del `POST` necesita la fila completa.

### 4.2 El efecto colateral sobre `Sesion.estado` no se oculta ni se revierte desde la API

`sp_generar_lote_migracion` hace `UPDATE sesion SET estado = 'MIGRADA'
WHERE id_sesion = p_id_sesion` en el mismo `CALL`.
`ServicioLoteMigracion.GenerarAsync` **no** intenta deshacer, verificar
ni duplicar ese cambio desde C# — su constructor solo recibe
`IRepositorioLoteMigracion` (D9, llevado a un caso donde el efecto
colateral cruza hacia una tabla de otra versión). El repositorio de
`Sesion` (v4) no necesita ningún cambio: el `UPDATE` lo hace la BD.

### 4.3 `PATCH` por mezcla + SP completo (molde de `sede`)

`LoteMigracion`, `DetalleMigracion` y `Parametro` actualizan igual: el
repositorio llama a `sp_consultar_<tabla>`, mezcla encima los campos que
llegaron (`ACamposEnviados()` de la petición) y llama a
`sp_actualizar_<tabla>` con el conjunto completo. Así:

- `sp_actualizar_lote_migracion` recibe `instancia_proceso_asis`,
  `fk_estado_proceso` y `observaciones`; la API solo expone los dos
  últimos y conserva el valor actual de `instancia_proceso_asis` al
  mezclar (D37-F).
- `sp_actualizar_detalle_migracion` recibe `resultado` (el parámetro se
  llama `p_resultado_migracion`, porque `p_resultado` es el `INOUT`) y
  `mensaje`; las columnas `*_enviado(a)` son copia del archivo y no se
  tocan.
- `sp_actualizar_parametro` reemplaza `valor`, `tipo_dato`, `descripcion`
  y `fk_usuario_modifica`, y **fija `modificado_en = now()` dentro del
  SP** (RF197). No hay trigger, y el repositorio no repite el `now()`.

### 4.4 FK hacia tablas que ya existen: no se revalida (D9, sin cambios)

`lote_migracion.fk_sesion`/`fk_usuario`/`fk_estado_proceso`,
`detalle_migracion.fk_lote_migracion`/`fk_registro`,
`parametro.fk_usuario_modifica` — un id inexistente lo rechaza el motor
(500). Tampoco se revalida en C# el índice parcial `ux_detmig_aceptado`.

### 4.5 `Bitacora`: solo lectura aunque los SP de escritura existan (D36, D37-A)

`IRepositorioBitacora` declara solo `ObtenerTodosAsync(nombreTabla,
limite)` y `ObtenerPorIdAsync`; `IServicioBitacora`, solo `ListarAsync`
y `ObtenerAsync`; `BitacoraController`, solo dos `[HttpGet]`. Los SP
`sp_crear_`/`sp_actualizar_`/`sp_eliminar_bitacora` se quedan en `014`
sin llamador (mismo criterio que los SP no expuestos de
`RespuestaEncuesta`). Sin filtro por usuario (D37-C).

### 4.6 Fechas

`row_to_json` entrega los `timestamptz` como texto con desfase; los
repositorios los normalizan a UTC (ajuste común del retrofit). En el
contenedor (UTC) `generado_en` (`DateTimeOffset`) sale como `…+00:00` y
`modificado_en`/`ocurrido_en` (`DateTime`) como `…Z`: mismo instante,
distinto texto. La Fase 55 confirma que `front_catedras/servicios/fechas.py`
lee ambos formatos.

## 5. Frontend: exportar a ASIS, bitácora y parámetros (D37-E)

Todo dentro de `routes/admin.py`, para heredar la guardia
`before_request` de `admin_bp` (rol `ADMIN`, D28). Sin blueprints nuevos.

```python
# routes/admin.py (forma, no código final)
# GET  /admin/parametros                  -> lista; crear/editar/eliminar (clave de solo lectura al editar)
# GET  /admin/lotes-migracion?fk_sesion=  -> lista de lotes
# GET  /admin/lotes-migracion/<id>        -> lote + sus filas (GET /api/DetalleMigracion?fkLoteMigracion=id)
#      POST …/<id>/estado                 -> PATCH /api/LoteMigracion/<id>  (<select> desde /api/EstadoProceso)
#      POST …/detalle/<id>                -> PATCH /api/DetalleMigracion/<id> (<select> PENDIENTE/ACEPTADO/RECHAZADO)
# GET  /admin/detalles-migracion          -> lista filtrable (lote, resultado); se edita desde el lote
# GET  /admin/bitacora?tabla=&limite=     -> solo lectura, <select> de tablas, JSON indentado
# POST /admin/sesiones/<id>/migrar        -> POST /api/LoteMigracion {fk_sesion: id} y redirige al lote nuevo
```

`detalle_sesion.html` pinta el botón "Generar migración" solo si el
usuario de la sesión de Flask tiene rol `ADMIN`, la sesión ya terminó y
tiene registros. `panel.html` gana 4 tarjetas. Los errores de la API se
muestran en castellano (ninguna pantalla muestra "PUT", "PATCH", "422"
ni "FK").

## 6. Docker: sin cambios de infraestructura

Sigue siendo `postgres` + `api`. Las 4 migraciones de la v7 ya están en
`db/postgres/migraciones/` y montadas en `docker-compose.yml` (`z013_`,
`z014_`, `z031_`, `z032_`); la Fase 54 no agrega ninguna. Una migración
nueva solo se aplica con el volumen vacío (`docker compose down -v`) o a
mano con `psql`.
