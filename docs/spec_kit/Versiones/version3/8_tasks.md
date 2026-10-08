# Tareas — Versión 3: personas y carga de asistentes

> ✅ **Fases 22-30 construidas y verificadas (2026-10-07)** contra Docker +
> PostgreSQL reales. Cada fase termina con su **Resultado**. Las
> correcciones al borrador están en el ADR D19 de [4_research.md](4_research.md).
> Queda abierta la Fase 31 (commit y tag `v3`).
>
> Continúa la numeración de fases de
> [version2/8_tasks.md](../version2/8_tasks.md) (termina en Fase 21).
>
> **Nota de alcance (v3):** sin segundo motor, sin devcontainer, sin
> cambios de infraestructura — ver [6_contracts.md](6_contracts.md). La
> BD ya tiene las 7 tablas de esta versión (y sus datos de ejemplo)
> desde el script de la v1 (Artículo 5); lo que no existe todavía son
> los **2 SP** de esta versión y la API/front que los usan.

---

## Fase 22 — Migraciones SQL: los 2 SP maestro-detalle de v3
> Guía IA, Regla 1. Ver §4.1 y §4.3 de [3_plan.md](3_plan.md), ADR D15
> y D16 de [4_research.md](4_research.md). Va primero porque las Fases
> 23 y 26 dependen de que estos SP ya existan en la BD para poder
> probar sus repositorios contra PostgreSQL real.

- [x] `db/postgres/migraciones/002_sp_guardar_asistente.sql` —
      `CREATE OR REPLACE PROCEDURE catedras.sp_guardar_asistente(...)`
      (re-ejecutable); INSERT maestro con `RETURNING id_asistente` +
      4 `INSERT ... SELECT ... FROM jsonb_array_elements(...)`
      condicionales (cada parámetro `jsonb` puede ser `NULL`).
- [x] `db/postgres/migraciones/003_sp_guardar_lote_carga.sql` —
      `CREATE OR REPLACE PROCEDURE catedras.sp_guardar_lote_carga(...)`;
      `UPDATE lote_carga_asistente` con los 3 contadores + `INSERT ...
      SELECT ... FROM jsonb_array_elements(p_novedades)`.
- [x] `docker-compose.yml` — montar ambas migraciones como
      `/docker-entrypoint-initdb.d/z002_...sql` y `z003_...sql` (después
      de `script_bdcatedras.sql` y de `z001_...` de v2 — orden
      alfabético).

**Verificación planeada** (contra PostgreSQL real, volumen recién
creado): ambos `CREATE OR REPLACE PROCEDURE` se aplican 2 veces sin
error; `\df catedras.sp_guardar_asistente` y
`\df catedras.sp_guardar_lote_carga` muestran las firmas con los
parámetros `jsonb` esperados; simular el orden de `initdb` (igual que
se hizo para `z001_` en v2) confirma que ambos corren después del
script principal.

**Resultado (2026-10-07):** ambos `CREATE OR REPLACE PROCEDURE` aplicados 2 veces
sin error; `\df` muestra las firmas (la de `sp_guardar_asistente` incluye
`p_nombre_completo_asis`, D19). En un PostgreSQL temporal con volumen vacío,
initdb corrió `script_` → `z001_` → `z002_` → `z003_` sin errores.

## Fase 23 — La aplicación (capas de `Asistente` y `DocumentoAsistente`)
> RF95-RF106 de [2_spec.md](2_spec.md), §2.1-2.2 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 22 (el SP de
> `Asistente` debe existir).

- [x] `Modelos/Asistente.cs`, `DocumentoAsistente.cs` — `IdAsistente` e
      `IdDocumento` sin `required` (los pone la BD).
- [x] `Peticiones/AsistenteCrear.cs` — `nombres`/`apellidos`
      obligatorios; `id_asis` con
      `[RegularExpression("^[0-9A-Za-z]{5,15}$")]`, sin `[Required]`
      (depende de `es_externo`, cruce de campos → lo valida el
      servicio, no la anotación); **4 propiedades de detalle**
      opcionales (`List<DetalleDocumentoInicial>?`, etc.) — ninguna
      lleva `fk_asistente`.
      `AsistenteReemplazo.cs`/`AsistenteActualizar.cs` — solo columnas
      propias, sin detalles (RF98-99).
- [x] `Peticiones/DetalleDocumentoInicial.cs`,
      `DetalleVinculacionInicial.cs`, `DetalleProgramaInicial.cs`,
      `DetalleConsentimientoInicial.cs` — mismo criterio que
      `AliasInicial.cs` de v2 (§4.1 de [3_plan.md](3_plan.md)).
- [x] `Peticiones/DocumentoAsistente{Crear,Reemplazo,Actualizar}.cs` —
      molde completo de 3 peticiones, a diferencia de `AliasPrograma`.
- [x] `Repositorios/IRepositorioAsistente.cs` +
      `RepositorioAsistentePostgres.cs` — `ObtenerTodosAsync` recibe
      `string? q` y lo usa en un `WHERE ... ILIKE` solo si no es
      `null`/vacío (ADR D18); `CrearAsync` serializa las 4 listas y
      hace `CALL catedras.sp_guardar_asistente(..., CAST(@Documentos AS
      jsonb), CAST(@Vinculaciones AS jsonb), CAST(@Programas AS jsonb),
      CAST(@Consentimiento AS jsonb))`.
- [x] `Repositorios/IRepositorioDocumentoAsistente.cs` +
      `RepositorioDocumentoAsistentePostgres.cs` — CRUD estándar, 5
      métodos.
- [x] `Servicios/IServicioAsistente.cs` + `ServicioAsistente.cs` —
      `CrearAsync` valida el cruce `id_asis`/`es_externo` **solo en el
      lado que la petición puede verificar sin tocar la BD** (que no
      sean ambos simultáneamente nulos cuando `es_externo` es falso);
      el resto del `CHECK` (p. ej. en un `PATCH`) se deja al
      `chk_asistente_externo` de la BD (D9 extendido, RF101 — decidir en
      la implementación si vale la pena adelantar algo de esa
      validación en C# o dejarla toda a la BD; cualquiera de las dos
      cumple la spec). Arma las 4 listas de Modelo con `fk_asistente` =
      lo que el repositorio devuelva tras el `CALL`.
- [x] `Servicios/IServicioDocumentoAsistente.cs` +
      `ServicioDocumentoAsistente.cs`.
- [x] `Controllers/AsistenteController.cs` — 6 endpoints; `GET` lista
      recibe `[FromQuery] string? q` además de `limite`.
- [x] `Controllers/DocumentoAsistenteController.cs` — 6 endpoints.
- [x] `Program.cs` — registrar las 2 parejas nuevas (`AddScoped`).

**Verificación planeada** (contra PostgreSQL real): `GET
/api/Asistente?limite=5` → 5 de 14.811 filas; `GET
/api/Asistente?q=1000445` → solo coincidencias (corregido: `Garcia` no existe en los datos sembrados), confirmando que el
`WHERE` corre en SQL (revisar el log de Postgres o el plan de
ejecución, no solo el resultado); `POST /api/Asistente` con las 4
listas de detalle → 200 y las 4 tablas muestran sus filas con el mismo
`id_asistente`; el mismo `POST` repetido → 500 y `GET
/api/Asistente/{id}` del segundo intento → 404 (todo revertido); `POST
/api/DocumentoAsistente` con un segundo `vigente: true` → 500
(`ux_documento_vigente`).

**Resultado (2026-10-07):** `?limite=5` → 5 filas; `?q=1000445` → 1;
`?limite=2&pagina=2` → ids 3 y 4. `POST` con los 4 detalles → 200 y las 4
tablas con el mismo `id_asistente`; mismo `id_asis` → 500 y nada creado;
un programa inexistente en el detalle → 500 y el asistente NO quedó.
2º documento vigente → 500 (`ux_documento_vigente`). Cruce
`id_asis`/`es_externo` y "sin correo" → 400 (servicio, D19).

## Fase 24 — La aplicación (capas de `VinculacionAsistente` y `ProgramaAsistente`)
> RF107-RF111 de [2_spec.md](2_spec.md), ADR D16, §2.3-2.4 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 23
> (`fk_asistente`). Primera vez en el proyecto sin el molde de 5/6
> verbos — construir con cuidado de no copiar por costumbre un
> `Controller`/`Repositorio` con `/{id}`.

- [x] `Modelos/VinculacionAsistente.cs`, `ProgramaAsistente.cs` — SIN
      propiedad `Id`/`IdVinculacion`: la llave primaria son sus propias
      columnas de negocio.
- [x] `Peticiones/VinculacionAsistenteCrear.cs`,
      `VinculacionAsistenteCerrar.cs` — la segunda lleva las 3 columnas
      de la llave (`fk_asistente`, `fk_tipo_vinculacion`, `fecha_ini`)
      más `fecha_fin`. **No** crear `Reemplazo`/`Actualizar` (§4.2 de
      [3_plan.md](3_plan.md): la ausencia es la especificación).
- [x] `Peticiones/ProgramaAsistenteCrear.cs`,
      `ProgramaAsistenteCambiarEstado.cs` — la segunda lleva
      `fk_asistente`, `fk_programa`, `fk_periodo` + `estado` (lista
      cerrada, `[RegularExpression]`).
- [x] `Repositorios/IRepositorioVinculacionAsistente.cs` +
      `RepositorioVinculacionAsistentePostgres.cs` — 3 métodos
      exactamente (código de la interfaz en §4.2 de
      [3_plan.md](3_plan.md)): `ObtenerTodosAsync`, `CrearAsync`,
      `CerrarAsync`. **Sin** `ActualizarAsync`/`EliminarAsync` en la
      interfaz.
- [x] `Repositorios/IRepositorioProgramaAsistente.cs` +
      `RepositorioProgramaAsistentePostgres.cs` — idem, con
      `CambiarEstadoAsync` en vez de `CerrarAsync`.
- [x] `Servicios/IServicioVinculacionAsistente.cs` +
      `ServicioVinculacionAsistente.cs` — `CerrarAsync` primero
      `ObtenerTodosAsync` filtrando por la llave para comprobar que
      existe y que `fecha_fin` sigue `null`; si ya estaba cerrada →
      `ArgumentException` (400), no una llamada al repositorio.
- [x] `Servicios/IServicioProgramaAsistente.cs` +
      `ServicioProgramaAsistente.cs` — mismo criterio con `estado`.
- [x] `Controllers/VinculacionAsistenteController.cs` — **3**
      endpoints: `[HttpGet]` lista, `[HttpPost]`,
      `[HttpPatch("cerrar")]`. Sin `[HttpGet("{id}")]` (no hay `id`).
- [x] `Controllers/ProgramaAsistenteController.cs` — **3** endpoints:
      `[HttpGet]` lista, `[HttpPost]`,
      `[HttpPatch("cambiar-estado")]`.
- [x] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada** (contra PostgreSQL real): `POST
/api/VinculacionAsistente` → 200; `PATCH .../cerrar` con la misma
llave → 200 y `fecha_fin` puesta; repetir → 400; un `GET
/api/VinculacionAsistente/1` (ruta que no existe) → 404 de ASP.NET,
confirmando que no quedó ningún `[HttpGet("{id}")]` olvidado. Mismo
patrón para `ProgramaAsistente` con `cambiar-estado`.

**Resultado (2026-10-07):** abrir → 200; cerrar → 200 con `fecha_fin`;
repetir → 400; `fecha_fin < fecha_ini` → 400; llave inexistente → 404;
misma llave en `POST` → 500 (PK). `GET`/`DELETE /api/VinculacionAsistente/1`
→ 404. `cambiar-estado` → 200; al mismo estado → 400; llave inexistente
→ 404; estado fuera de la lista → 422.

## Fase 25 — La aplicación (capas de `ConsentimientoDatos`)
> RF112-RF113 de [2_spec.md](2_spec.md), §2.5 de
> [5_data_model.md](5_data_model.md). Depende de la Fase 23
> (`fk_asistente`). La entidad más simple de la versión: 3 verbos, sin
> ninguna decisión de diseño nueva (sigue el molde de `AliasPrograma`,
> solo con un verbo menos).

- [x] `Modelos/ConsentimientoDatos.cs` — `IdConsentimiento` y
      `AceptadoEn` sin `required` (los pone la BD).
- [x] `Peticiones/ConsentimientoDatosCrear.cs` — **única** petición de
      esta entidad (igual criterio que `AliasProgramaCrear` en v2):
      `fk_asistente`, `version_politica` obligatorios; `canal` con
      `[RegularExpression]` sobre la lista cerrada, por defecto `WEB`;
      `ip` opcional.
- [x] `Repositorios/IRepositorioConsentimientoDatos.cs` +
      `RepositorioConsentimientoDatosPostgres.cs` — **sin**
      `ActualizarAsync` ni `EliminarAsync` en la interfaz; `CrearAsync`
      usa `RETURNING id_consentimiento, aceptado_en`.
- [x] `Servicios/IServicioConsentimientoDatos.cs` +
      `ServicioConsentimientoDatos.cs` — sin métodos de actualizar ni
      eliminar.
- [x] `Controllers/ConsentimientoDatosController.cs` — **3** endpoints:
      `GET` lista, `GET/{id}`, `POST`. Sin `[HttpPut]`, `[HttpPatch]`
      ni `[HttpDelete]`.
- [x] `Program.cs` — registrar la pareja nueva.

**Verificación planeada**: `POST /api/ConsentimientoDatos` → 200 con
`aceptado_en` puesto por la BD; repetir la misma `version_politica`
para el mismo asistente → 500 (`uq_consentimiento_ver`); `PUT
/api/ConsentimientoDatos/1` → 404 de ASP.NET.

**Resultado (2026-10-07):** `POST` → 200 con `aceptado_en` de la BD;
misma versión → 500 (`uq_consentimiento_ver`); `PUT`/`DELETE` sobre
`/{id}` → 405 (la ruta `/{id}` solo existe para `GET`).

## Fase 26 — La aplicación (capas de `LoteCargaAsistente` y `NovedadCarga`)
> RF114-RF118 de [2_spec.md](2_spec.md), §2.6-2.7 de
> [5_data_model.md](5_data_model.md), ADR D15. Depende de la Fase 22
> (el segundo SP) y, para la prueba de cierre con asistentes reales, de
> la Fase 23.

- [x] `Modelos/LoteCargaAsistente.cs`, `NovedadCarga.cs`.
- [x] `Peticiones/LoteCargaAsistenteCrear.cs` — solo
      `nombre_archivo` obligatorio; **no** expone los 3 contadores (los
      pone la BD en 0 al crear).
- [x] `Peticiones/LoteCargaAsistenteCerrar.cs` — `filas_leidas`,
      `filas_aceptadas`, `filas_rechazadas` obligatorios (snake_case, D19);
      `observaciones` opcional; `novedades: List<NovedadInicial>`
      (puede ser `[]`).
- [x] `Peticiones/NovedadInicial.cs` — `numeroFila`, `contenidoCrudo`,
      `motivo`; sin `fk_lote_carga` (lo fija el servicio con el id del
      lote que se está cerrando).
- [x] `Repositorios/IRepositorioLoteCargaAsistente.cs` +
      `RepositorioLoteCargaAsistentePostgres.cs` — **sin**
      `ActualizarAsync`/`EliminarAsync`; `CerrarAsync` hace `CALL
      catedras.sp_guardar_lote_carga(..., CAST(@Novedades AS jsonb))`.
- [x] `Repositorios/IRepositorioNovedadCarga.cs` +
      `RepositorioNovedadCargaPostgres.cs` — **solo**
      `ObtenerTodosAsync`/`ObtenerPorIdAsync` en la interfaz (sin
      `CrearAsync`: nace dentro del SP, no por un `INSERT` del
      repositorio).
- [x] `Servicios/IServicioLoteCargaAsistente.cs` +
      `ServicioLoteCargaAsistente.cs` — `CerrarAsync` valida
      `filas_aceptadas + filas_rechazadas <= filas_leidas` y una novedad
      por fila rechazada (400 si no
      cuadra) y que el lote no esté ya cerrado (400 si
      `observaciones`/algún contador ya es distinto de su valor
      inicial — ver criterio exacto en [5_data_model.md](5_data_model.md)
      §2.6) ANTES de llamar al repositorio.
- [x] `Servicios/IServicioNovedadCarga.cs` + `ServicioNovedadCarga.cs` —
      sin método de crear (de solo lectura).
- [x] `Controllers/LoteCargaAsistenteController.cs` — **4** endpoints:
      `GET` lista, `GET/{id}`, `POST`, `POST/{id}/cerrar`.
- [x] `Controllers/NovedadCargaController.cs` — **2** endpoints: `GET`
      lista (con `?fkLoteCarga=`), `GET/{id}`.
- [x] `Program.cs` — registrar las 2 parejas nuevas.

**Verificación planeada**: `POST /api/LoteCargaAsistente` → 200 con
los 3 contadores en 0; `POST .../{id}/cerrar` con contadores que no
cuadran (`aceptadas + rechazadas > leidas`) → 400 sin tocar la BD;
con contadores correctos y 1 novedad → 200; `GET
/api/NovedadCarga?fkLoteCarga={id}` → esa fila; repetir el `POST
.../cerrar` → 400 (ya cerrado).

**Resultado (2026-10-07):** abrir → 200 con contadores en 0; contadores
que no cuadran → 400; faltan novedades → 400; `motivo` > 200 → 422;
cerrar → 200; `GET /api/NovedadCarga?fkLoteCarga=` → la novedad; repetir
→ 400; lote inexistente → 404; `POST /api/NovedadCarga` → 405.

## Fase 27 — Prueba de capas ampliada (criterio de aceptación 8)
> Depende de las Fases 23-26 (las 7 entidades deben existir).

- [x] Agregar a `pruebas/Programa.cs` un repositorio FALSO y sus
      verificaciones para las 7 entidades de v3. Puntos especiales:
      - `RepositorioAsistenteFalso` simula el `CALL` del SP devolviendo
        un id fijo y guardando las 4 listas de detalle en memoria, para
        poder verificar sin PostgreSQL que el servicio arma
        correctamente `fk_asistente` en cada detalle.
      - `RepositorioVinculacionAsistenteFalso`/
        `RepositorioProgramaAsistenteFalso` **no** implementan
        `ActualizarAsync` (no existe en la interfaz — Fase 24); un caso
        de prueba fuerza "cerrar dos veces" y confirma el 400 desde el
        servicio, sin tocar el repositorio la segunda vez.
      - `RepositorioLoteCargaAsistenteFalso` incluye un caso con
        contadores que no cuadran, para confirmar que el 400 lo lanza
        el servicio ANTES de llamar al repositorio (el repositorio
        falso nunca se invoca en ese caso).

**Verificación planeada**: `dotnet run --project pruebas` → cubre las
**22** entidades, código de salida 0. Anotar aquí el total final de
verificaciones pasadas una vez construido (en v2 llegó a 158; se
espera un salto comparable, dado que son 7 entidades nuevas con reglas
propias).

**Resultado (2026-10-07):** `docker compose exec api dotnet run --project
pruebas` → **220 pasadas, 0 fallidas** (v2 terminó en 158: +62). Se corre en
el contenedor porque en Windows el Control de aplicaciones bloqueó la DLL.

## Fase 28 — Verificación de integridad referencial y reglas de negocio (criterios de aceptación 3-7)
> Depende de las Fases 23-26. Como en v2 (Fase 18), es la fase
> dedicada exclusivamente a probar lo que la BD hace sola — RESTRICT,
> CHECK, índices únicos — sin código nuevo.

- [x] Ejecutar, contra Docker + PostgreSQL reales y EN ESTE ORDEN:
  1. `POST /api/Asistente` con detalles completos → 200; repetir con el
     mismo `id_asis` → 500, y confirmar con `GET` que no quedó nada de
     los 4 detalles (no solo del asistente).
  2. `POST /api/DocumentoAsistente` con un segundo vigente para el
     mismo asistente → 500 (`ux_documento_vigente`); con el mismo
     número y tipo de documento que otro asistente de ejemplo → 500
     (`uq_documento_numero`).
  3. `PATCH /api/VinculacionAsistente/cerrar` dos veces sobre la misma
     llave → 200 la primera, 400 la segunda.
  4. `POST /api/ConsentimientoDatos` con la misma `version_politica`
     para el mismo asistente dos veces → 200 la primera, 500 la
     segunda (`uq_consentimiento_ver`).
  5. `DELETE /api/Asistente/{id}` sobre uno de los 14.810 reales con
     `registro_asistencia` sembrado (el asistente 1; corregido:
     `programa_asistente` es CASCADE) → 500; confirmar con `GET` que
     sus `documento_asistente`/`vinculacion_asistente` de ejemplo
     siguen intactos.
  6. `POST /api/LoteCargaAsistente/{id}/cerrar` con contadores que no
     cuadran → 400 (del servicio, no de la BD).
- [x] `docker compose down -v && docker compose up -d --build` —
      revertir los efectos de arriba antes de seguir con cualquier otra
      prueba manual.

**Verificación planeada**: los 6 resultados coinciden con lo
documentado en [5_data_model.md](5_data_model.md) y con los criterios
de aceptación 3-7 de [2_spec.md](2_spec.md). Si alguno no coincide, el
error está en el modelo de datos o en la implementación de esta
versión — no se "corrige" simulando en C# una regla que ya es de la BD
(Artículo 5).

**Resultado (2026-10-07):** los 6 casos coinciden (con las correcciones
de D19): `DELETE /api/Asistente/1` → 500 por `fk_registro_asistente` y su
documento sigue ahí; el asistente de prueba sí se borra con sus 4
detalles. El reset (`down -v && up -d --build`) se hizo al cerrar la
versión (Fase 31), con autorización del usuario.

## Fase 29 — Frontend: listado buscable de `Asistente` y administración de las 7 tablas
> RF95 (el `?q=`), criterios 9-10 de [2_spec.md](2_spec.md), ADR D18.
> Depende de las Fases 23-25 (API completa para estas 5 tablas; la
> pantalla de carga, que también toca `Asistente`, queda en la Fase 30
> para no mezclar las dos pantallas nuevas en una sola fase).

- [x] `routes/admin.py` — bloque de `Asistente` con buscador: cada
      tecla (o con *debounce*) dispara `GET /api/Asistente?q=...`, no
      una llamada sin filtro seguida de un filtro en Python (eso sería
      D13, no D18).
- [x] `routes/admin.py` — bloques de `DocumentoAsistente`,
      `VinculacionAsistente`, `ProgramaAsistente`, `ConsentimientoDatos`,
      `NovedadCarga`: los dos de llave compuesta ofrecen "abrir" y
      "cerrar"/"cambiar estado", no "editar"; `ConsentimientoDatos` y
      `NovedadCarga` solo muestran, sin formulario de edición.
- [x] `templates/admin/` — templates nuevos siguiendo el layout ya
      existente desde v1/v2.

**Verificación planeada**: `http://localhost:5000/admin/asistentes` (o
la ruta elegida) pagina y busca sin descargar las 14.811 filas de una
vez (confirmar revisando las llamadas de red del front, no solo el
resultado en pantalla); los formularios de `VinculacionAsistente`/
`ProgramaAsistente` no ofrecen "editar" en ningún punto de la interfaz.

**Resultado (2026-10-07):** `/admin/asistentes` busca con `?q=` y pagina
con `?pagina=` en la API (cada búsqueda = 1 petición de 50 filas; debounce
de 400 ms). Ficha del asistente con sus 4 detalles: abrir/cerrar
vinculación, matricular/cambiar estado, retirar vigencia, registrar
consentimiento; ningún "editar" en las tablas de llave compuesta. Los
errores 400/422/500 de la API se muestran en pantalla.

## Fase 30 — Frontend: pantalla de carga de datos
> RNF8-RNF9, criterio 9 de [2_spec.md](2_spec.md), ADR D15 y D17. Única
> fase de v3 que agrega una pantalla completamente nueva, no una
> extensión del panel de administración. Depende de las Fases 23 y 26
> (la API de `Asistente` y `LoteCargaAsistente` completas).

- [x] `front_catedras/servicios/carga_asistentes.py` — interpreta el
      archivo subido (`openpyxl`/`pandas`); por cada fila construye el
      body de `POST /api/Asistente` (resolviendo lo que pueda del
      programa en texto libre contra el catálogo ya cargado; lo que no
      resuelva, lo manda igual y deja que la API lo rechace).
- [x] `routes/carga.py` (o el nombre que fije la implementación) — abre
      el lote (`POST /api/LoteCargaAsistente`), llama
      `servicios/carga_asistentes.py` fila por fila, acumula
      aceptadas/rechazadas/novedades, y cierra el lote al final
      (`POST .../{id}/cerrar`) — pseudocódigo exacto en §5 de
      [3_plan.md](3_plan.md).
- [x] `templates/carga_datos.html` — formulario de subida + pantalla de
      resultado (contadores + tabla de novedades con su motivo).

**Verificación planeada**: subir un archivo de prueba con 2-3 filas
(una inválida a propósito) → la pantalla reporta los contadores
correctos y el motivo exacto de la fila rechazada; `GET
/api/NovedadCarga?fkLoteCarga={id}` confirma que ese motivo quedó
guardado en la BD, no solo mostrado en pantalla; apagar la API durante
una carga → error claro, sin datos inventados.

**Resultado (2026-10-07):** `asistentes_ejemplo.csv` (3 filas, 1
inválida) → 2 aceptadas, 1 rechazada con su motivo, igual en
`/api/NovedadCarga`. Un `.xlsx` con números (sin `.0`), programa por nombre
y por alias, y una fila vacía (saltada) → correcto. 400 filas: < 3 s.
4.000 filas con la API detenida a mitad → "se detuvo en la fila N", lote
ABIERTO, sin datos inventados.

## Fase 31 — Cierre de la v3
> Última fase. Solo entonces se escribe la spec de la v4 (Artículo 1).

- [x] Confirmar que los 10 criterios de aceptación de
      [2_spec.md](2_spec.md) §5 pasan, en orden, sobre un entorno
      recién reseteado (`down -v && up -d --build`).
- [x] `dotnet run --project pruebas` → anotar el total final de
      verificaciones pasadas (Fase 27): **220**.
- [x] Revisar este spec kit completo (los 7 documentos) contra lo que
      realmente quedó construido — quitar el banner de "BORRADOR" de
      cada archivo una vez confirmado que coincide. (Hecho; ADR D19.)
- [x] Commit y tag: `git tag v3`.

**Resultado (2026-10-07):** tras `docker compose down -v && docker compose
up -d --build` (initdb creó solo los 3 SP; 14.811 asistentes, 1 lote), los
10 criterios pasaron en orden: 22 recursos en Swagger; `?limite=5`/`?q=`;
maestro-detalle todo o nada; `ux_documento_vigente`; cerrar vinculación
y lote dos veces → 400; `DELETE /api/Asistente/1` → 500 con su documento
intacto; prueba de capas 220/0; carga CSV cerrada; 23 tarjetas en `/admin`
(15 + 7 + carga). Luego, segundo reset para dejar los datos originales.
