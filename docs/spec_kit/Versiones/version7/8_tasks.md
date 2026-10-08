# Tareas — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Plan, no
> registro de trabajo hecho.
>
> Continúa la numeración de fases de
> [version6/8_tasks.md](../version6/8_tasks.md) (termina en Fase 53).
>
> **Nota de alcance (v7):** ninguna migración SQL nueva (ADR D33) —
> segunda versión del proyecto sin ninguna, junto con v4. Esta es la
> última versión del mapa: al cerrarla, las 37 tablas de `bdcatedras`
> quedan expuestas por completo.

---

## Fase 54 — Preparación: sin migración nueva, confirmar lo que ya existe
> ADR D33 de [4_research.md](4_research.md). Va primero porque las
> Fases 55-57 dependen de confirmar, contra la BD real, que no hace
> falta escribir ningún `CREATE PROCEDURE`.

- [ ] Confirmar con `\sf catedras.sp_generar_lote_migracion`, `\sf
      catedras.fn_generar_lote_api` y `\sf catedras.fn_exportar_asis`
      (contra la BD del sandbox) que las 3 funciones existen y que la
      primera hace el `INSERT` del maestro, el `INSERT ... SELECT`
      de los detalles y el `UPDATE sesion SET estado = 'MIGRADA'` en
      una sola transacción.
- [ ] Confirmar con `\d catedras.bitacora` que no existe ningún
      trigger de escritura disparado desde la API — solo
      `trg_bitacora_registro` sobre `registro_asistencia`.
- [ ] No crear ningún archivo en `db/postgres/migraciones/`.

**Verificación planeada**: ninguna llamada nueva a `CALL` con `jsonb`
en todo el código de v7 — todo repositorio de escritura de esta
versión es un `INSERT`/`UPDATE` directo o un `SELECT * FROM
fn_..._api(...)`.

## Fase 55 — La aplicación (capas de `LoteMigracion` y `DetalleMigracion`)
> RF179-RF189 de [2_spec.md](2_spec.md), ADR D33, D34, §2.1-2.2 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 54 y de
> `Sesion`/`EstadoProceso` (v1/v4) y `RegistroAsistencia` (v5), ya
> construidas.

- [ ] `Modelos/LoteMigracion.cs`, `DetalleMigracion.cs`.
- [ ] `Peticiones/LoteMigracionGenerar.cs` — **única** petición de
      creación de esta entidad: `FkSesion` obligatorio, `FkUsuario`
      opcional. Sin `LoteMigracionCrear` genérico (no hay un `POST`
      de forma libre, solo "generar").
- [ ] `Peticiones/LoteMigracionActualizar.cs` — `FkEstadoProceso` y
      `Observaciones`, ambos opcionales. **Sin**
      `LoteMigracionReemplazo` (no hay `PUT`, RF183).
- [ ] `Peticiones/DetalleMigracionActualizar.cs` — **única** petición
      de esta entidad: `Resultado` con `[RegularExpression]`
      (`PENDIENTE|ACEPTADO|RECHAZADO`), `Mensaje` opcional. Sin
      `Crear` (nace dentro de "generar") ni `Reemplazo`.
- [ ] `Repositorios/IRepositorioLoteMigracion.cs` +
      `RepositorioLoteMigracionPostgres.cs` — `ObtenerTodosAsync`,
      `ObtenerPorIdAsync`, `GenerarAsync` (`SELECT * FROM
      fn_generar_lote_api(...)`, código exacto en §4.1 de
      [3_plan.md](3_plan.md)), `ActualizarEstadoAsync` (`UPDATE`
      directo). **Sin** `CrearAsync`/`ReemplazarAsync`/`EliminarAsync`.
- [ ] `Repositorios/IRepositorioDetalleMigracion.cs` +
      `RepositorioDetalleMigracionPostgres.cs` —
      `ObtenerTodosAsync`, `ObtenerPorIdAsync`,
      `ActualizarResultadoAsync`. **Sin** `CrearAsync` (nace dentro del
      `GenerarAsync` de `LoteMigracion`, no tiene repositorio propio
      de creación).
- [ ] `Servicios/IServicioLoteMigracion.cs` + `ServicioLoteMigracion.cs`
      — `GenerarAsync` **no** intenta deshacer, verificar ni duplicar
      el `UPDATE sesion SET estado = 'MIGRADA'` (§4.2 de
      [3_plan.md](3_plan.md)): solo llama al repositorio y propaga el
      `idLote`.
- [ ] `Servicios/IServicioDetalleMigracion.cs` +
      `ServicioDetalleMigracion.cs`.
- [ ] `Controllers/LoteMigracionController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPost]` (= generar), `[HttpPatch]`. Sin `PUT`
      ni `DELETE` (RF183).
- [ ] `Controllers/DetalleMigracionController.cs` — `GET` lista,
      `GET/{id}`, `[HttpPatch]`. Sin `POST`, `PUT` ni `DELETE`
      (RF189).
- [ ] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/LoteMigracion` con `fkSesion: 5712` → 200 y `GET
/api/DetalleMigracion?fkLoteMigracion={idLote}` muestra 166 filas;
`GET /api/Sesion/5712` confirma `estado: "MIGRADA"`; una `fkSesion`
sin nada migrable → 500; `PATCH` de `fkEstadoProceso` inexistente →
500; marcar `ACEPTADO` dos filas con el mismo `fkRegistro` en lotes
distintos → la segunda, 500 (`ux_detmig_aceptado`).

## Fase 56 — La aplicación (capa de `Bitacora`, solo lectura)
> RF190-RF191 de [2_spec.md](2_spec.md), ADR D35, §2.3 de
> [5_data_model.md](5_data_model.md). Depende de `RegistroAsistencia`
> (v5, ya construida) solo para que haya filas de ejemplo que leer —
> ningún código de esta fase escribe en `bitacora`.

- [ ] `Modelos/Bitacora.cs`.
- [ ] **Sin ninguna clase en `Peticiones/`** — no existe ninguna
      petición de escritura para esta entidad.
- [ ] `Repositorios/IRepositorioBitacora.cs` +
      `RepositorioBitacoraPostgres.cs` — **solo**
      `ObtenerTodosAsync(nombreTabla?, fkUsuario?)` y
      `ObtenerPorIdAsync`. Ningún método de escritura en la interfaz
      (verificación de diseño, mismo criterio que D30/D32 de v6 con
      `IRepositorioRespuestaEncuesta`).
- [ ] `Servicios/IServicioBitacora.cs` + `ServicioBitacora.cs` — solo
      lectura, delega los 2 filtros al repositorio.
- [ ] `Controllers/BitacoraController.cs` — **solo 2 métodos**: `GET`
      lista, `GET/{id}`. Ningún `[HttpPost]`/`[HttpPut]`/
      `[HttpPatch]`/`[HttpDelete]` — por eso esas rutas devuelven 404
      de ASP.NET, no 405: el framework no encuentra ningún método que
      las acepte.
- [ ] `Program.cs` — registrar el par nuevo.

**Verificación planeada**: `GET /api/Bitacora?limite=5` → 200 con 5
filas; `GET /api/Bitacora?nombreTabla=registro_asistencia` → filtra
correctamente; `POST`/`PUT`/`PATCH`/`DELETE` sobre `/api/Bitacora` (o
con un `{id}`) → 404 en los 4 casos.

## Fase 57 — La aplicación (capa de `Parametro`, CRUD completo con llave de texto)
> RF192-RF197 de [2_spec.md](2_spec.md), §2.4 de
> [5_data_model.md](5_data_model.md). Sin dependencias de otra
> entidad de v7 — puede construirse en paralelo con las Fases 55-56.

- [ ] `Modelos/Parametro.cs`.
- [ ] `Peticiones/ParametroCrear.cs` — `Clave` con
      `[RegularExpression]` (mismo molde que `Facultad`/`Sede`),
      `Valor`, `TipoDato` (`[RegularExpression]` con la lista
      cerrada), `Descripcion` obligatorios; `FkUsuarioModifica`
      opcional.
- [ ] `Peticiones/ParametroReemplazo.cs` + `ParametroActualizar.cs` —
      **sin** `Clave` en ninguna de las dos (es la llave de la ruta,
      no una columna editable del body — mismo criterio que
      `Facultad.id_facultad` desde v2).
- [ ] `Repositorios/IRepositorioParametro.cs` +
      `RepositorioParametroPostgres.cs` — CRUD estándar de 6 métodos,
      `clave` como parámetro `string` en vez de `long`.
      `ActualizarAsync` fija `modificado_en = now()` explícitamente en
      el `UPDATE` (§4.3 de [3_plan.md](3_plan.md)) — **sin** ningún
      trigger de BD que lo haga por sí solo (primera vez en el
      proyecto).
- [ ] `Servicios/IServicioParametro.cs` + `ServicioParametro.cs`.
- [ ] `Controllers/ParametroController.cs` — 6 endpoints, molde
      completo, rutas con `{clave}` en vez de `{id}`.
- [ ] `Program.cs` — registrar el par nuevo.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/Parametro` con una `clave` nueva → 200; repetir la misma `clave`
→ 500; `PATCH` cambia `valor` y `modificadoEn` avanza respecto al
valor anterior; `DELETE` → 200, sin ningún error de integridad
referencial (confirma RF196: ninguna FK entrante bloquea nunca este
`DELETE`, a diferencia de cualquier otra entidad del proyecto con
`DELETE`).

## Fase 58 — Prueba de capas ampliada (criterio de aceptación 7)
> Depende de las Fases 55-57 (las 4 entidades deben existir). Última
> vez que esta prueba crece — después de esta fase cubre las 37
> entidades completas del proyecto.

- [ ] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para las 4 entidades. Puntos especiales:
      - `RepositorioLoteMigracionFalso.GenerarAsync` simula el
        `SELECT * FROM fn_generar_lote_api(...)` generando en memoria
        tanto el maestro como los detalles, para verificar sin
        PostgreSQL que el servicio no intenta tocar `Sesion` por su
        cuenta.
      - Un caso de prueba confirma, por inspección del tipo de
        `IRepositorioBitacora`, que **no existe** ningún método de
        escritura en la interfaz (verificación de diseño, mismo
        criterio que D30 de v6 con `RespuestaEncuesta` — asegura que
        D35 no se revierta por accidente en una refactorización
        futura).
      - `RepositorioParametroFalso` incluye un caso de `clave`
        duplicada y confirma que `modificado_en` avanza en el
        `ActualizarAsync` simulado (sin depender de ningún trigger
        falso, porque tampoco existe uno real).

**Verificación planeada**: `dotnet run --project pruebas` → cubre las
**37** entidades completas del proyecto, código de salida 0 — número
que por fin coincide con las 37 tablas de `bdcatedras`.

## Fase 59 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 2-6)
> Depende de las Fases 55-57.

- [ ] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN:
  1. `POST /api/LoteMigracion` con `fkSesion: 5712` → 200; `GET
     /api/DetalleMigracion?fkLoteMigracion={idLote}` → 166 filas; `GET
     /api/Sesion/5712` → `estado: "MIGRADA"`.
  2. `PATCH /api/LoteMigracion/{idLote}` con
     `{"fkEstadoProceso": "EN_CURSO"}` → 200; con uno inexistente →
     500.
  3. `PATCH /api/DetalleMigracion/{idDetalle}` con
     `{"resultado": "ACEPTADO"}` dos veces sobre el mismo `fkRegistro`
     en lotes distintos → la segunda, 500 (`ux_detmig_aceptado`).
  4. `POST`/`PUT`/`PATCH`/`DELETE` sobre `/api/Bitacora` → 404 en los
     4 casos.
  5. `POST /api/Parametro` (clave nueva) → 200; `DELETE` → 200, sin
     error de integridad referencial.
- [ ] `docker compose down -v && docker compose up -d --build`.

**Verificación planeada**: los 5 resultados coinciden con
[5_data_model.md](5_data_model.md) y los criterios de aceptación 2-6
de [2_spec.md](2_spec.md).

## Fase 60 — Frontend: migración, bitácora y parámetros
> Criterio 8 de [2_spec.md](2_spec.md), §5 de [3_plan.md](3_plan.md).
> Depende de las Fases 55-57.

- [ ] `templates/detalle_sesion.html` (v4) — agregar el botón "generar
      migración", visible solo si la sesión ya pasó y tiene registros.
- [ ] `routes/migracion.py` + `templates/migracion_detalle.html` —
      detalle de un lote y sus filas; dos formularios `PATCH`
      distintos (uno por entidad), desde el mismo panel.
- [ ] `routes/bitacora.py` + `templates/admin/bitacora.html` — solo
      lectura, con filtros por tabla y usuario.
- [ ] `routes/parametros.py` + `templates/admin/parametros.html` —
      CRUD simple, mismo molde visual que `Facultad`/`Sede`.
- [ ] `routes/admin.py` — 4 tarjetas nuevas en el panel principal.

**Verificación planeada**: generar un lote de migración de punta a
punta contra Docker/PostgreSQL reales desde el botón de
`detalle_sesion.html`, confirmando visualmente que la sesión pasa a
"migrada" sin que el usuario del front haya tocado nada de `Sesion`
directamente.

## Fase 61 — Cierre de la v7 y del proyecto
> Última fase de la última versión del mapa. No hay ninguna v8.

- [ ] Confirmar que los 8 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado.
- [ ] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 58) — debe cubrir las 37 entidades.
- [ ] Confirmar la cuenta de control del mapa de versiones
      (10+5+7+2+5+4+4 = 37) contra las 37 tablas realmente expuestas
      por `/swagger`.
- [ ] Revisar este spec kit completo, y los de v1-v6, contra lo
      realmente construido — quitar el banner de "BORRADOR" de los 7.
- [ ] Commit y tag: `git tag v7`.
- [ ] Revisar el proyecto completo una última vez contra la
      constitución ([../1_constitution.md](../1_constitution.md)) y la
      [guía operativa para la IA](../../guia_ia.md) — las 3 reglas de
      la guía (SP para maestro-detalle, `<select>` para toda FK en el
      front, BCrypt solo en el servicio) se cumplieron en las 7
      versiones.
