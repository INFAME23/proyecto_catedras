# Tareas — Versión 7: migración a ASIS, bitácora y parámetros

> ⚠️ **BORRADOR** — ver nota de [2_spec.md](2_spec.md). Plan y registro
> de trabajo: una casilla se marca solo con la verificación ejecutada y
> anotada (comando + salida resumida).
>
> Continúa la numeración de fases de
> [version6/8_tasks.md](../version6/8_tasks.md) (termina en Fase 53).
> Guía para hacerlo con un asistente de IA: [guia_ia_v7.md](guia_ia_v7.md).
>
> **Nota de alcance (v7):** las capas de las 4 entidades **ya existen**
> (retrofit del criterio 3, migraciones `013`, `014`, `031`, `032`). Las
> Fases 55-57 son de **auditar y alinear**, no de construir: el código
> solo cambia donde lo decidió el equipo (D37-A en la Fase 56, D37-D en
> la Fase 57). Esta es la última versión del mapa.

---

## Fase 54 — Preparación: decisiones y corrección del borrador
> ADR D34 (enmendado), D36 (enmendado) y D37 de [4_research.md](4_research.md).
> Va primero porque las Fases 55-61 dependen de un spec que no
> contradiga al código ni a la BD.

- [x] Tag `v6` confirmado por el equipo (2026-10-15).
- [x] Contra la BD (script + migraciones `001`-`041` en PostgreSQL 16,
      orden de initdb): existen `sp_generar_lote_migracion` (procedure),
      `fn_generar_lote_api` y `fn_exportar_asis` (functions).
      `sp_generar_lote_migracion` cuenta con `fn_exportar_asis`, lanza
      `RAISE … check_violation` si es 0, inserta el lote, inserta los
      detalles y hace `UPDATE sesion SET estado = 'MIGRADA'` en el mismo
      `CALL`. Evidencia: `\sf` / `pg_proc.prosrc`.
- [x] Triggers que escriben en `bitacora`: `trg_bitacora_registro`
      (`AFTER INSERT OR DELETE OR UPDATE` sobre `registro_asistencia`) y
      `trg_bitacora_asistente` (`AFTER DELETE OR UPDATE` sobre
      `asistente`), ambos con `fn_trg_bitacora`. Evidencia: `pg_trigger`.
- [x] Las 16 filas del §2 de [guia_ia_v7.md](guia_ia_v7.md) confirmadas
      contra el código y la BD, salvo la 10 (405 vs 404), que requiere
      HTTP real → Fase 59. Datos: 5 lotes / 823 detalles / 168 bitácora /
      9 parámetros; 5712 `MIGRADA`, 168 registros, 1 externo,
      `fn_exportar_asis(5712)` = 166; generar (en transacción revertida)
      → lote 6, 166 `PENDIENTE`, sesión `MIGRADA`; `ACEPTADO` pasa con el
      registro 167, choca con el 1 (`ux_detmig_aceptado`); sesión 1 →
      "No hay registros migrables…"; `NO_EXISTE` → `fk_lotemig_estado`;
      borrar el asistente 2 → fila `asistente / UPDATE`.
- [x] Decisiones A-F del equipo (todas las recomendadas) escritas en
      D37 de [4_research.md](4_research.md).
- [x] Documentos de `version7/` alineados (2_spec, 3_plan, 4_research,
      5_data_model, 6_contracts, 7_quickstart, 8_tasks) y
      `guia_ia_v7.md` agregada a la carpeta. Banner BORRADOR conservado
      hasta la Fase 61.
- [x] Ningún archivo nuevo en `db/postgres/migraciones/`.

**Verificación**: ningún documento de `version7/` dice camelCase en los
cuerpos, "sin migración nueva", "un solo trigger", "404 de ASP.NET",
`fn_generar_lote_api` como llamada de la API, `fkUsuario` como filtro de
bitácora, ni que el repositorio fije `modificado_en`.

> **Cómo se verificó (Fases 55-61).** Sin Docker ni NuGet en el entorno de
> la sesión, se armó un banco equivalente: .NET SDK 10.0.112; Dapper
> 2.1.66 y BCrypt.Net-Next 4.0.3 compilados desde su código fuente
> (GitHub, mismas versiones que el `.csproj`); un **Npgsql de prueba** que
> manda cada comando, con sus parámetros tipados como Npgsql 9, a
> PostgreSQL 16 real (sesión en UTC, como el contenedor); un Swashbuckle
> mínimo que expone `/swagger/v1/swagger.json`; y PowerShell 7.4. La BD se
> recreó con `script_bdcatedras.sql` + `001`-`041` antes de cada corrida
> (equivalente a `down -v`). Controladores, servicios, repositorios,
> enrutamiento y validación de ASP.NET Core son los **reales**. Lo único
> que no se ejecutó es el contenedor en sí: eso lo cubre
> `pruebas/correr_todo_v7.ps1` (Fase 61).

## Fase 55 — `LoteMigracion` y `DetalleMigracion` (auditar y alinear)
> RF179-RF189 de [2_spec.md](2_spec.md), ADR D34, D35, D37-B/F,
> §2.1-2.2 de [5_data_model.md](5_data_model.md).

- [x] `LoteMigracionController`: exactamente `GET` lista, `GET/{id}`,
      `POST`, `PATCH`. `DetalleMigracionController`: exactamente `GET`
      lista, `GET/{id}`, `PATCH`.
- [x] `RepositorioLoteMigracionPostgres.CrearAsync` = `CALL
      sp_crear_lote_migracion`; `ServicioLoteMigracion` solo recibe
      `IRepositorioLoteMigracion`.
- [x] `IRepositorioLoteMigracion` sin `ReemplazarAsync`/`EliminarAsync`;
      `IRepositorioDetalleMigracion` sin `CrearAsync`/`EliminarAsync`.
- [x] JSON en snake_case; respuesta del `POST` `{estado, mensaje,
      id_lote_migracion, lote}`; `PATCH` vacío → 400; `resultado` fuera de
      lista → 422.
- [x] Fechas: `generado_en` sale `…+00:00` y `modificado_en`/`ocurrido_en`
      `…Z`; `fechas.py` lee los dos (`2026-08-13T18:35:08.145925+00:00` →
      `2026-08-13 13:35:08` hora de Colombia).
- [x] `dotnet build` → 0 errores, 0 advertencias.

**Resultado**: ninguna diferencia con el contrato; no hubo que cambiar
código.

## Fase 56 — `Bitacora`: solo lectura (D36, D37-A)
> RF190-RF191 de [2_spec.md](2_spec.md), §2.3 de
> [5_data_model.md](5_data_model.md).

- [x] `BitacoraController`: solo `GET` lista y `GET/{id}`.
- [x] Quitados `CrearAsync`/`ReemplazarAsync`/`ActualizarAsync`/`EliminarAsync`
      de `IServicioBitacora`, `ServicioBitacora`, `IRepositorioBitacora` y
      `RepositorioBitacoraPostgres`.
- [x] Borrados `Peticiones/BitacoraCrear.cs`, `BitacoraReemplazo.cs` y
      `BitacoraActualizar.cs`.
- [x] `014_sp_bitacora.sql` sin tocar (los 5 SP siguen en la BD). Ojo: sus
      comentarios (`COMMENT ON PROCEDURE`, cabecera) todavía mencionan
      `POST/PUT/DELETE /api/Bitacora`; se dejaron así por la regla "no tocar
      la 014" — decidir si se corrigen solo los comentarios.
- [x] Comentarios de cabecera de las 4 capas y de `Program.cs` actualizados.
- [x] `grep`: ningún otro uso de los métodos borrados (API, pruebas, front).
- [x] `dotnet build` → 0 errores, 0 advertencias.

**Resultado**: `IRepositorioBitacora` declara solo `ObtenerPorIdAsync` y
`ObtenerTodosAsync` (lo vigila la prueba de capas).

## Fase 57 — `Parametro` (auditar + D37-D)
> RF192-RF197 de [2_spec.md](2_spec.md), §2.4 de
> [5_data_model.md](5_data_model.md).

- [x] 6 endpoints con `{clave}`; `clave` nunca en el body de `PUT`/`PATCH`.
- [x] `ParametroCrear.Clave`: `[RegularExpression("^[A-Z][A-Z0-9_]*$")]` → 422.
- [x] `tipo_dato`: opcional en `ParametroCrear`/`ParametroActualizar`,
      obligatorio en `ParametroReemplazo`; lista cerrada → 422 (ya estaba).
- [x] `PATCH` vacío → 400 (ya estaba).
- [x] `modificado_en` lo pone `sp_actualizar_parametro`; ningún `now()` en
      C#; el repositorio lo normaliza a UTC.
- [x] `dotnet build` → 0 errores, 0 advertencias.

## Fase 58 — Prueba de capas (37 entidades, criterio 7)

- [x] **Hallazgo:** la prueba de capas cubría **31** entidades, no 33: las
      4 de la v6 (`Pregunta`, `OpcionPregunta`, `RespuestaEncuesta`,
      `RespuestaItem`) no tenían prueba, aunque el README decía "33
      entidades". Se agregaron, porque el criterio 7 pide las 37.
- [x] `Parametro`: crear (default `TEXTO`), clave duplicada (el falso lanza
      como la BD), inexistente → 404, `PATCH` vacío → 400 sin tocar el
      repositorio, `ModificadoEn` avanza, `PUT`, eliminar + segundo
      eliminar → 404, y la forma (clave en minúsculas o con dígito inicial
      → 422; `PUT` sin `tipo_dato` → 422; sin `clave`/`modificado_en` en
      los cuerpos de `PUT`/`PATCH`).
- [x] `Bitacora`: listar (orden, filtro por tabla, filtro en blanco,
      límite), inexistente → 404, y por reflexión: interfaces sin métodos
      de escritura, `BitacoraController` solo con `GET`, sin las 3
      peticiones borradas.
- [x] `LoteMigracion`/`DetalleMigracion`: el servicio solo recibe
      `IRepositorioLoteMigracion` (no puede tocar `Sesion`); verbos de los
      controladores exactamente los del contrato; sin
      `instancia_proceso_asis` en el `PATCH`.
- [x] v6: `Pregunta` (opciones en la misma llamada, rango
      `valor_min`/`valor_max` → 400, `PATCH` parcial que cruza con lo
      guardado), `OpcionPregunta` (CRUD), `RespuestaEncuesta` (responder
      crea maestro + ítems; contrato sin editar ni borrar) y
      `RespuestaItem` (solo lectura).
- [x] Cuenta de control: la API declara 37 controladores.
- [x] `dotnet run --project pruebas` → **452 pasadas, 0 fallidas**, código
      de salida 0 (antes: 385). Entidades con prueba de capas: **37 de 37**.

## Fase 59 — Integridad y reglas de negocio por HTTP (criterios 1-6)
> Datos del §8 de [guia_ia_v7.md](guia_ia_v7.md). Script nuevo:
> `api_catedras/pruebas/verificar_v7.ps1` (71 verificaciones).

- [x] BD nueva; `GET /` → `version: "v7"`; `/swagger/v1/swagger.json` → 37
      recursos, incluidos los 4 de la v7.
- [x] `PATCH /api/Sesion/5712 {"estado":"CERRADA"}`; `POST
      /api/LoteMigracion {"fk_sesion":5712}` → 200, lote **6**,
      `total_filas` 166, `GENERADO`; detalle → 166 filas `PENDIENTE`;
      sesión → `MIGRADA`. Cuerpo en camelCase → 422.
- [x] `POST` con `fk_sesion` 1 → 500 "No hay registros migrables".
- [x] `PATCH` lote `EN_CURSO` → 200; solo observaciones conserva el estado;
      `NO_EXISTE` → 500 (`fk_lotemig_estado`); `{}` → 400; inexistente → 404.
- [x] `PATCH` detalle (registro 167) `ACEPTADO` → 200; detalle (registro 1)
      `ACEPTADO` → 500 `ux_detmig_aceptado`; `OTRO` → 422; `{}` → 400.
- [x] Bitácora: `?limite=5` → 5; `registro_asistencia` → 168 `INSERT` con
      `fk_usuario` nulo; `DELETE /api/Asistente/2` → fila nueva `asistente /
      UPDATE`, llave 2, `activo` true → false; `?limite=0` → 400; tabla
      inexistente → 204.
- [x] Parámetro: `POST` 200, duplicado 500 (`pk_parametro`), minúsculas 422,
      sin `tipo_dato` → `TEXTO`, `PATCH` (`modificado_en` avanza), `PUT` sin
      `tipo_dato` 422, `DELETE` 200, segundo `DELETE` 404.
- [x] **Códigos reales de los verbos inexistentes: 405 en los 9 casos**
      (`POST /api/Bitacora`, `PUT`/`PATCH`/`DELETE /api/Bitacora/1`,
      `PUT`/`DELETE /api/LoteMigracion/6`, `POST /api/DetalleMigracion`,
      `PUT`/`DELETE /api/DetalleMigracion/{id}`). Escritos en
      [2_spec.md](2_spec.md), [6_contracts.md](6_contracts.md) y
      [7_quickstart.md](7_quickstart.md).
- [x] Regresión, cada una sobre BD nueva: `verificar_retrofit_fase4.ps1` →
      **80/0**; `verificar_v5.ps1` → **63/0**;
      `db/postgres/pruebas/prueba_sp_retrofit_fase4.sql` → **160/160**.
- [x] `verificar_v7.ps1` → **71 pasadas, 0 fallidas**.

## Fase 60 — Front: migración, bitácora y parámetros (criterio 8, D37-E)
> Todo dentro de `routes/admin.py`. Prueba de punta a punta con el
> cliente de pruebas de Flask contra la API real: **49/0**.

- [x] Parámetros (`/admin/parametros`): lista, crear (la clave se pasa a
      mayúsculas), editar (clave de solo lectura), eliminar con
      `confirm()`; `tipo_dato` por `<select>`; `fk_usuario_modifica` = el
      administrador de la sesión.
- [x] Lotes de migración (`/admin/lotes-migracion`): lista con `<select>`
      de las sesiones que tienen lotes; detalle del lote con sus filas,
      conteo por resultado y filtro; formulario de estado (`<select>` desde
      `GET /api/EstadoProceso`) y observaciones; formulario por fila para
      `resultado` (`<select>`) y `mensaje`.
- [x] Detalle de migración (`/admin/detalles-migracion`): lista filtrable
      por lote y resultado; enlace a la fila dentro de su lote.
- [x] Bitácora (`/admin/bitacora`): solo lectura, `<select>` de tablas y
      de límite, `datos_antes`/`datos_despues` como JSON indentado.
- [x] `POST /admin/sesiones/<id>/migrar` → `POST /api/LoteMigracion`
      (`fk_usuario` = el administrador) y redirige al lote nuevo.
- [x] `panel.html`: 4 tarjetas nuevas ("Las 37 tablas").
- [x] `detalle_sesion.html`: botón "Generar migración" solo para `ADMIN`,
      sesión terminada y con registros (`puede_migrar` en
      `routes/sesiones.py`); sin sesión o sin `ADMIN` no aparece y la
      ruta responde login/403.
- [x] Errores en castellano (`_avisar_v7`): `ux_detmig_aceptado`, sesión sin
      migrables, estado inexistente, clave repetida, API caída. Ninguna de
      las pantallas recorridas muestra "PUT", "PATCH", "422" ni "FK".
- [x] Con la API apagada, las 5 pantallas nuevas muestran "No se pudo
      contactar la API de Cátedras".
- [x] Revisión visual (capturas con Chromium): panel, parámetros, lote,
      bitácora y detalle de la sesión con el botón.

## Fase 61 — Cierre de la v7 y del proyecto
> Última fase de la última versión del mapa. No hay ninguna v8.

- [x] `Program.cs`: `version = "v7"` y `contratos` →
      `version7/6_contracts.md`.
- [x] `README.md`, `0_mapa_versiones.md` (estado de la V7) y `guia_ia.md`
      (fila de la v7 y enlace a `guia_ia_v7.md`) actualizados; este
      archivo con el total de la prueba de capas (452).
- [x] Cuenta de control 10+5+7+2+5+4+4 = 37 contra
      `/swagger/v1/swagger.json` (lo comprueba `verificar_v7.ps1`).
- [x] `pruebas/correr_todo_v7.ps1`: un comando para todo lo anterior
      contra Docker.
- [x] Repaso de `ESTADO_V6_Y_CUMPLIMIENTO.md`, lo que sigue abierto (no
      se resolvió): 409 en vez de 500 (criterio 7 de `VERSION_2.md`, de
      todo el proyecto); carpeta de prompts de v1-v6 (la v7 ya tiene
      `prompts/`); cierre formal de la v1 (Fase 12) y tags `v1`/`v5`; los
      documentos de la v6 siguen con banner BORRADOR.
- [ ] **Correr `api_catedras/pruebas/correr_todo_v7.ps1` contra Docker**
      y anotar aquí el resumen.
- [ ] Criterio 8 a mano en el front ([7_quickstart.md](7_quickstart.md) §3).
- [ ] Quitar el banner BORRADOR de los 7 documentos de `version7/`.
- [ ] Commit y tag `v7`.
