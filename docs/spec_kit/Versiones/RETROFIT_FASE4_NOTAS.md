# Retrofit criterio 3 · Fase 4 — notas de ajustes y pruebas

> Lote de Isaac (tester del equipo). Plan: [PLAN_RETROFIT_SP.md](PLAN_RETROFIT_SP.md) §3, Fase 4.
> Molde: `db/postgres/migraciones/005_sp_sede_ejemplo.sql` +
> `api_catedras/Repositorios/RepositorioSedePostgres.cs`.
> Este documento es el insumo para el entregable de prompts (§11 de `VERSION_2.md`):
> qué hubo que **ajustarle** al molde de `sede` en cada tabla y por qué.

> **Nota de fusión (2026-10-14).** Este lote se fusionó con la rama que trae
> `AJUSTES_PATRON_SEDE_LOTE.md` (Facultad, Dependencia, ProgramaAcademico,
> AliasPrograma, Catedra, Sesion, Ponencia, Parametro, Bitacora → `006`-`014`) y
> la Fase 2 (catálogos → `015`-`023`). Para no chocar, las migraciones de este
> lote pasaron de `006`-`014` a **`024`-`032`** (mismo contenido, mismo orden
> relativo; `z024_` … `z032_` en `docker-compose.yml`). Donde este documento
> habla del estado "antes de la fusión" se dejó el número original.

## 1. Qué se entregó

| Tabla | Migración | Repositorio | ¿Cambió Controller/Servicio/IRepositorio/Peticiones? |
|---|---|---|---|
| Asistente | `024_sp_asistente.sql` (4 SP; crear = `sp_guardar_asistente` de la 002) | reescrito | **No** |
| DocumentoAsistente | `025_sp_documento_asistente.sql` | reescrito | **No** |
| VinculacionAsistente | `026_sp_vinculacion_asistente.sql` | reescrito | **No** |
| ProgramaAsistente | `027_sp_programa_asistente.sql` | reescrito | **No** |
| ConsentimientoDatos | `028_sp_consentimiento_datos.sql` | reescrito | **No** |
| LoteCargaAsistente | `029_sp_lote_carga_asistente.sql` (cerrar sigue en la 003) | reescrito | **No** |
| NovedadCarga | `030_sp_novedad_carga.sql` | reescrito | **No** |
| LoteMigracion | `031_sp_lote_migracion.sql` | **nuevo** | **Nuevos** (no existían; permitido) |
| DetalleMigracion | `032_sp_detalle_migracion.sql` | **nuevo** | **Nuevos** (no existían; permitido) |

Las 9 migraciones están montadas en `docker-compose.yml` como `z024_` … `z032_`.
En total quedan **49 procedimientos** con el molde `sp_<verbo>_<tabla>` (5 de
`sede` + 4 de `asistente` + 5 × 8). Todos tienen `p_resultado json` INOUT como
último parámetro (lo comprueba la prueba SQL).

**Regla 1 cumplida:** ningún `IRepositorio*.cs`, `Servicio*.cs`,
`Controllers/*.cs` ni `Peticiones/*.cs` de las 7 tablas existentes cambió. Una
prueba de `Programa.cs` lo vigila por reflexión (nombres exactos de los métodos de
cada `IRepositorio*`).

## 2. Ajustes transversales (valen para varias tablas)

1. **Fechas `DateOnly` → `CAST(@x AS date)` en el `CALL`.** El
   `ManejadorTipoDateOnly` manda cada `DateOnly` como `DateTime`, y Npgsql lo tipa
   `timestamp`. Para resolver la sobrecarga de un procedimiento, `timestamp → date`
   **no** es un cast implícito (solo de asignación), así que el `CALL` falla con
   *"procedure … does not exist"*. Comprobado contra PostgreSQL 16: sin el CAST
   falla, con el CAST pasa. En `sede` no aparecía porque no tiene fechas.
2. **`smallint` → el parámetro entra como `integer`.** `int4 → int2` tampoco es
   implícito. (`detalle_migracion.reunion_enviada`.)
3. **`citext` → el parámetro entra como `varchar`.** `text → citext` tampoco es
   implícito; se asigna a la columna dentro del SP. (Correos de `asistente`, mismo
   criterio que ya usaba `sp_guardar_asistente`.) `text → char(6)` **sí** es
   implícito: `fk_periodo` no necesita nada.
4. **Clave generada por la BD (IDENTITY).** `sede` recibe su clave del cliente;
   aquí `sp_crear_` hace `RETURNING id INTO v_id` y devuelve la fila completa. Los
   repositorios que devolvían una tupla `(id, fecha)` la sacan de esa fila (mismo
   contrato).
5. **`sp_actualizar_`/`sp_crear_` reusan `sp_consultar_`** (`CALL … (v_id,
   p_resultado)`) en vez de repetir el `SELECT`: una sola lista de columnas por tabla.
6. **Listar con filtro.** `sede` solo tiene `p_limite`; las tablas detalle reciben
   además su FK opcional (`p_fk_asistente`, `p_fk_lote_carga`, `p_fk_sesion`,
   `p_fk_lote_migracion`), como ya pedía su contrato.
7. **Fechas en el JSON.** Antes Npgsql entregaba `timestamptz` como `DateTime`
   `Kind=Utc` (la API lo serializaba con `Z`). Ahora llega como texto
   `…+00:00` y System.Text.Json lo convierte a `Kind=Local`; en el contenedor
   (UTC) el **valor es el mismo**, pero la API lo escribe `…+00:00` en vez de `…Z`.
   Afecta a `creado_en`, `registrado_en`, `aceptado_en` y `cargado_en`. No rompe
   ninguna prueba; lo anoto por si el front compara textos.

## 3. Ajustes por tabla

### Asistente
- **Sin `sp_crear_asistente`:** el crear sigue siendo `sp_guardar_asistente`
  (002, maestro + 4 detalles). Un segundo alta paralelo permitiría crear
  asistentes sin sus detalles por otro camino. La prueba SQL comprueba que **no**
  existe.
- `sp_listar_` recibe `p_q` y `p_desplazamiento` (búsqueda ILIKE + paginación, ADR
  D18). El `'%…%'` lo arma ahora el SP, no el repositorio.
- **Eliminar pasa a LÓGICO** (`activo = false`). **Cambio visible de
  comportamiento:** antes `DELETE /api/Asistente/{id}` era físico, con CASCADE de
  sus 4 detalles y **500 por RESTRICT** si tenía usuario/ponencia/registros. Ahora
  responde 200 y no borra nada; un segundo DELETE da 404.
- Listar devuelve **solo activos** (igual que `sede`). Consultar por id sí devuelve
  inactivos. (Hoy los 14 811 de ejemplo están activos: no cambia nada visible
  hasta que alguien desactive uno.)
- Los CHECK (`chk_asistente_externo`, `chk_asistente_correo`) ahora se evalúan
  sobre la fila **ya mezclada** del PATCH: un externo sin `id_asis` al que se le
  manda `{"es_externo": false}` → 500. Antes era igual (el UPDATE parcial también
  dejaba la fila así), pero ahora es explícito.

### DocumentoAsistente
- Clave IDENTITY (ajuste 4); filtro por asistente (ajuste 6).
- `sp_eliminar_` es **DELETE físico**: no hay `activo`. `vigente` no es un
  `activo` disfrazado (es "cuál es el documento actual", índice único parcial).
- `sp_actualizar_` no recibe `fk_asistente` (un documento no cambia de dueño:
  misma lista blanca que tenía el repositorio).

### VinculacionAsistente
- **Llave compuesta** (3 columnas) en consultar/actualizar/eliminar.
- **`sp_eliminar_` = CERRAR** (`fecha_fin`, por defecto hoy, solo si seguía
  abierta). Por la regla 3 sería DELETE físico, pero la tabla es una historia que
  se cierra, nunca se borra (ADR D16). Es exactamente el UPDATE que hacía
  `CerrarAsync`, y es lo que ese método llama ahora. Recibe un 4.º parámetro
  `p_fecha_fin`.
- `sp_actualizar_` reemplaza la única columna no-llave (`fecha_fin`, NULL =
  reabrir). Existe por el criterio, **la API no lo expone** (no hay
  `ActualizarAsync` en el contrato).
- Fechas con `CAST(@x AS date)` (ajuste 1).

### ProgramaAsistente
- Llave compuesta. **`sp_actualizar_` = cambiar estado** (`estado` es la única
  columna no-llave). `CambiarEstadoAsync` hace consulta + mezcla + llamada como
  `sede`, aunque la mezcla quede trivial.
- **`sp_eliminar_` = retiro lógico** (`estado = 'RETIRADO'`, si no lo estaba).
  ⚠ **A confirmar:** no lo usa la API (el contrato no tiene `EliminarAsync`);
  retirar sigue siendo `PATCH /cambiar-estado`. Alternativa literal a la regla 3:
  un DELETE físico, que contradice el ADR D16.

### ConsentimientoDatos
- ⚠ **Se construyeron los 5 SP, pero la API sigue usando solo 3** (listar,
  consultar, crear). `sp_actualizar_` y `sp_eliminar_` funcionan en la BD (la
  prueba SQL lo comprueba), **pero no están conectados** a ningún repositorio,
  servicio ni controlador. Exponerlos contradice la Ley 1581 / RF113: **decisión
  del equipo antes de tocar el Controller.** Una prueba de `Programa.cs` falla si
  alguien le agrega PUT/PATCH/DELETE al controlador.
- `ip` (INET): se lee con `host(ip)` y se escribe con `::inet` **dentro** del SP.

### LoteCargaAsistente
- Corrección al encargo: **`sp_guardar_lote_carga` (003) no es el CREAR, es el
  CIERRE** (contadores + novedades). El ABRIR era un INSERT suelto en el
  repositorio; ese es el que pasó a `sp_crear_lote_carga_asistente`. El cierre no
  se tocó.
- `sp_actualizar_` y `sp_eliminar_` (físico; novedades en CASCADE) existen pero
  **no se exponen** (RF117: el lote es bitácora).
- Listar del más reciente al más antiguo (como en v3).

### NovedadCarga
- La API la usa en solo lectura (RF118): el repositorio llama a listar y
  consultar. Crear/actualizar/eliminar existen y **no se exponen**.
  `sp_guardar_lote_carga` **no** se reescribió para llamar a `sp_crear_novedad_carga`
  (su `INSERT … SELECT` de un arreglo jsonb es una sola sentencia).

### LoteMigracion (nueva)
- **`sp_crear_` NO reimplementa la generación:** llama a
  `sp_generar_lote_migracion` (script provisto, ADR D34) y devuelve la fila. Ahí
  viven el nombre del archivo, el conteo de migrables, los detalles y el cambio de
  la sesión a `MIGRADA`. Recibe la **sesión**, no columnas.
- `sp_actualizar_` reemplaza solo lo que cambia con el tiempo según responde
  ASIS: `instancia_proceso_asis`, `fk_estado_proceso`, `observaciones` (ADR D35).
- `sp_eliminar_` (físico) existe pero **no se expone** (RF185).
- Capa construida según el borrador de la v7 (RF179-RF185) con 2 diferencias a
  revisar: (a) los JSON van en **snake_case** (`fk_sesion`), como las otras 35
  tablas, no en el camelCase del borrador (`fkSesion`); (b) el PATCH **no**
  acepta `instancia_proceso_asis` porque el borrador no lo pide, aunque el SP sí
  lo recibe (agregarlo = una propiedad en `LoteMigracionActualizar`).
- El borrador de la v7 decía "sin ninguna migración nueva" (ADR D34). Con el
  retrofit eso cambia: `031`/`032` existen. Habrá que actualizar esa nota cuando
  se cierre la v7.

### DetalleMigracion (nueva)
- **Choque de nombres:** la columna se llama `resultado` y la regla 4 reserva
  `p_resultado` para el INOUT json. El parámetro de la columna es
  `p_resultado_migracion`.
- `reunion_enviada` entra como `integer` (ajuste 2).
- `sp_actualizar_` reemplaza solo `resultado` y `mensaje`; las columnas
  `*_enviado(a)` son copia literal del archivo y no se tocan.
- Sin POST/DELETE en la API (RF189); `sp_crear_`/`sp_eliminar_` existen y no se
  exponen.

## 4. Otros archivos que se tocaron (y por qué)

- `api_catedras/Program.cs` — registro de DI de LoteMigracion/DetalleMigracion.
- `api_catedras/pruebas/verificar_v5.ps1` — el paso 8 esperaba **500** en
  `DELETE /api/Asistente/1` (RESTRICT); con el borrado lógico ahora es 200 +
  `activo=false` + sus registros intactos. El paso 0 exigía `version == "v5"`, y
  la API ya responde `"v6"`: fallaba **antes** de este lote. Ahora acepta v5 o
  posterior.
- `front_catedras/routes/admin.py` — solo el mensaje flash de "eliminar
  asistente", que decía "y sus documentos… por ON DELETE CASCADE" (ya no es
  cierto).

## 5. Pruebas

| Colección | Qué cubre | Estado |
|---|---|---|
| `db/postgres/pruebas/prueba_sp_retrofit_fase4.sql` (**nueva**) | 160 casos: inventario de los 49 SP, los 5 verbos de las 9 tablas, no-existe → NULL, eliminar lógico/físico/cierre, y CHECK/UNIQUE/FK como última muralla. Corre en `BEGIN … ROLLBACK` | ✅ **160/160** contra PostgreSQL 16 con el script + las 14 migraciones en el orden de initdb |
| `api_catedras/pruebas/Programa.cs` (**ampliado**) | +8 pruebas de "contrato sin cambios" (regla 1) y +21 de capas para LoteMigracion/DetalleMigracion con repositorios falsos | ⏳ sin correr: no hay SDK de .NET en el entorno donde se escribió (sintaxis verificada) |
| `api_catedras/pruebas/verificar_retrofit_fase4.ps1` (**nuevo**) | Regresión v1 automatizada (Sede CRUD/422/500 + catálogos) y las 9 tablas por HTTP | ⏳ requiere Docker |
| `api_catedras/pruebas/verificar_v5.ps1` (**ajustado**) | v5 completa | ⏳ requiere Docker |
| `api_catedras/pruebas/correr_todo_fase4.ps1` (**nuevo**) | **Todo lo anterior en un comando**, desde una BD nueva | ⏳ requiere Docker |

Verificaciones extra hechas sin Docker:
- Cada `CALL` escrito en los 9 repositorios (37 apariciones, ninguna sin probar) se extrajo del `.cs`
  y se ejecutó contra la BD real con los parámetros tipados como los manda Npgsql
  (`string→text`, `long→int8`, `int→int4`, `DateOnly→timestamp`): todas resuelven
  y devuelven lo esperado.
- Las claves del JSON que devuelve cada SP coinciden 1 a 1 con los
  `[JsonPropertyName]` de los 9 modelos (ningún `required` sin clave).
- `docker compose config` valida y monta `z001_` … `z014_` en orden (antes de la fusión; hoy `z001_` … `z032_`).

**Para cerrar la Fase 4 (y la 7 en lo que toca a este lote):**
```powershell
powershell -ExecutionPolicy Bypass -File .\api_catedras\pruebas\correr_todo_fase4.ps1
```
Hace `docker compose down -v` + `up -d --build`, espera la API y corre: SQL →
`dotnet run --project pruebas` (con la regresión v1 de capas) → `verificar_v5.ps1`
→ `verificar_retrofit_fase4.ps1`. Sale con 0 solo si todo pasa.

**BD que ya existe (sin `down -v`):** aplicar a mano, en orden,
`024` … `032` con `psql` (todas son `CREATE OR REPLACE`, re-ejecutables).

## 6. Pendientes de coordinación

1. ~~**Numeración 006-014** tomada para este lote.~~ **Resuelto al fusionar:** el
   lote de Facultad…Bitacora ya usaba 006-014 y la Fase 2 015-023, así que este
   lote se renumeró a **024-032** (ver la nota al inicio).
2. Confirmar las decisiones ⚠ de ConsentimientoDatos y ProgramaAsistente.
3. Regresión v1, paso 3 del quickstart: con `sede` en borrado lógico (Fase 1), el
   "GET posterior" a un DELETE ya no da 404 sino 200 con `activo=false`. El
   script nuevo lo verifica así; conviene actualizar
   `version1/7_quickstart.md` para que diga lo mismo.
